namespace Logistics.Application.Features.Authentication;

/// <summary>
/// A user authenticated against the application's own personnel table.
/// </summary>
/// <param name="KeyPersonnel">The <c>key_personnel</c>, which becomes the token's user key.</param>
/// <param name="SocieteKey">The company the employee belongs to — the tenant the token will carry.</param>
/// <param name="PremiereConnexion">
/// Whether the legacy flag <c>premiere_connexion</c> is set, meaning the password has never been changed by
/// the user. Surfaced rather than acted on: see the remarks on <see cref="IUtilisateurAuthenticator"/>.
/// </param>
public sealed record UtilisateurLegacy(
    string KeyPersonnel,
    string SocieteKey,
    string? AgenceKey,
    string? NomAffichage,
    bool Admin,
    bool PremiereConnexion);

/// <summary>
/// Authenticates a real user against <c>t_personnel</c>, the way the desktop application does.
/// </summary>
/// <remarks>
/// <para>
/// Before this existed the API could only authenticate a single configured service account, so no employee
/// could sign in — the largest functional gap left by the migration. The password check reproduces the
/// legacy encoding exactly, because that is what the stored values are; see
/// <c>MotDePasseLegacy</c>, which also records why that encoding must not be used for anything new.
/// </para>
/// <para>
/// <b>A first connection is allowed through rather than blocked.</b> The legacy sets
/// <c>premiere_connexion</c> and then prompts for a change; there is no change-password endpoint here yet, so
/// refusing would lock out every newly created employee with no way forward. The flag is reported so a caller
/// can prompt, and the missing flow is recorded as a gap rather than silently enforced by a refusal.
/// </para>
/// </remarks>
public interface IUtilisateurAuthenticator
{
    /// <summary>
    /// Verifies a user's credentials within a company.
    /// </summary>
    /// <returns>
    /// The user, or <c>null</c> when the identifier is unknown, the password is wrong, or the account is
    /// blocked or closed. <b>The three are deliberately indistinguishable</b> — telling a caller that an
    /// identifier exists but the password is wrong is a user-enumeration oracle.
    /// </returns>
    Task<UtilisateurLegacy?> AuthentifierAsync(
        string societeKey, string? identifiant, string? motDePasse, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes a user's own password.
    /// </summary>
    /// <param name="keyPersonnel">The signed-in user, from their token — never from the request body.</param>
    /// <param name="ancienMotDePasse">Their current password, re-verified before anything is written.</param>
    /// <param name="nouveauMotDePasse">The replacement.</param>
    /// <returns>
    /// <c>true</c> on success; <c>false</c> when the current password is wrong or the new one is unusable.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Re-verifying the current password is the point of the operation, not a formality: it is what stops a
    /// stolen token from being escalated into permanent account ownership.
    /// </para>
    /// <para>
    /// <b>The key comes from the token, not the request.</b> Accepting a user key in the body would let any
    /// authenticated caller change anyone's password.
    /// </para>
    /// </remarks>
    Task<bool> ChangerMotDePasseAsync(
        string keyPersonnel, string? ancienMotDePasse, string? nouveauMotDePasse,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Rules for a new password.
/// </summary>
/// <remarks>
/// <b>An addition, not legacy behaviour.</b> The desktop application imposes no minimum, and this does not
/// change what it accepts — a password set through this API is still just a string it can encode, so the two
/// front ends stay interoperable. The rule applies only when a password is <i>set</i>, so nobody is locked out
/// of an account whose existing password is short.
/// <para>
/// A minimum length is a weak remedy for a scheme this weak, but it is nearly free and removes the worst
/// inputs. The real fix is replacing <c>MotDePasseLegacy</c> with a salted algorithm, which is recorded as a
/// migration rather than pretended away by a length check.
/// </para>
/// </remarks>
public static class ReglesMotDePasse
{
    /// <summary>Shortest password this API will set.</summary>
    public const int LongueurMinimale = 8;

    /// <summary>Why a candidate was refused, or null when it is acceptable.</summary>
    public static string? MotifDeRefus(string? motDePasse)
    {
        if (string.IsNullOrWhiteSpace(motDePasse))
        {
            return "Le nouveau mot de passe est obligatoire.";
        }

        if (motDePasse.Length < LongueurMinimale)
        {
            return $"Le nouveau mot de passe doit comporter au moins {LongueurMinimale} caractères.";
        }

        if (motDePasse.Length > 200)
        {
            // Not arbitrary: the encoding multiplies by n! and the column is bounded, so an unbounded length is
            // a way to store garbage nobody can authenticate with.
            return "Le nouveau mot de passe est trop long (200 caractères au maximum).";
        }

        return null;
    }
}
