using Logistics.Domain.Entities.ReferenceData;

namespace Logistics.Application.Features.ReferenceData;

/// <summary>
/// Reads the company settings the API's own behaviour depends on.
/// </summary>
/// <remarks>
/// <para>
/// Read-only on purpose. The desktop application remains the authority for these values, and an API write
/// would be invisible to it until the next restart because the legacy code caches 20 of them at login — a
/// divergence nobody would notice until behaviour differed between the two front ends.
/// </para>
/// <para>
/// Every method takes the setting code rather than being one method per setting, because there are 212 codes
/// and only a handful matter here; see <see cref="SetCodes"/> for the ones named.
/// </para>
/// </remarks>
public interface IParametreSocieteProvider
{
    /// <summary>
    /// Reads a setting the legacy code compares with <c>= "Oui"</c>.
    /// </summary>
    /// <remarks>An absent row, or any value other than <c>Oui</c>, yields <c>false</c>.</remarks>
    Task<bool> EstOuiAsync(string keyParametre, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a setting the legacy code compares with <c>&lt;&gt; "Non"</c>.
    /// </summary>
    /// <remarks>
    /// <b>An absent row yields true.</b> This is the legacy behaviour and the reason the two readings exist
    /// separately: a company that never configured <c>param_oblig_port</c> requires a port, because the test
    /// asks whether the value is specifically <c>Non</c>. Collapsing this into
    /// <see cref="EstOuiAsync"/> would silently relax a required field for every unconfigured company.
    /// </remarks>
    Task<bool> ObligatoireSaufNonAsync(string keyParametre, CancellationToken cancellationToken = default);

    /// <summary>Reads the raw text, or null when the row is absent.</summary>
    Task<string?> TexteAsync(string keyParametre, CancellationToken cancellationToken = default);

    /// <summary>Reads a numeric setting, or null when absent or unparseable.</summary>
    Task<decimal?> NombreAsync(string keyParametre, CancellationToken cancellationToken = default);

    /// <summary>Lists the settings held for the current company.</summary>
    Task<IReadOnlyList<ParametreSociete>> ListerAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolves a company's weight/volume equivalence ratios.
/// </summary>
/// <remarks>
/// Separate from <see cref="IParametreSocieteProvider"/> because these live in their own table keyed by
/// product rather than as key/value settings, and because they are consumed as numbers rather than as text.
/// </remarks>
public interface IRapportPoidsVolumeProvider
{
    /// <summary>
    /// The ratios for a product, or null when the company holds no row for it.
    /// </summary>
    /// <remarks>
    /// A missing row is not an error: the legacy applies no ratios at all in that case, so only actual weight
    /// and volume are chargeable. Callers must handle null rather than treating zero ratios as equivalent to
    /// absent ones — zeros would additionally disable the weight-to-volume term.
    /// </remarks>
    Task<RapportPoidsVolume?> PourProduitAsync(string produit, CancellationToken cancellationToken = default);
}
