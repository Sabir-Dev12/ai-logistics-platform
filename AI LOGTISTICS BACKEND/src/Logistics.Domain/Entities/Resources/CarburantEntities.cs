using Logistics.Domain.Common;
using Logistics.Domain.Entities.ReferenceData;

namespace Logistics.Domain.Entities.Resources;

/// <summary>
/// The key sequence the whole fuel family draws from.
/// </summary>
/// <remarks>
/// The legacy application issues <b>one</b> sequence for <c>t_citernes</c>,
/// <c>t_approvisionnement_citerne</c>, <c>t_sortie_gasoil</c> and <c>t_consommation_gasoil</c> — every one of
/// them calls <c>tools.IncrementationKeyApp("GASOIL")</c> — so a key is unique across the family rather than
/// within a table.
/// <para>
/// This backend had split it into three per-table sequences (<c>CITERNE</c>, <c>SORTIE_GASOIL</c>,
/// <c>JAUGE_GASOIL</c>), which would issue different keys for the same work and could issue the <em>same</em>
/// key to two different tables. Aligning on the shared sequence is both faithful and safer, and it is what
/// would make the pairing between <c>t_sortie_gasoil</c> and <c>t_consommation_gasoil</c> — which the legacy
/// writes with one key — expressible here.
/// </para>
/// </remarks>
public static class SequenceGasoil
{
    /// <summary>The single sequence name used by every fuel table.</summary>
    public const string Operation = "GASOIL";
}

/// <summary>Which fuel a tank, voucher or issue concerns (<c>type_citerne</c>).</summary>
/// <remarks>
/// Tanks are segregated by product, and the sortie screens hard-filter on it — issuing AdBlue from a
/// Gasoil tank is a data error, not a rounding difference.
/// </remarks>
public enum TypeCiterne
{
    Gasoil,
    AdBlue
}

/// <summary>
/// Fuel tank master with its stock and weighted-average litre price (<c>t_citernes</c>).
/// </summary>
/// <remarks>
/// <para>
/// Tenant column is <c>societe</c>. <see cref="Pmp"/> — <i>prix moyen pondéré</i> — is the per-litre
/// price actually used to value every issue; <c>t_prix_gasoil</c> is only a reference market price and is
/// <b>not</b> what issues are valued at. Confusing the two misprices all fuel consumption.
/// </para>
/// <para>
/// The stock and the PMP are the analytical heart of this module, so both operations live on the entity:
/// a reception re-weights the average, and an issue refuses to drive the tank negative.
/// </para>
/// </remarks>
public sealed class Citerne : Entity, ITenantScoped
{
    /// <summary>
    /// Label of the sentinel "partner tanks" row.
    /// </summary>
    /// <remarks>
    /// Not a real tank: the legacy data uses this one row to stand in for fuel held by partner hauliers,
    /// so treating it as ordinary stock corrupts the totals.
    /// </remarks>
    public const string IntituleConfreres = "CITERNES CONFRERES";

    private Citerne() { }

    public Citerne(string keyCiterne, string societeKey, string? intituleCiterne, TypeCiterne typeCiterne)
    {
        Id = Require(keyCiterne, nameof(keyCiterne));
        SocieteKey = Require(societeKey, nameof(societeKey));
        IntituleCiterne = Trim(intituleCiterne);
        TypeCiterne = typeCiterne;
    }

    public string KeyCiterne => Id;
    public string SocieteKey { get; private set; } = string.Empty;
    public string? Agence { get; private set; }
    public string? IntituleCiterne { get; private set; }
    /// <summary>Meter index at the last movement.</summary>
    public decimal? IndexCompteur { get; private set; }
    /// <summary>Current stock in litres.</summary>
    public decimal? QteCiterne { get; private set; }
    /// <summary>
    /// Weighted-average price per litre. The <b>only</b> price used to value an issue.
    /// </summary>
    public decimal? Pmp { get; private set; }
    public TypeCiterne TypeCiterne { get; private set; }
    public bool Supprime { get; private set; }

    public bool EstGasoil => TypeCiterne == TypeCiterne.Gasoil;
    public bool EstAdBlue => TypeCiterne == TypeCiterne.AdBlue;

    /// <summary>True for the partner-tanks sentinel row, which is not real stock.</summary>
    public bool EstConfreres => string.Equals(IntituleCiterne, IntituleConfreres, StringComparison.OrdinalIgnoreCase);

    public decimal Stock => QteCiterne ?? 0m;

    /// <summary>Value of the stock at the weighted-average price.</summary>
    public decimal ValeurStock => Math.Round(Stock * (Pmp ?? 0m), 2, MidpointRounding.AwayFromZero);

    public void SetEntete(string? intituleCiterne, string? agence, decimal? indexCompteur)
    {
        IntituleCiterne = Trim(intituleCiterne);
        Agence = Trim(agence);
        IndexCompteur = indexCompteur;
    }

    /// <summary>
    /// Receives a delivery into the tank and re-weights the average price.
    /// </summary>
    /// <remarks>
    /// This is the legacy <c>nouv_pmp = ((qte_citerne*pmp) + nouv_mt) / (qte_citerne + qte)</c>. Two
    /// consequences worth stating: receiving into an <b>empty</b> tank sets the PMP to the delivered unit
    /// price, and the PMP is a stock-weighted figure, so a small delivery cannot move it much however
    /// expensive it was.
    /// </remarks>
    /// <param name="quantite">Litres received. Must be positive.</param>
    /// <param name="montant">Total cost of the delivery, in the company currency.</param>
    public void Receptionner(decimal quantite, decimal montant)
    {
        if (quantite <= 0m)
        {
            throw new BusinessRuleException("citerne.reception.qte", "La quantité réceptionnée doit être positive.");
        }

        if (montant < 0m)
        {
            throw new BusinessRuleException("citerne.reception.montant", "Le montant réceptionné ne peut pas être négatif.");
        }

        var stockAvant = Stock;
        var valeurAvant = stockAvant * (Pmp ?? 0m);
        var stockApres = stockAvant + quantite;

        // stockApres > 0 is guaranteed by the positivity check above, so this cannot divide by zero.
        Pmp = Math.Round((valeurAvant + montant) / stockApres, 4, MidpointRounding.AwayFromZero);
        QteCiterne = stockApres;
    }

    /// <summary>
    /// Issues fuel from the tank and returns the unit price applied.
    /// </summary>
    /// <remarks>
    /// The issue is valued at the <b>current</b> PMP and that value is snapshotted onto the movement, as
    /// the legacy code did. Recomputing it later from the tank would silently restate history every time
    /// a delivery arrives.
    /// </remarks>
    public decimal Sortir(decimal quantite)
    {
        if (quantite <= 0m)
        {
            throw new BusinessRuleException("citerne.sortie.qte", "La quantité sortie doit être positive.");
        }

        // The legacy guard is `(qte_citerne - <qte>) >= 0` — a tank cannot hold a negative volume.
        if (quantite > Stock)
        {
            throw new BusinessRuleException(
                "citerne.sortie.stock",
                $"Stock insuffisant dans la citerne : {Stock} L disponibles pour {quantite} L demandés.");
        }

        var prixUnitaire = Pmp ?? 0m;
        QteCiterne = Stock - quantite;

        return prixUnitaire;
    }

    /// <summary>
    /// Restores stock when an issue is cancelled.
    /// </summary>
    /// <remarks>
    /// Cancelling a movement is a <b>soft</b> delete plus a stock restore — the legacy code never removed
    /// the row, because the audit trail of what was issued has to survive.
    /// </remarks>
    public void AnnulerSortie(decimal quantite)
    {
        if (quantite <= 0m)
        {
            throw new BusinessRuleException("citerne.annulation.qte", "La quantité annulée doit être positive.");
        }

        QteCiterne = Stock + quantite;
    }

    /// <summary>
    /// Records a physical dip and returns the gap against the theoretical stock.
    /// </summary>
    /// <remarks>
    /// The theoretical figure is <b>snapshotted</b> onto the dip row at capture time, because the whole
    /// point of a dip is to compare reality against what the system believed at that moment. Reading it
    /// back later would compare the dip against a figure that has since moved.
    /// </remarks>
    /// <returns>Positive when the tank holds less than the system believed.</returns>
    public decimal Jauger(decimal quantiteReelle)
    {
        if (quantiteReelle < 0m)
        {
            throw new BusinessRuleException("citerne.jauge.qte", "La quantité jaugée ne peut pas être négative.");
        }

        return Math.Round(Stock - quantiteReelle, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Corrects the stock directly — the dedicated correction screen's operation.</summary>
    public void CorrigerStock(decimal quantiteReelle)
    {
        if (quantiteReelle < 0m)
        {
            throw new BusinessRuleException("citerne.correction.qte", "La quantité corrigée ne peut pas être négative.");
        }

        QteCiterne = quantiteReelle;
    }

    public void SupprimerLogiquement() => Supprime = true;

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Fuel issue from a tank to a vehicle (<c>t_sortie_gasoil</c>).
/// </summary>
/// <remarks>
/// Tenant column is <c>societe_sortie</c>. <see cref="PrixUnitaire"/> is a <b>snapshot</b> of the tank's
/// PMP at issue time, not a live lookup — recomputing it later would restate the cost of every past issue
/// whenever a delivery arrived.
/// </remarks>
public sealed class SortieGasoil : Entity
{
    public const string TypeSortieInterne = "Sortie Gasoil";
    /// <summary>The legacy spelling, typo included.</summary>
    public const string TypeSortieConfrere = "Sortie Gasoil Confrére";
    public const string TypeSortieTransporteur = "Sortie Gasoil Transporteur";

    private SortieGasoil() { }

    public SortieGasoil(
        string keySortie, string societeKey, string keyCiterne, TypeCiterne typeCiterne, DateTime dateSortie, decimal quantite)
    {
        Id = Require(keySortie, nameof(keySortie));
        SocieteKey = Require(societeKey, nameof(societeKey));
        KeyCiterne = Require(keyCiterne, nameof(keyCiterne));
        TypeCiterne = typeCiterne;
        DateSortie = dateSortie;
        QteSortie = quantite;
        TypeSortie = TypeSortieInterne;
    }

    public string KeySortie => Id;
    public string SocieteKey { get; private set; } = string.Empty;
    public string KeyCiterne { get; private set; } = string.Empty;
    public DateTime DateSortie { get; private set; }
    public DateTime? DateSaisie { get; private set; }
    public TypeCiterne TypeCiterne { get; private set; }
    public string? KeyVoyage { get; private set; }
    public string? KeyMateriel { get; private set; }
    public string? KeyChauffeur { get; private set; }
    public string? KeyTransporteur { get; private set; }
    public string? Matricule { get; private set; }
    public decimal? Compteur { get; private set; }
    public decimal QteSortie { get; private set; }
    public decimal? MontantSortie { get; private set; }
    /// <summary>Per-litre price snapshotted from the tank's PMP at issue time.</summary>
    public decimal? PrixUnitaire { get; private set; }
    public bool Plein { get; private set; }
    public string? TypeSortie { get; private set; }
    public string? Observation { get; private set; }
    public bool Attachement { get; private set; }
    /// <summary>Soft delete — a cancelled issue keeps its row so the audit trail survives.</summary>
    public bool Supprime { get; private set; }

    public void SetAffectation(string? keyVoyage, string? keyMateriel, string? keyChauffeur, string? matricule, decimal? compteur, bool plein)
    {
        KeyVoyage = Trim(keyVoyage);
        KeyMateriel = Trim(keyMateriel);
        KeyChauffeur = Trim(keyChauffeur);
        Matricule = Trim(matricule);
        Compteur = compteur;
        Plein = plein;
    }

    /// <summary>
    /// Applies the tank's unit price and derives the amount.
    /// </summary>
    /// <remarks>
    /// The amount is the quantity times the snapshotted price, so it stays consistent with the price even
    /// if the tank's PMP moves afterwards.
    /// </remarks>
    public void AppliquerPrix(decimal prixUnitaire, DateTime dateSaisie)
    {
        if (prixUnitaire < 0m)
        {
            throw new BusinessRuleException("sortie.prix", "Le prix unitaire ne peut pas être négatif.");
        }

        PrixUnitaire = prixUnitaire;
        MontantSortie = Math.Round(QteSortie * prixUnitaire, 2, MidpointRounding.AwayFromZero);
        DateSaisie = dateSaisie;
    }

    public void SetType(string? typeSortie, string? observation, string? keyTransporteur)
    {
        TypeSortie = Trim(typeSortie) ?? TypeSortieInterne;
        Observation = Trim(observation);
        KeyTransporteur = Trim(keyTransporteur);
    }

    public void SetAttachement(bool attachement) => Attachement = attachement;
    public void SupprimerLogiquement() => Supprime = true;

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Fuel voucher from a supplier (<c>t_bon_gasoil</c>). Tenant column is <c>societe_bon</c>.
/// </summary>
public sealed class BonGasoil : Entity
{
    /// <summary>An ordinary voucher from a supplier.</summary>
    public const string TypeBonGasoil = "Bon Gasoil";

    /// <summary>A voucher from an outside haulier.</summary>
    public const string TypeBonTransporteur = "Bon Gasoil Transporteur";

    /// <summary>The legacy literal, accent included.</summary>
    public const string TypeBonConfrere = "Bon Gasoil Confrère";

    private BonGasoil() { }

    public BonGasoil(string keyBon, string societeKey, string? numBon, DateTime dateBon, decimal quantite)
    {
        Id = Require(keyBon, nameof(keyBon));
        SocieteKey = Require(societeKey, nameof(societeKey));
        NumBon = Trim(numBon);
        DateBon = dateBon;
        QteBon = quantite;
    }

    public string KeyBon => Id;
    public string SocieteKey { get; private set; } = string.Empty;
    public string? NumBon { get; private set; }
    public DateTime DateBon { get; private set; }
    public DateTime? DateSaisie { get; private set; }
    public string? KeyFournisseur { get; private set; }
    public string? KeyVoyage { get; private set; }
    public string? KeyMateriel { get; private set; }
    public string? KeyChauffeur { get; private set; }
    public string? KeyTransporteur { get; private set; }
    public string? Matricule { get; private set; }
    public decimal? Compteur { get; private set; }
    public decimal QteBon { get; private set; }
    public string? DeviseBon { get; private set; }
    public decimal? MontantBon { get; private set; }
    public TypeCiterne? TypeCiterne { get; private set; }
    public bool Plein { get; private set; }
    public string? Observation { get; private set; }
    public string? TypeBon { get; private set; }
    public bool Attachement { get; private set; }
    public bool Supprime { get; private set; }

    public void SetFournisseur(string? keyFournisseur, string? deviseBon, decimal? montantBon)
    {
        KeyFournisseur = Trim(keyFournisseur);
        DeviseBon = Trim(deviseBon);
        MontantBon = montantBon;
    }

    public void SetAffectation(string? keyVoyage, string? keyMateriel, string? keyChauffeur, string? keyTransporteur, string? matricule, decimal? compteur, bool plein)
    {
        KeyVoyage = Trim(keyVoyage);
        KeyMateriel = Trim(keyMateriel);
        KeyChauffeur = Trim(keyChauffeur);
        KeyTransporteur = Trim(keyTransporteur);
        Matricule = Trim(matricule);
        Compteur = compteur;
        Plein = plein;
    }

    public void SetType(TypeCiterne? typeCiterne, string? typeBon, string? observation, DateTime dateSaisie)
    {
        TypeCiterne = typeCiterne;
        TypeBon = Trim(typeBon);
        Observation = Trim(observation);
        DateSaisie = dateSaisie;
    }

    public void SetAttachement(bool attachement) => Attachement = attachement;
    public void SupprimerLogiquement() => Supprime = true;

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Physical tank dip (<c>t_jauge_gasoil</c>). Tenant column is <c>societe</c>.
/// </summary>
/// <remarks>
/// <see cref="QteStockTheorique"/> is a <b>snapshot</b> of the tank's stock at capture, taken from the
/// entity so the comparison cannot drift. <see cref="Ecart"/> is therefore meaningful for ever, which it
/// would not be if the theoretical figure were re-read live.
/// </remarks>
public sealed class JaugeGasoil : Entity
{
    private JaugeGasoil() { }

    public JaugeGasoil(string keyJauge, string societeKey, string keyCiterne, DateTime dateJauge, decimal quantiteReelle, decimal quantiteTheorique)
    {
        Id = Require(keyJauge, nameof(keyJauge));
        SocieteKey = Require(societeKey, nameof(societeKey));
        KeyCiterne = Require(keyCiterne, nameof(keyCiterne));
        DateJauge = dateJauge;
        QteStock = quantiteReelle;
        QteStockTheorique = quantiteTheorique;
    }

    public string KeyJauge => Id;
    public string SocieteKey { get; private set; } = string.Empty;
    public string KeyCiterne { get; private set; } = string.Empty;
    public DateTime DateJauge { get; private set; }
    public decimal? IndexCompteur { get; private set; }
    /// <summary>Measured stock.</summary>
    public decimal? QteStock { get; private set; }
    /// <summary>What the system believed at capture time — a snapshot, not a live read.</summary>
    public decimal? QteStockTheorique { get; private set; }
    public bool Supprime { get; private set; }

    /// <summary>Positive when the tank holds less than the system believed.</summary>
    public decimal Ecart => Math.Round((QteStockTheorique ?? 0m) - (QteStock ?? 0m), 2, MidpointRounding.AwayFromZero);

    public void SetIndexCompteur(decimal? indexCompteur) => IndexCompteur = indexCompteur;
    public void SupprimerLogiquement() => Supprime = true;

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();
}

/// <summary>
/// Reference market price of the two fuels (<c>t_prix_gasoil</c>). Tenant column is <c>soc_prix</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>No key column exists.</b> The extraction evidenced only <c>soc_prix</c>, <c>date_prix</c>,
/// <c>prix_gasoil</c>, <c>prix_adblue</c>, <c>date_saisie_reel</c>, <c>utilisateur</c> and
/// <c>supprimer</c> — the practical key is <c>(soc_prix, date_prix)</c>. The identifier below is therefore
/// synthetic and unmapped, purely to satisfy the entity base.
/// </para>
/// <para>
/// <b>Reference only.</b> Issues are valued at the tank's PMP, not at this price — using it would ignore
/// what the fuel actually cost.
/// </para>
/// </remarks>
public sealed class PrixGasoil : Entity, ITenantScoped
{
    private PrixGasoil() { }

    public PrixGasoil(string societeKey, DateTime datePrix, decimal? prixGasoil, decimal? prixAdBlue)
    {
        SocieteKey = Require(societeKey, nameof(societeKey));
        DatePrix = datePrix;
        // Named `PrixDuGasoil` because a member may not share its enclosing type's name (CS0542).
        PrixDuGasoil = prixGasoil;
        PrixAdBlue = prixAdBlue;

        // Synthetic and unmapped: there is no key column on this table.
        Id = $"{societeKey}:{datePrix:yyyyMMdd}";
    }

    public string SocieteKey { get; private set; } = string.Empty;
    public DateTime DatePrix { get; private set; }
    public decimal? PrixDuGasoil { get; private set; }
    public decimal? PrixAdBlue { get; private set; }
    /// <summary>When the price was actually entered, which is what the legacy query filters on.</summary>
    public DateTime? DateSaisieReel { get; private set; }
    public string? Utilisateur { get; private set; }
    public bool Supprime { get; private set; }

    public void SetPrix(decimal? prixGasoil, decimal? prixAdBlue)
    {
        PrixDuGasoil = prixGasoil;
        PrixAdBlue = prixAdBlue;
    }

    public void SetSaisie(DateTime? dateSaisieReel, string? utilisateur)
    {
        DateSaisieReel = dateSaisieReel;
        Utilisateur = string.IsNullOrWhiteSpace(utilisateur) ? null : utilisateur.Trim();
    }

    public void SupprimerLogiquement() => Supprime = true;

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();
}
