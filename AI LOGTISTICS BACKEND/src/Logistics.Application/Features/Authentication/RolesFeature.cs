namespace Logistics.Application.Features.Authentication;

/// <summary>
/// What a caller is allowed to do beyond being authenticated.
/// </summary>
/// <param name="EstAdmin">
/// May change parameterisation — reference data and pricing configuration.
/// </param>
/// <param name="TypesDossierAutorises">
/// The dossier types this caller may handle. <b>Null or empty means unrestricted</b> — see the remarks on
/// <see cref="AutoriseTypeDossier"/>, and the note on the empty-set ambiguity in the README.
/// </param>
/// <remarks>
/// Modelled on the legacy <c>t_personnel.admin</c> flag, which in the desktop application gated the
/// parameterisation screens rather than day-to-day operations. Ordinary business mutations (cancelling a
/// fuel issue, unlinking a dossier, soft-deleting a booking) are therefore <b>not</b> admin-gated, because
/// widening the flag's meaning would lock operators out of their own work.
/// </remarks>
public sealed record CallerRoles(bool EstAdmin, IReadOnlyList<string>? TypesDossierAutorises = null)
{
    /// <summary>The safe default: authenticated but not privileged.</summary>
    public static readonly CallerRoles Aucun = new(false);

    /// <summary>
    /// Whether this caller may handle a dossier of the given type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Administrators are unrestricted — configuring the scopes is part of parameterisation.
    /// </para>
    /// <para>
    /// <b>An empty scope list is treated as unrestricted, deliberately and visibly.</b> The extraction
    /// established the shape of <c>t_personnel_type_dossier</c> but not what "no rows" means: it could be
    /// "unrestricted" or "nothing". The two errors are not symmetric — a false restriction locks every
    /// operator out the moment the API is deployed, while a false permission is a correctable gap within a
    /// single company. So the permissive reading is taken, and token issuance logs which reading applied
    /// rather than leaving it silent.
    /// </para>
    /// </remarks>
    public bool AutoriseTypeDossier(string? typeDossier)
    {
        if (EstAdmin)
        {
            return true;
        }

        // Nothing to check: the caller did not name a dossier type.
        if (string.IsNullOrWhiteSpace(typeDossier))
        {
            return true;
        }

        if (TypesDossierAutorises is null || TypesDossierAutorises.Count == 0)
        {
            return true;
        }

        return TypesDossierAutorises.Contains(typeDossier.Trim(), StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Resolves a caller's roles from the application's own data.
/// </summary>
/// <remarks>
/// Kept behind an interface because the answer comes from <c>t_personnel</c>, which the authentication
/// layer must not reach into directly — and because it lets the token endpoint be exercised without a
/// database.
/// </remarks>
public interface IUserRoleResolver
{
    /// <summary>
    /// Resolves the roles for a user key, defaulting to <see cref="CallerRoles.Aucun"/>.
    /// </summary>
    /// <remarks>
    /// <b>Fails closed.</b> An unknown user, or a repository that cannot answer, yields no privileges —
    /// never a privileged default. The cost of being wrong that way is an administrator who cannot change
    /// settings until the database is reachable; the cost of the opposite is anyone at all becoming an
    /// administrator whenever a lookup fails.
    /// </remarks>
    Task<CallerRoles> ResolveAsync(string? userKey, CancellationToken cancellationToken = default);
}

/// <summary>
/// The current caller's roles, as the application layer sees them.
/// </summary>
/// <remarks>
/// Populated from the authenticated identity, never from the request body or headers — so a service can
/// make an authorisation decision without trusting anything the caller supplied.
/// </remarks>
public interface ICallerRolesAccessor
{
    CallerRoles Roles { get; }
}
