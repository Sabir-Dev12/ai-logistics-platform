using Logistics.Domain.Common;

namespace Logistics.Domain.Entities.ReferenceData;

/// <summary>
/// Weight/volume equivalence ratios per product (<c>t_rapport_poids_volume</c>).
/// </summary>
/// <remarks>
/// <para>
/// These are the divisors and multipliers behind a groupage shipment's taxable (chargeable) measures: one
/// cubic metre is worth <see cref="VolumeKgs"/> kilograms, one MPL is worth <see cref="MplKgs"/> kilograms
/// and <see cref="MplVolume"/> cubic metres. Without a row for a product, every ratio is zero and only the
/// actual weight and volume are chargeable.
/// </para>
/// <para>
/// <b>Tenant column is <c>soc_rapport</c></b> — the third distinct spelling in this area of the schema
/// (<c>soc_rapport</c> here, <c>societe</c> on the tank tables, <c>soc_fournisseur</c> on suppliers), so it
/// must not be assumed uniform.
/// </para>
/// <para>
/// The key is <c>(soc_rapport, produit)</c>. The EDI import path looks up <c>produit = 'ROUTIER'</c>
/// <b>hard-coded</b> (<c>frm_Integration_EDI_IMPORT_STC.vb:927</c>), which is why that literal appears in the
/// promotion code rather than being derived from the file.
/// </para>
/// </remarks>
public sealed class RapportPoidsVolume : Entity, ITenantScoped
{
    /// <summary>
    /// The product the EDI import path always resolves its ratios from.
    /// </summary>
    /// <remarks>
    /// The legacy literal, not a default this backend chose. An inbound STC file cannot express which ratio
    /// row applies, so the import fixed on road transport.
    /// </remarks>
    public const string ProduitRoutier = "ROUTIER";

    private RapportPoidsVolume() { }

    public RapportPoidsVolume(
        string societeKey, string produit,
        decimal volumeKgs, decimal mplKgs, decimal mplVolume)
    {
        SocieteKey = string.IsNullOrWhiteSpace(societeKey)
            ? throw new ArgumentException("Value is required.", nameof(societeKey))
            : societeKey.Trim();

        Produit = string.IsNullOrWhiteSpace(produit)
            ? throw new ArgumentException("Value is required.", nameof(produit))
            : produit.Trim();

        VolumeKgs = volumeKgs;
        MplKgs = mplKgs;
        MplVolume = mplVolume;

        Id = $"{societeKey}:{produit}";
    }

    /// <summary>Tenant. Column is <c>soc_rapport</c>.</summary>
    public string SocieteKey { get; private set; } = string.Empty;

    /// <summary>Product code; the EDI import always uses <see cref="ProduitRoutier"/>.</summary>
    public string Produit { get; private set; } = string.Empty;

    /// <summary>Kilograms per cubic metre — the volume-to-weight equivalence.</summary>
    public decimal VolumeKgs { get; private set; }

    /// <summary>Kilograms per MPL.</summary>
    public decimal MplKgs { get; private set; }

    /// <summary>Cubic metres per MPL.</summary>
    public decimal MplVolume { get; private set; }

    public void Definir(decimal volumeKgs, decimal mplKgs, decimal mplVolume)
    {
        VolumeKgs = volumeKgs;
        MplKgs = mplKgs;
        MplVolume = mplVolume;
    }
}
