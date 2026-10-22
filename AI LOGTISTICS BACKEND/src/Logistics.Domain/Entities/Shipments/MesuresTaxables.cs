namespace Logistics.Domain.Entities.Shipments;

/// <summary>
/// Derives the taxable (chargeable) weight and volume of a shipment.
/// </summary>
/// <remarks>
/// <para>
/// Transcribed from <c>frm_Saisie_Cotation.vb:2134-2168</c>, the only place the legacy application states
/// the rule. The taxable weight is the <b>greatest</b> of the actual weight, the volumetric weight and the
/// MPL weight — a consolidation is charged on whichever measure it consumes most of, which is why an
/// imported dossier with no taxable weight cannot be allocated against.
/// </para>
/// <para>
/// Because the rule is transcribed rather than invented, three of its quirks are reproduced deliberately
/// and would look like bugs to anyone reading them fresh:
/// </para>
/// <list type="bullet">
/// <item>
/// <b>Non-stackable volume takes precedence over real volume</b> whenever both are present (lines 2137-2141
/// and 2154-2158). It is not added to it, and real volume is not used as a floor.
/// </item>
/// <item>
/// The two results round differently — taxable weight to <b>2</b> decimals, taxable volume to <b>3</b>
/// (lines 2148 and 2168).
/// </item>
/// <item>
/// It applies <b>only to groupage</b>. A full-load shipment carries no taxable measures at all, so callers
/// must decide whether to invoke this rather than assuming a value.
/// </item>
/// </list>
/// <para>
/// The equivalence ratios are per-company, per-product settings held in <c>t_rapport_poids_volume</c>
/// (<c>volume_kgs</c>, <c>mpl_kgs</c>, <c>mpl_volume</c>).
/// </para>
/// </remarks>
public static class MesuresTaxables
{
    /// <summary>Decimal places the legacy applies to taxable weight.</summary>
    public const int DecimalesPoids = 2;

    /// <summary>Decimal places the legacy applies to taxable volume.</summary>
    public const int DecimalesVolume = 3;

    /// <summary>
    /// Computes the taxable weight and volume for a groupage shipment.
    /// </summary>
    /// <param name="poidsReel">Actual gross weight.</param>
    /// <param name="volumeReel">Actual volume.</param>
    /// <param name="volumeNonGerbable">
    /// Non-stackable volume. Takes precedence over <paramref name="volumeReel"/> when supplied, matching the
    /// legacy <c>If ... ElseIf</c> rather than summing the two.
    /// </param>
    /// <param name="mplReel">Actual MPL (minimum payable load).</param>
    /// <param name="equivalenceVolumeKgs">Volume-to-weight ratio; 0 or null disables that term.</param>
    /// <param name="equivalenceMplKgs">MPL-to-weight ratio; 0 or null disables that term.</param>
    /// <param name="equivalenceMplVolume">MPL-to-volume ratio; 0 or null disables that term.</param>
    public static (decimal PoidsTaxable, decimal VolumeTaxable) Calculer(
        decimal poidsReel,
        decimal? volumeReel,
        decimal? volumeNonGerbable,
        decimal? mplReel,
        decimal? equivalenceVolumeKgs,
        decimal? equivalenceMplKgs,
        decimal? equivalenceMplVolume)
    {
        var poids = poidsReel < 0 ? 0m : poidsReel;
        var mpl = mplReel is > 0 ? mplReel : null;

        // The volume both calculations use: non-stackable wins outright when present. Unwrapped to decimal
        // rather than left nullable, or every arithmetic expression downstream becomes decimal?.
        var volume = volumeNonGerbable is > 0
            ? volumeNonGerbable.Value
            : volumeReel is > 0
                ? volumeReel.Value
                : 0m;

        var ratioVolumePoids = equivalenceVolumeKgs is > 0 ? equivalenceVolumeKgs : null;
        var ratioMplPoids = equivalenceMplKgs is > 0 ? equivalenceMplKgs : null;
        var ratioMplVolume = equivalenceMplVolume is > 0 ? equivalenceMplVolume : null;

        // --- Taxable weight: the greatest of actual, volumetric and MPL weight.
        var poidsVolumetrique = volume * (ratioVolumePoids ?? 0m);
        var poidsMpl = mpl is { } valeurMpl ? valeurMpl * (ratioMplPoids ?? 0m) : 0m;

        var poidsTaxable = Math.Max(poids, Math.Max(poidsVolumetrique, poidsMpl));

        // --- Taxable volume: the greatest of the volume, the weight expressed as volume, and MPL volume.
        // The weight term is a DIVISION, so a zero or absent ratio must disable it — the legacy would raise a
        // division-by-zero, which is not a behaviour worth reproducing in an API.
        var volumeDuPoids = ratioVolumePoids is { } diviseur && diviseur != 0m ? poids / diviseur : 0m;
        var volumeMpl = mpl is { } valeurMplVolume ? valeurMplVolume * (ratioMplVolume ?? 0m) : 0m;

        var volumeTaxable = Math.Max(volume, Math.Max(volumeDuPoids, volumeMpl));

        return (
            Math.Round(poidsTaxable, DecimalesPoids, MidpointRounding.AwayFromZero),
            Math.Round(volumeTaxable, DecimalesVolume, MidpointRounding.AwayFromZero));
    }
}
