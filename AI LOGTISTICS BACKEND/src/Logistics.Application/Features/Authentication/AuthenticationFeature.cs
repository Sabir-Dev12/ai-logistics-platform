using System.Security.Cryptography;
using System.Text;
using Logistics.Application.Abstractions;

namespace Logistics.Application.Features.Authentication;

/// <summary>The authenticated caller, as carried by a token.</summary>
/// <param name="EstAdmin">
/// Whether the caller may change <b>parameterisation</b> — reference data and pricing configuration.
/// </param>
/// <remarks>
/// Deliberately the legacy <c>t_personnel.admin</c> flag rather than a new role model. In the desktop
/// application that flag gated parameterisation screens, not day-to-day operations, so it is applied here
/// to the same surface and <b>not</b> to ordinary business mutations such as cancelling a fuel issue or
/// unlinking a dossier.
/// </remarks>
public sealed record CallerIdentity(
    string SocieteKey,
    string? AgenceKey,
    string? UserKey,
    string? UserDisplayName,
    string? Workstation,
    DateTime ExpiresAt,
    bool EstAdmin = false,
    IReadOnlyList<string>? TypesDossierAutorises = null);

/// <summary>Why a token was rejected.</summary>
public enum TokenRejection
{
    None,
    Malformed,
    BadSignature,
    Expired
}

/// <summary>Outcome of validating a token.</summary>
public sealed record TokenValidationResult(TokenRejection Rejection, CallerIdentity? Identity)
{
    public bool IsValid => Rejection == TokenRejection.None && Identity is not null;
}

/// <summary>
/// Issues and validates the API's bearer tokens.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is a deliberately small custom scheme, not JWT.</b> The deployment environment has no network
/// access and <c>Microsoft.AspNetCore.Authentication.JwtBearer</c> is not in the package cache, so a
/// standards-based OIDC/JWT provider could not be wired up here. The format is therefore explicit and
/// signed with HMAC-SHA256 from the base class library, and it should be replaced by a real identity
/// provider before production — the seam is <see cref="ILogisticsTokenService"/> plus the ASP.NET handler,
/// so the swap does not touch the domain.
/// </para>
/// <para>
/// The payload is a delimited string rather than JSON so that validation has no serializer dependency and
/// cannot be confused by a payload that merely looks like one. The pipe delimiter is rejected inside field
/// values, which is what makes the format unambiguous.
/// </para>
/// </remarks>
public interface ILogisticsTokenService
{
    /// <summary>Issues a token for an already-authenticated caller.</summary>
    string Issue(CallerIdentity identity);

    /// <summary>
    /// Validates a token's structure, signature and expiry.
    /// </summary>
    /// <remarks>
    /// The signature is compared in <b>constant time</b>: a byte-by-byte comparison that returns early
    /// leaks how much of a forged signature was correct, which is enough to reconstruct a valid one.
    /// </remarks>
    TokenValidationResult Validate(string? token);
}

/// <summary>Configuration for token signing.</summary>
public sealed record TokenOptions
{
    /// <summary>
    /// The HMAC signing key. Must be supplied by configuration; there is deliberately no default, because
    /// a built-in fallback key would be a published key.
    /// </summary>
    public string? SigningKey { get; init; }

    public string Issuer { get; init; } = "Logistics.Api";

    public int TokenLifetimeMinutes { get; init; } = 480;

    /// <summary>
    /// Whether unauthenticated requests may fall back to headers.
    /// </summary>
    /// <remarks>
    /// Development only, and off unless explicitly enabled. In any other environment a request without a
    /// valid token is refused, because the header fallback is exactly the hole this replaces.
    /// </remarks>
    public bool AllowHeaderFallback { get; init; }

    /// <summary>
    /// Checks the configuration is usable, so a misconfiguration fails at startup rather than per request.
    /// </summary>
    /// <exception cref="InvalidOperationException">The signing key or lifetime is unusable.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SigningKey))
        {
            throw new InvalidOperationException(
                "Authentication:SigningKey is not configured. Tokens cannot be issued or validated. "
                + "Supply it from a secret store or the Authentication__SigningKey environment variable — "
                + "there is deliberately no default, because a built-in key would be a published key.");
        }

        if (TokenLifetimeMinutes <= 0)
        {
            throw new InvalidOperationException(
                $"Authentication:TokenLifetimeMinutes must be positive (got {TokenLifetimeMinutes}).");
        }
    }
}

public sealed class LogisticsTokenService(TokenOptions options) : ILogisticsTokenService
{
    private const string Prefix = "v1";
    private const char Separator = '|';

    private static readonly DateTime Epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public string Issue(CallerIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        // The configured lifetime is honoured as-is rather than substituted. Silently replacing a
        // non-positive value would turn a misconfiguration into a working-but-wrong lifetime — an operator
        // setting 0 expecting immediate expiry would get the default instead. Startup validation rejects a
        // non-positive value; see TokenOptions.Validate.
        var expiration = DateTime.UtcNow.AddMinutes(options.TokenLifetimeMinutes);

        var payload = string.Join(Separator, new[]
        {
            Prefix,
            Champ(identity.SocieteKey),
            Champ(identity.AgenceKey),
            Champ(identity.UserKey),
            Champ(identity.UserDisplayName),
            Champ(identity.Workstation),
            ((long)(expiration - Epoch).TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture),
            // Inside the signed payload, so the flag cannot be raised without the key.
            identity.EstAdmin ? "1" : "0",
            // Comma-joined: the field delimiter is '|', so commas are unambiguous here. An empty string
            // means no scope rows, which resolves to unrestricted.
            Champ(string.Join(',', identity.TypesDossierAutorises ?? []))
        });

        var signature = Signer(payload);

        return $"{Base64Url(Encoding.UTF8.GetBytes(payload))}.{Base64Url(signature)}";
    }

    public TokenValidationResult Validate(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new TokenValidationResult(TokenRejection.Malformed, null);
        }

        var parties = token.Split('.');
        if (parties.Length != 2)
        {
            return new TokenValidationResult(TokenRejection.Malformed, null);
        }

        byte[] payloadBytes;
        byte[] signatureFournie;

        try
        {
            payloadBytes = FromBase64Url(parties[0]);
            signatureFournie = FromBase64Url(parties[1]);
        }
        catch (FormatException)
        {
            return new TokenValidationResult(TokenRejection.Malformed, null);
        }

        var signatureAttendue = Signer(Encoding.UTF8.GetString(payloadBytes));

        // Constant-time: an early-exit comparison leaks how much of a forgery was correct.
        if (!CryptographicOperations.FixedTimeEquals(signatureAttendue, signatureFournie))
        {
            return new TokenValidationResult(TokenRejection.BadSignature, null);
        }

        var champs = Encoding.UTF8.GetString(payloadBytes).Split(Separator);

        // Seven fields is the pre-role layout, eight added the admin flag, nine adds the dossier scopes.
        // All are accepted so tokens issued moments before a deploy do not all fail at once — and an older
        // token simply carries no roles, which is the safe direction to fail.
        if (champs.Length is < 7 or > 9 || champs[0] != Prefix)
        {
            return new TokenValidationResult(TokenRejection.Malformed, null);
        }

        if (!long.TryParse(champs[6], System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var secondes))
        {
            return new TokenValidationResult(TokenRejection.Malformed, null);
        }

        var expiration = Epoch.AddSeconds(secondes);

        // Expiry is checked AFTER the signature: an unsigned payload must not be trusted even to say it
        // expired.
        if (expiration <= DateTime.UtcNow)
        {
            return new TokenValidationResult(TokenRejection.Expired, null);
        }

        var estAdmin = champs.Length >= 8 && champs[7] == "1";

        var typesDossier = champs.Length == 9
            ? champs[8].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];

        var identity = new CallerIdentity(
            champs[1], NullIfEmpty(champs[2]), NullIfEmpty(champs[3]), NullIfEmpty(champs[4]),
            NullIfEmpty(champs[5]), expiration, estAdmin, typesDossier);

        // A token without a company is not usable: every query is tenant-scoped.
        if (string.IsNullOrWhiteSpace(identity.SocieteKey))
        {
            return new TokenValidationResult(TokenRejection.Malformed, null);
        }

        return new TokenValidationResult(TokenRejection.None, identity);
    }

    private byte[] Signer(string payload)
    {
        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            // Refusing is the point: a built-in default key would be a published key.
            throw new InvalidOperationException(
                "Authentication:SigningKey is not configured. Refusing to sign or validate tokens.");
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(options.SigningKey));
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
    }

    /// <summary>Rejects the delimiter inside a value, which is what keeps the format unambiguous.</summary>
    private static string Champ(string? valeur)
    {
        if (string.IsNullOrWhiteSpace(valeur))
        {
            return string.Empty;
        }

        var propre = valeur.Trim();

        return propre.Contains(Separator)
            ? throw new ArgumentException($"A token field may not contain '{Separator}'.", nameof(valeur))
            : propre;
    }

    private static string? NullIfEmpty(string? valeur) => string.IsNullOrWhiteSpace(valeur) ? null : valeur;

    private static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = (padded.Length % 4) switch
        {
            2 => padded + "==",
            3 => padded + "=",
            0 => padded,
            _ => throw new FormatException("Invalid base64url length.")
        };

        return Convert.FromBase64String(padded);
    }
}
