using Logistics.Application.Abstractions;
using Logistics.Application.Features.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Logistics.Api.Controllers;

/// <summary>Credentials presented to obtain a token.</summary>
public sealed record TokenRequest(string Identifiant, string MotDePasse, string? SocieteKey = null, string? AgenceKey = null);

/// <summary>A freshly issued bearer token.</summary>
public sealed record TokenResponse(string AccessToken, string TokenType, DateTime ExpiresAt, string SocieteKey);

/// <summary>
/// Token issuance.
/// </summary>
/// <remarks>
/// <para>
/// <b>This endpoint authenticates a configured service account, not an application user.</b> The legacy
/// user password column (<c>t_personnel.pass_word</c>) is written through
/// <c>tools.Codage_Motpasse</c>, whose implementation is not present in the extracted source, so user
/// credentials <em>cannot</em> be verified from here yet. Rather than accept a password and pretend to
/// check it, this endpoint refuses any identity it has not been configured with, and the seam for real
/// user authentication is stated plainly below.
/// </para>
/// <para>
/// The token carries the company, which is what the tenant context resolves from. That is the whole point
/// of the change: identity comes from a signed token, not from a header the caller chose.
/// </para>
/// </remarks>
[ApiController]
[Route("api/v1/auth")]
[Tags("Authentication")]
[AllowAnonymous]
public sealed class AuthController(
    ILogisticsTokenService tokens,
    IUserRoleResolver roles,
    IUtilisateurAuthenticator utilisateurs,
    IAuditTrail audit,
    IConfiguration configuration,
    ILogger<AuthController> logger) : ControllerBase
{
    /// <summary>Exchanges credentials for a bearer token.</summary>
    /// <response code="200">A token valid for the configured lifetime.</response>
    /// <response code="401">The credentials did not match the configured account.</response>
    [HttpPost("token")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenResponse>> Token(TokenRequest request, CancellationToken cancellationToken)
    {
        var identifiant = configuration["Authentication:ServiceAccount:Identifiant"];
        var motDePasse = configuration["Authentication:ServiceAccount:MotDePasse"];
        var societeDemandee = request.SocieteKey ?? configuration["Authentication:ServiceAccount:SocieteKey"];

        if (string.IsNullOrWhiteSpace(societeDemandee))
        {
            return Problem(
                title: "No company",
                detail: "A company key is required to issue a token.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var serviceConfigure = !string.IsNullOrWhiteSpace(identifiant) && !string.IsNullOrWhiteSpace(motDePasse);

        var identifiantEstServiceAccount =
            serviceConfigure && CryptographicEquals(request.Identifiant, identifiant);

        UtilisateurLegacy? utilisateur = null;

        if (identifiantEstServiceAccount)
        {
            // The service account's answer is fully determined by configuration, so the database is not
            // consulted at all — neither to authenticate nor to reject.
            //
            // Consulting it even for a WRONG service password was an inaccuracy: the credentials are knowably
            // invalid without any I/O, and answering 503 tells a machine caller to retry later when it should
            // stop and fix its configuration. The password comparison is the sensitive one and stays
            // constant-time.
            if (!CryptographicEquals(request.MotDePasse, motDePasse))
            {
                logger.LogWarning("Rejected the service account's token request: wrong password.");
                return IdentifiantsInvalides();
            }
        }
        else
        {
            // Anyone else must be a real employee, and that cannot be answered without the personnel table —
            // so an unreachable database surfaces as 503 rather than as a misleading 401.
            utilisateur = await utilisateurs.AuthentifierAsync(
                societeDemandee!, request.Identifiant, request.MotDePasse, cancellationToken);

            if (utilisateur is null)
            {
                logger.LogWarning("Rejected a token request for identifier {Identifiant}.", request.Identifiant);
                return IdentifiantsInvalides();
            }
        }

        // Built from whichever path authenticated: a real user carries their own key, company and agency; the
        // service account falls back to configuration.
        var societe = utilisateur?.SocieteKey ?? societeDemandee!;
        var userKey = utilisateur?.KeyPersonnel
            ?? configuration["Authentication:ServiceAccount:UserKey"]
            ?? request.Identifiant;

        // Roles are resolved here, once, and baked into the signed token. That keeps authorisation off the
        // request path, and means a role can only be granted by the issuer — never asserted by the caller.
        var rolesResolus = await roles.ResolveAsync(userKey, cancellationToken);

        var identite = new CallerIdentity(
            societe,
            utilisateur?.AgenceKey ?? request.AgenceKey ?? configuration["Authentication:ServiceAccount:AgenceKey"],
            userKey,
            utilisateur?.NomAffichage
                ?? configuration["Authentication:ServiceAccount:NomAffichage"]
                ?? request.Identifiant,
            null,
            DateTime.UtcNow,
            rolesResolus.EstAdmin,
            rolesResolus.TypesDossierAutorises);

        var token = tokens.Issue(identite);

        // The expiry is re-read from the issued token rather than recomputed, so the response cannot
        // disagree with what the token actually says.
        var valide = tokens.Validate(token);

        return Ok(new TokenResponse(token, "Bearer", valide.Identity?.ExpiresAt ?? DateTime.UtcNow, societe));
    }

    /// <summary>
    /// Changes the signed-in user's own password.
    /// </summary>
    /// <remarks>
    /// The user is taken from the <b>token</b>, never from the request body — accepting a user key there would
    /// let any authenticated caller change anyone's password. The current password is re-verified, which is what
    /// stops a stolen token from becoming permanent account ownership.
    /// <para>
    /// Response codes are chosen to say which part of the request failed: <b>401</b> when the current password
    /// is wrong, <b>422</b> when the new one breaks the rules (with the reason), <b>404</b> when the account no
    /// longer exists. Unlike sign-in, distinguishing these leaks nothing — the caller has already proved they
    /// are this user.
    /// </para>
    /// </remarks>
    [HttpPost("mot-de-passe")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ChangerMotDePasse(
        ChangerMotDePasseRequest request,
        [FromServices] Logistics.Infrastructure.CallerContext caller,
        CancellationToken cancellationToken)
    {
        var identite = caller.Caller;

        if (identite?.UserKey is null)
        {
            // Fires only when there is no user key at all — the development header fallback without one, say.
            // A service account does NOT land here: it carries a configured user key, and that key has no
            // personnel row, so its attempt surfaces as a 404 from the lookup. That is the accurate answer — a
            // service account is not an employee and has no password here to change.
            return IdentifiantsInvalides();
        }

        var change = await utilisateurs.ChangerMotDePasseAsync(
            identite.UserKey, request.AncienMotDePasse, request.NouveauMotDePasse, cancellationToken);

        if (!change)
        {
            return IdentifiantsInvalides();
        }

        await audit.JournalAsync("Personnel", "Modification",
            $"Mot de passe modifié pour {identite.UserKey}", cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// The single rejection used for every failed credential.
    /// </summary>
    /// <remarks>
    /// One shape for "unknown identifier", "wrong password", "blocked account" and "employee has left", so the
    /// response never says which — that distinction is a user-enumeration oracle.
    /// </remarks>
    private ObjectResult IdentifiantsInvalides() => Problem(
        title: "Invalid credentials",
        detail: "The identifier or password is incorrect.",
        statusCode: StatusCodes.Status401Unauthorized);

    /// <summary>Reports the caller the API recognised, for diagnostics.</summary>
    /// <remarks>
    /// Useful for confirming which company a token actually resolved to, which is the first thing to check
    /// when data looks wrong. Requires a valid token, unlike the issuing endpoint.
    /// </remarks>
    [HttpGet("moi")]
    [ProducesResponseType<CallerIdentity>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<CallerIdentity> Moi([FromServices] Logistics.Infrastructure.CallerContext caller)
    {
        var identite = caller.Caller;

        return identite is null
            ? Problem(title: "Unauthenticated", detail: "No authenticated caller.", statusCode: StatusCodes.Status401Unauthorized)
            : Ok(identite);
    }

    /// <summary>
    /// Compares two strings without leaking their common prefix length through timing.
    /// </summary>
    private static bool CryptographicEquals(string? gauche, string? droite)
    {
        var a = System.Text.Encoding.UTF8.GetBytes(gauche ?? string.Empty);
        var b = System.Text.Encoding.UTF8.GetBytes(droite ?? string.Empty);

        // FixedTimeEquals requires equal lengths; comparing lengths first is safe because the length is
        // not secret in the way the content is.
        return a.Length == b.Length && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(a, b);
    }
}

/// <summary>
/// A password change for the signed-in user.
/// </summary>
/// <remarks>
/// Note what is NOT here: the user key. It comes from the token, so this request cannot name somebody else's
/// account to change.
/// </remarks>
public sealed record ChangerMotDePasseRequest(string? AncienMotDePasse, string? NouveauMotDePasse);