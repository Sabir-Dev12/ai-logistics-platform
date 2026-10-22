using Logistics.Domain.Common;

namespace Logistics.Domain.Entities.Shipments;

/// <summary>
/// A COD (cash-on-delivery) term attached to a dossier (<c>T_COD_DOSSIER</c>).
/// </summary>
/// <remarks>
/// <para>
/// Written by the EDI import only when the file carries a COD
/// (<c>frm_Integration_EDI_IMPORT_STC.vb:1106-1107</c>), and its key comes from the <b><c>"COD"</c></b>
/// sequence — a separate sequence from the fuel family's shared <c>"GASOIL"</c>.
/// </para>
/// <para>
/// The legacy's own test is that the raw field is non-empty, and the code stored is the value resolved
/// against <c>t_cod</c> rather than the two-character EDI code, so <c>"10"</c> — the file's "no COD" marker —
/// never reaches here.
/// </para>
/// </remarks>
public sealed class CodDossier : Entity
{
    private CodDossier() { }

    public CodDossier(string keyLigneCod, string keyDossier, string cod)
    {
        Id = string.IsNullOrWhiteSpace(keyLigneCod)
            ? throw new ArgumentException("Value is required.", nameof(keyLigneCod))
            : keyLigneCod.Trim();

        KeyDossier = string.IsNullOrWhiteSpace(keyDossier)
            ? throw new ArgumentException("Value is required.", nameof(keyDossier))
            : keyDossier.Trim();

        Cod = string.IsNullOrWhiteSpace(cod)
            ? throw new ArgumentException("Value is required.", nameof(cod))
            : cod.Trim();
    }

    public string KeyDossier { get; private set; } = string.Empty;

    /// <summary>COD code, matching <c>t_cod.intitule_cod</c>.</summary>
    public string Cod { get; private set; } = string.Empty;
}

/// <summary>
/// A supplier invoice referenced by a dossier (<c>T_FACTURE_FOURNISSEUR_DOSSIER</c>).
/// </summary>
/// <remarks>
/// <para>
/// The EDI file carries up to <b>five</b> invoice blocks, each with a number, a date and an amount, and each
/// written only when its number is non-empty (<c>frm_Integration_EDI_IMPORT_STC.vb:1109-1123</c>). They are
/// distinct from the dossier's own sales invoice: these are what the supplier billed.
/// </para>
/// <para>
/// <b>No key column is inserted.</b> The legacy column list is exactly
/// <c>(key_dossier, num_facture, date_facture, montant_facture, devise_facture, intitule_fournisseur)</c>, so
/// the logical key is the pair and the mapping declares that rather than inventing a surrogate.
/// </para>
/// <para>
/// The legacy writes <c>intitule_fournisseur</c> as a <b>single space</b> — there is no supplier name
/// available on this path, and it substitutes a placeholder rather than null. That is reproduced because a
/// later reader may be depending on non-null, but it is recorded here as the legacy's quirk rather than a
/// decision.
/// </para>
/// </remarks>
public sealed class FactureFournisseurDossier : Entity
{
    /// <summary>The placeholder the legacy stores when no supplier name is known.</summary>
    public const string IntituleParDefaut = " ";

    private FactureFournisseurDossier() { }

    public FactureFournisseurDossier(
        string keyDossier, string numFacture, DateTime? dateFacture,
        decimal? montantFacture, string? deviseFacture, string? intituleFournisseur = null)
    {
        KeyDossier = string.IsNullOrWhiteSpace(keyDossier)
            ? throw new ArgumentException("Value is required.", nameof(keyDossier))
            : keyDossier.Trim();

        NumFacture = string.IsNullOrWhiteSpace(numFacture)
            ? throw new ArgumentException("Value is required.", nameof(numFacture))
            : numFacture.Trim();

        DateFacture = dateFacture;
        MontantFacture = montantFacture;
        DeviseFacture = string.IsNullOrWhiteSpace(deviseFacture) ? null : deviseFacture.Trim();
        IntituleFournisseur = string.IsNullOrWhiteSpace(intituleFournisseur)
            ? IntituleParDefaut
            : intituleFournisseur.Trim();

        Id = $"{KeyDossier}:{NumFacture}";
    }

    public string KeyDossier { get; private set; } = string.Empty;

    public string NumFacture { get; private set; } = string.Empty;

    public DateTime? DateFacture { get; private set; }

    public decimal? MontantFacture { get; private set; }

    public string? DeviseFacture { get; private set; }

    public string IntituleFournisseur { get; private set; } = IntituleParDefaut;
}
