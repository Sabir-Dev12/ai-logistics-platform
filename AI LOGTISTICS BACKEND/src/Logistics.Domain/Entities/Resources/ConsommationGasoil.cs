using Logistics.Domain.Common;
using Logistics.Domain.Entities.ReferenceData;

namespace Logistics.Domain.Entities.Resources;

/// <summary>
/// The consolidated fuel-movement journal (<c>t_consommation_gasoil</c>).
/// </summary>
/// <remarks>
/// <para>
/// Every fuel movement — an issue to a vehicle, a voucher to a fellow company, an EDI-delivered purchase —
/// writes a row here <em>in addition to</em> its own operational document. The two are a pair, not a
/// duplicate: this is the flat ledger every consumption report reads, while <c>t_sortie_gasoil</c> and
/// <c>t_bon_gasoil</c> are the documents a user opens.
/// </para>
/// <para>
/// <b>Both rows of a pair carry the same key.</b> The legacy issues one
/// <c>IncrementationKeyApp("GASOIL")</c> and writes it as <c>key_sortie</c> and <c>key_consommation</c>
/// (<c>frm_Saisie_Sortie_Gasoil.vb:319-323</c>), so the pair can be rejoined later. That is only possible
/// because the whole fuel family shares one sequence — see <see cref="SequenceGasoil"/>.
/// </para>
/// <para>
/// <b>Tenant column is <c>societe_consommation</c></b>, a fourth distinct spelling in this module alongside
/// <c>societe</c> (<c>t_citernes</c>, <c>t_approvisionnement_citerne</c>), <c>societe_sortie</c>
/// (<c>t_sortie_gasoil</c>) and <c>key_soc</c>.
/// </para>
/// </remarks>
public sealed class ConsommationGasoil : Entity, ITenantScoped
{
    /// <summary>Discriminator written to <c>type_sortie</c> by each writer.</summary>
    /// <remarks>
    /// The literals are the legacy's, accents and all, and are what the reports group by. Several carry
    /// misspellings that must be preserved: <c>Confrére</c> with an acute accent on the first vowel only.
    /// </remarks>
    public static class Types
    {
        /// <summary>An issue from a tank to a vehicle.</summary>
        public const string SortieGasoil = "Sortie Gasoil";

        /// <summary>An issue to an outside haulier.</summary>
        public const string SortieTransporteur = "Sortie Gasoil Transporteur";

        /// <summary>An issue to a fellow company. Note the legacy spelling.</summary>
        public const string SortieConfrere = "Sortie Gasoil Confrére";

        /// <summary>A fuel voucher.</summary>
        public const string BonGasoil = "Bon Gasoil";

        /// <summary>A voucher for an outside haulier.</summary>
        public const string BonTransporteur = "Bon Gasoil Transporteur";

        /// <summary>A voucher for a fellow company.</summary>
        public const string BonConfrere = "Bon Gasoil Confrère";
    }

    /// <summary>Fuel. The other value is <c>AdBlue</c>.</summary>
    public const string TypeCiterneGasoil = "Gasoil";

    private ConsommationGasoil() { }

    public ConsommationGasoil(
        string keyConsommation,
        string societeKey,
        string typeSortie,
        DateTime dateConsommation,
        string? keyCiterne,
        decimal qteConsommation,
        decimal montantConsommation,
        decimal prixUnitaire,
        string typeCiterne = TypeCiterneGasoil)
    {
        Id = string.IsNullOrWhiteSpace(keyConsommation)
            ? throw new ArgumentException("Value is required.", nameof(keyConsommation))
            : keyConsommation.Trim();

        SocieteKey = string.IsNullOrWhiteSpace(societeKey)
            ? throw new ArgumentException("Value is required.", nameof(societeKey))
            : societeKey.Trim();

        TypeSortie = string.IsNullOrWhiteSpace(typeSortie)
            ? throw new ArgumentException("Value is required.", nameof(typeSortie))
            : typeSortie.Trim();

        // Nullable on purpose: a voucher is fuel bought from a supplier, so there is no tank to name. The
        // legacy's voucher insert omits key_citerne entirely (frm_Saisie_Bon_Gasoil.vb:254), whereas the
        // issue insert supplies it. Requiring it here would have made the second writer impossible.
        KeyCiterne = Trim(keyCiterne);

        DateConsommation = dateConsommation;
        QteConsommation = qteConsommation;
        MontantConsommation = montantConsommation;
        PrixUnitaire = prixUnitaire;
        TypeCiterne = typeCiterne;
    }

    /// <summary>Tenant. Column is <c>societe_consommation</c>.</summary>
    public string SocieteKey { get; private set; } = string.Empty;

    /// <summary>Which writer produced the row; see <see cref="Types"/>.</summary>
    public string TypeSortie { get; private set; } = string.Empty;

    public DateTime DateConsommation { get; private set; }

    /// <summary>
    /// The tank the fuel came out of, when it came out of one.
    /// </summary>
    /// <remarks>
    /// <b>Null for a voucher.</b> A voucher records fuel bought from a supplier, so no tank is involved; the
    /// legacy's voucher insert omits the column entirely. Only the issue paths name a tank.
    /// </remarks>
    public string? KeyCiterne { get; private set; }

    /// <summary>Vehicle the fuel went to, when the movement is attributable to one.</summary>
    public string? KeyMateriel { get; private set; }

    public string? KeyChauffeur { get; private set; }

    /// <summary>Journey the fuel is charged to, when the movement is attributed to one.</summary>
    public string? KeyVoyage { get; private set; }

    /// <summary>Outside haulier, for the transporteur variants.</summary>
    public string? KeyTransporteur { get; private set; }

    /// <summary>Supplier, for purchase-side rows.</summary>
    public string? KeyFournisseur { get; private set; }

    /// <summary>Voucher number, for the <c>Bon Gasoil</c> variants.</summary>
    public string? NumBon { get; private set; }

    /// <summary>Physical counter reading at the time of the movement.</summary>
    public int? CompteurConsommation { get; private set; }

    public decimal QteConsommation { get; private set; }

    public decimal MontantConsommation { get; private set; }

    /// <summary>Currency of <see cref="MontantConsommation"/>. Only the EDI purchase rows set it.</summary>
    public string? DeviseConsommation { get; private set; }

    /// <summary>Unit price snapshot, so a later tank revaluation cannot restate this movement.</summary>
    public decimal PrixUnitaire { get; private set; }

    /// <summary>Whether the tank was filled.</summary>
    public bool Plein { get; private set; }

    /// <summary><c>Gasoil</c> or <c>AdBlue</c>.</summary>
    public string TypeCiterne { get; private set; } = TypeCiterneGasoil;

    public string? Matricule { get; private set; }

    public string? Observation { get; private set; }

    /// <summary>Set once a cost-per-kilometre ratio has been computed from this movement.</summary>
    public bool RatioCalcule { get; private set; }

    public bool Supprime { get; private set; }

    /// <param name="compteur">
    /// Counter reading. Taken as a decimal and truncated to an integer because the legacy wraps its own
    /// value in <c>CInt(...)</c> before writing it — the column is an integer, and a caller holding a
    /// decimal reading should not have to know that.
    /// </param>
    public void SetAffectation(
        string? keyVoyage, string? keyMateriel, string? keyChauffeur,
        string? matricule, decimal? compteur, bool plein)
    {
        KeyVoyage = Trim(keyVoyage);
        KeyMateriel = Trim(keyMateriel);
        KeyChauffeur = Trim(keyChauffeur);
        Matricule = Trim(matricule);
        CompteurConsommation = compteur.HasValue ? (int)compteur.Value : null;
        Plein = plein;
    }

    public void SetPrixUnitaire(decimal prixUnitaire)
    {
        PrixUnitaire = prixUnitaire;
        // The amount is the quantity valued at the movement's own price, kept consistent here rather than at
        // each call site so the ledger cannot disagree with itself.
        MontantConsommation = Math.Round(QteConsommation * prixUnitaire, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Sets the purchase-side fields, which only the voucher writers supply.
    /// </summary>
    /// <remarks>
    /// The journal is the only place a voucher's supplier, currency and document number appear together with
    /// its quantity — which is what lets the consumption reports total purchases and issues in one pass.
    /// </remarks>
    public void SetAchat(string? keyFournisseur, string? deviseConsommation, string? numBon)
    {
        KeyFournisseur = Trim(keyFournisseur);
        DeviseConsommation = Trim(deviseConsommation);
        NumBon = Trim(numBon);
    }

    /// <summary>
    /// Soft-deletes the ledger row alongside its operational document.
    /// </summary>
    /// <remarks>
    /// The journal is never physically removed — the legacy's list screens set <c>supprimer=1</c> — because a
    /// cancelled movement still has to appear in the history of what was issued.
    /// </remarks>
    public void SupprimerLogiquement() => Supprime = true;

    /// <summary>
    /// Marks this movement as counted in a cost-per-kilometre measurement.
    /// </summary>
    /// <remarks>
    /// The flag is what stops a movement being counted in two windows. Note the closing fill-up of a window is
    /// deliberately left unmarked so it can open the next one — see
    /// <see cref="RatioCoutKm.DansLeMarquage"/>.
    /// </remarks>
    public void MarquerRatioCalcule() => RatioCalcule = true;

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
