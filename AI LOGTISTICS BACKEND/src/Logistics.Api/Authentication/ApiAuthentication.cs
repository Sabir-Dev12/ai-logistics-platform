using System.Security.Claims;
using System.Text.Encodings.Web;
using Logistics.Application.Features.Authentication;
using Logistics.Infrastructure;
using Logistics.Api.Hubs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Logistics.Api.Authentication;

/// <summary>
/// Authentication wiring for the API.
/// </summary>
/// <remarks>
/// <para>
/// The scheme is custom rather than JWT because
/// <c>Microsoft.AspNetCore.Authentication.JwtBearer</c> is not available in this offline environment; see
/// <see cref="ILogisticsTokenService"/> for why, and for the seam to replace it.
/// </para>
/// <para>
/// Authorisation is applied as a <b>global filter</b> rather than per-controller attributes. A controller
/// added later is then protected by default, and forgetting an attribute cannot silently expose it — the
/// failure mode of per-controller attributes.
/// </para>
/// </remarks>
public static class ApiAuthentication
{
    public const string Scheme = "LogisticsToken";

    /// <summary>Policy requiring the parameterisation role.</summary>
    public const string PolitiqueAdmin = "Parameterisation";

    /// <summary>Claim types carried by an authenticated caller.</summary>
    public static class Claims
    {
        public const string Societe = "logistics:societe";
        public const string Agence = "logistics:agence";
        public const string UserKey = "logistics:user_key";
        public const string UserName = "logistics:user_name";
        public const string Workstation = "logistics:workstation";
        /// <summary>Present only for callers allowed to change parameterisation.</summary>
        public const string Admin = "logistics:admin";
        /// <summary>One claim per dossier type the caller may handle. Absent means unrestricted.</summary>
        public const string TypesDossier = "logistics:dossier_type";
    }

    /// <summary>
    /// Registers the authentication scheme, authorisation and the caller-context population.
    /// </summary>
    /// <remarks>
    /// The header fallback is enabled <b>only</b> when the host environment is Development <em>and</em>
    /// configuration asks for it. Both conditions are required: enabling it from configuration alone would
    /// let a misconfigured production deployment reopen the hole this change closes.
    /// </remarks>
    public static IServiceCollection AddApiAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        bool estDeveloppement)
    {
        var options = new TokenOptions
        {
            SigningKey = configuration["Authentication:SigningKey"],
            Issuer = configuration["Authentication:Issuer"] ?? "Logistics.Api",
            TokenLifetimeMinutes = int.TryParse(configuration["Authentication:TokenLifetimeMinutes"], out var minutes)
                ? minutes
                : 480,
            AllowHeaderFallback = estDeveloppement
                && string.Equals(configuration["Authentication:AllowHeaderFallback"], "true", StringComparison.OrdinalIgnoreCase)
        };

        // Fail closed. The only situation where a missing signing key is tolerated is the development
        // fallback, which is itself gated on Development *and* an explicit opt-in. Anywhere else a host
        // without a key cannot authenticate anyone, so starting up would mean running an API that either
        // rejects every request or — worse — quietly trusts headers.
        if (!options.AllowHeaderFallback)
        {
            options.Validate();
        }

        services.AddSingleton(options);
        services.AddSingleton<ILogisticsTokenService, LogisticsTokenService>();

        services
            .AddAuthentication(Scheme)
            .AddScheme<AuthenticationSchemeOptions, TokenAuthenticationHandler>(Scheme, _ => { });

        services.AddAuthorization(options =>
        {
            // Only a caller whose signed token carries the flag may change parameterisation. The claim is
            // absent for everyone else, so the policy cannot be satisfied by simply being authenticated.
            options.AddPolicy(PolitiqueAdmin, policy =>
                policy.RequireAuthenticatedUser().RequireClaim(Claims.Admin, "true"));
        });

        return services;
    }
}

/// <summary>
/// Validates the bearer token and turns it into a principal.
/// </summary>
/// <remarks>
/// A missing or non-bearer <c>Authorization</c> header yields <c>NoResult</c> rather than a failure, so the
/// request is treated as anonymous and the global authorisation filter decides — which is what lets the
/// token endpoint itself stay reachable.
/// </remarks>
public sealed class TokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ILogisticsTokenService tokens)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const string Bearer = "Bearer ";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var entete = Request.Headers.Authorization.FirstOrDefault();

        // Browsers cannot attach Authorization headers to WebSocket/SSE requests.
        // Only the chat Hub may read the SignalR access_token query parameter.
        if (string.IsNullOrWhiteSpace(entete)
            && Request.Path.StartsWithSegments(ChatHub.Endpoint)
            && Request.Query.TryGetValue("access_token", out var accessToken)
            && accessToken.Count == 1 && !string.IsNullOrWhiteSpace(accessToken[0]))
        {
            entete = Bearer + accessToken[0];
        }

        if (string.IsNullOrWhiteSpace(entete)
            || !entete.StartsWith(Bearer, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var resultat = tokens.Validate(entete[Bearer.Length..].Trim());

        if (!resultat.IsValid)
        {
            // The rejection reason is surfaced so a client can distinguish an expired token from a forged
            // one — but never the signature or payload.
            return Task.FromResult(AuthenticateResult.Fail($"Invalid token ({resultat.Rejection})."));
        }

        var identite = resultat.Identity!;
        var claims = new List<Claim> { new(ApiAuthentication.Claims.Societe, identite.SocieteKey) };

        Ajouter(claims, ApiAuthentication.Claims.Agence, identite.AgenceKey);
        Ajouter(claims, ApiAuthentication.Claims.UserKey, identite.UserKey);
        Ajouter(claims, ApiAuthentication.Claims.UserName, identite.UserDisplayName);
        Ajouter(claims, ApiAuthentication.Claims.Workstation, identite.Workstation);

        // Added only when true: an absent claim is what makes the policy unachievable for ordinary callers.
        if (identite.EstAdmin)
        {
            claims.Add(new Claim(ApiAuthentication.Claims.Admin, "true"));
        }

        // One claim per scoped dossier type. No claims at all is the "empty scope" case, which the roles
        // object resolves to unrestricted — see CallerRoles.AutoriseTypeDossier for why that reading was
        // chosen and why it is logged rather than silent.
        foreach (var type in identite.TypesDossierAutorises ?? [])
        {
            if (!string.IsNullOrWhiteSpace(type))
            {
                claims.Add(new Claim(ApiAuthentication.Claims.TypesDossier, type));
            }
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        var properties = new AuthenticationProperties { ExpiresUtc = identite.ExpiresAt };
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, properties, Scheme.Name)));
    }

    private static void Ajouter(List<Claim> claims, string type, string? valeur)
    {
        if (!string.IsNullOrWhiteSpace(valeur))
        {
            claims.Add(new Claim(type, valeur));
        }
    }
}

/// <summary>
/// Populates the scoped <see cref="CallerContext"/> for the request.
/// </summary>
/// <remarks>
/// Runs after authentication so the principal is already resolved. Without a valid token it leaves the
/// caller null, and the tenant context then refuses to resolve a company — which is what turns an
/// unauthenticated request into a rejection instead of a request scoped to whoever the header named.
/// </remarks>
public sealed class CallerContextMiddleware(RequestDelegate next, bool headerFallbackAllowed)
{
    public async Task InvokeAsync(HttpContext context, CallerContext caller)
    {
        caller.HeaderFallbackAllowed = headerFallbackAllowed;

        if (headerFallbackAllowed)
        {
            caller.HeaderSociete = Entete(context, "X-Societe-Key");
            caller.HeaderAgence = Entete(context, "X-Agence-Key");
            caller.HeaderUserKey = Entete(context, "X-User-Key");
            caller.HeaderUserName = Entete(context, "X-User-Name");
            caller.HeaderWorkstation = Entete(context, "X-Workstation");
        }

        var utilisateur = context.User;

        if (utilisateur?.Identity?.IsAuthenticated == true)
        {
            var societe = utilisateur.FindFirst(ApiAuthentication.Claims.Societe)?.Value;

            if (!string.IsNullOrWhiteSpace(societe))
            {
                caller.Caller = new CallerIdentity(
                    societe,
                    utilisateur.FindFirst(ApiAuthentication.Claims.Agence)?.Value,
                    utilisateur.FindFirst(ApiAuthentication.Claims.UserKey)?.Value,
                    utilisateur.FindFirst(ApiAuthentication.Claims.UserName)?.Value,
                    utilisateur.FindFirst(ApiAuthentication.Claims.Workstation)?.Value,
                    DateTime.UtcNow,
                    // Read back from the principal, which the handler built from the SIGNED payload — not
                    // from anything the request supplied.
                    EstAdmin: string.Equals(
                        utilisateur.FindFirst(ApiAuthentication.Claims.Admin)?.Value, "true",
                        StringComparison.OrdinalIgnoreCase),
                    TypesDossierAutorises: utilisateur.FindAll(ApiAuthentication.Claims.TypesDossier)
                        .Select(c => c.Value)
                        .ToArray());
            }
        }

        await next(context);
    }

    private static string? Entete(HttpContext context, string nom)
    {
        var valeur = context.Request.Headers[nom].FirstOrDefault();
        return string.IsNullOrWhiteSpace(valeur) ? null : valeur;
    }
}
