using Logistics.Domain.Common;
using Logistics.Domain.Entities.ReferenceData;

namespace Logistics.Domain.Entities.Invoicing;

/// <summary>
/// Sales invoice / credit note / debit note header (<c>T_ENTETE_FACTURE_VENTE</c>). The tenant
/// column is <c>soc_fact</c>.
/// </summary>
/// <remarks>
/// Two things about the legacy model are worth stating up front because they surprise people:
/// <list type="number">
///   <item><c>HT_MAD</c> does <em>not</em> hold the total excl. tax. The header splits the total into
///   four buckets — <see cref="DeboursMad"/>, <see cref="AssigneMad"/>, <see cref="TaxeRegionale"/>
///   and <see cref="HtMad"/> ("Produit") — plus <see cref="TvaMad"/>. <see cref="HtMad"/> only ever
///   receives the "Produit" bucket; the full total excl. tax must be derived.</item>
///   <item>A "total avoir" (full credit note) is not a new computation: the original header is copied
///   with <em>every monetary column negated</em>.</item>
/// </list>
/// </remarks>
public sealed class EnteteFactureVente : Entity, ITenantScoped
{
    private readonly List<DetailFactureVente> _lignes = [];
    private readonly List<TvaFactureVente> _tva = [];

    private EnteteFactureVente() { }

    public EnteteFactureVente(
        string keyFacture,
        string societeKey,
        string? numFacture,
        TypeFacture typeFacture,
        string? keyClient,
        string? intituleClient,
        string deviseFacture,
        DateTime? dateFacture)
    {
        Id = Require(keyFacture, nameof(keyFacture));
        SocieteKey = Require(societeKey, nameof(societeKey));
        NumFacture = Trim(numFacture);
        TypeFacture = typeFacture;
        KeyClient = Trim(keyClient);
        IntituleClient = Trim(intituleClient);
        DeviseFacture = Require(deviseFacture, nameof(deviseFacture)).ToUpperInvariant();
        DateFacture = dateFacture;
        DeviseReg = DeviseFacture;
        TauxDeviseFacture = 1m;
        TauxDeviseReg = 1m;
        FactRegle = false;
        Integre = false;
    }

    public string KeyFacture => Id;
    public string SocieteKey { get; private set; } = string.Empty;
    public string? NumFacture { get; private set; }
    /// <summary>'F' facture, 'A' avoir, 'D' note de débit, 'C' note de crédit, 'O' solde/OD, 'I' impayé.</summary>
    public TypeFacture TypeFacture { get; private set; }
    public string? AgFact { get; private set; }
    public string? Langue { get; private set; }
    public string? TypeOperation { get; private set; }
    public string? NatureService { get; private set; }

    public string? KeyDossier { get; private set; }
    public string? KeyVoyageInt { get; private set; }
    public string? KeyClient { get; private set; }
    public string? IntituleClient { get; private set; }

    public string? MoisFacture { get; private set; }
    public DateTime? DateFacture { get; private set; }
    public DateTime? DateEcheance { get; private set; }
    public string? CommentaireFacture { get; private set; }
    public string? CommentaireExoneration { get; private set; }

    // Total decomposition (MAD)
    public decimal TtcMad { get; private set; }
    public decimal DeboursMad { get; private set; }
    public decimal AssigneMad { get; private set; }
    /// <summary>Holds only the "Produit" bucket — see the class remarks.</summary>
    public decimal HtMad { get; private set; }
    public decimal TvaMad { get; private set; }
    public decimal TaxeRegionale { get; private set; }

    // Foreign-currency presentation
    public decimal TtcDevise { get; private set; }
    public string DeviseFacture { get; private set; } = string.Empty;
    public decimal TauxDeviseFacture { get; private set; }
    public decimal TtcReg { get; private set; }
    public string DeviseReg { get; private set; } = string.Empty;
    public decimal TauxDeviseReg { get; private set; }

    public string? MontantLettreMad { get; private set; }
    public string? MontantLettreDevise { get; private set; }

    // Withholding (retenue à la source)
    public decimal RasIs { get; private set; }
    public decimal RasTva { get; private set; }
    public decimal TauxRasIs { get; private set; }
    public decimal TauxRasTva { get; private set; }
    public decimal NetAPayer { get; private set; }

    // Settlement state
    public decimal MtRegle { get; private set; }
    public bool FactRegle { get; private set; }
    public DateTime? DateDernierReglement { get; private set; }

    // Credit-note linkage
    public string? NumFactureAvoir { get; private set; }
    public string? KeyFactureAvoir { get; private set; }
    public string? KeyAvoir { get; private set; }
    public string? NumAvoir { get; private set; }

    public string? KeyReleveFacture { get; private set; }
    public bool Integre { get; private set; }
    public string? StatutFacture { get; private set; }
    public bool Attachement { get; private set; }
    public bool EnvoiEmail { get; private set; }
    public string? MagasinDouane { get; private set; }
    public string? KeyExoneration { get; private set; }
    public DateTime? DateCommission { get; private set; }

    public IReadOnlyCollection<DetailFactureVente> Lignes => _lignes.AsReadOnly();
    public IReadOnlyCollection<TvaFactureVente> TvaDetail => _tva.AsReadOnly();

    /// <summary>Sign convention: only a plain invoice is positive; every other type is a credit.</summary>
    public int Signe => TypeFacture == TypeFacture.Facture ? 1 : -1;

    /// <summary>Total excl. tax across all four buckets.</summary>
    public decimal TotalHtMad => DeboursMad + AssigneMad + TaxeRegionale + HtMad;

    /// <summary>Remaining amount to settle, in MAD.</summary>
    public decimal EncoursMad => NetAPayer != 0m ? NetAPayer - MtRegle : TtcMad - MtRegle;

    public bool IsFullySettled => MtRegle >= (NetAPayer != 0m ? NetAPayer : TtcMad) && (NetAPayer != 0m || TtcMad != 0m);

    public void SetEntete(
        string? agFact,
        string? langue,
        string? typeOperation,
        string? natureService,
        string? keyDossier,
        string? keyVoyageInt,
        DateTime? dateEcheance,
        string? commentaireFacture,
        string? magasinDouane)
    {
        AgFact = Trim(agFact);
        Langue = Trim(langue);
        TypeOperation = Trim(typeOperation);
        NatureService = Trim(natureService);
        KeyDossier = Trim(keyDossier);
        KeyVoyageInt = Trim(keyVoyageInt);
        DateEcheance = dateEcheance;
        CommentaireFacture = Trim(commentaireFacture);
        MagasinDouane = Trim(magasinDouane);
        MoisFacture = DateFacture?.ToString("MM/yyyy");
    }

    public void SetClientSnapshot(
        string? intituleClient,
        string? adresse1,
        string? adresse2,
        string? cp,
        string? ville,
        string? pays)
    {
        IntituleClient = Trim(intituleClient);
        Adresse1Client = Trim(adresse1);
        Adresse2Client = Trim(adresse2);
        CpClient = Trim(cp);
        VilleClient = Trim(ville);
        PaysClient = Trim(pays);
    }

    public string? Adresse1Client { get; private set; }
    public string? Adresse2Client { get; private set; }
    public string? CpClient { get; private set; }
    public string? VilleClient { get; private set; }
    public string? PaysClient { get; private set; }

    /// <summary>
    /// Recomputes every header bucket from the invoice lines, then derives the foreign-currency and
    /// withholding amounts. This mirrors the legacy "Mise à jour de la table entête" step.
    /// </summary>
    public void Recalculate(Devise deviseFacture, decimal tauxDeviseFacture, decimal tauxRasIs, decimal tauxRasTva)
    {
        DeboursMad = Sum(n => n.NatureLigne == NatureLigne.Debours);
        AssigneMad = Sum(n => n.NatureLigne == NatureLigne.Assigne);
        TaxeRegionale = Sum(n => n.NatureLigne == NatureLigne.TaxeRegionale);
        HtMad = Sum(n => n.NatureLigne == NatureLigne.Produit);
        TvaMad = Math.Round(_tva.Sum(t => t.Tva), 2, MidpointRounding.AwayFromZero);

        TtcMad = DeboursMad + AssigneMad + TaxeRegionale + HtMad + TvaMad;

        TauxDeviseFacture = tauxDeviseFacture <= 0 ? 1m : tauxDeviseFacture;
        DeviseFacture = deviseFacture.CodeDevise;
        TtcDevise = deviseFacture.Round(TtcMad / TauxDeviseFacture);
        TtcReg = TtcDevise;

        TauxRasIs = tauxRasIs;
        TauxRasTva = tauxRasTva;

        // Withholding is only computed when the company configured rates.
        RasTva = tauxRasTva > 0
            ? Math.Round(TvaMad * tauxRasTva / 100m, 2, MidpointRounding.AwayFromZero)
            : 0m;

        var rasIsBase = Sum(n => n.NatureLigne == NatureLigne.Produit) / TauxDeviseFacture;
        RasIs = tauxRasIs > 0
            ? Math.Round(rasIsBase * tauxRasIs / 100m, 2, MidpointRounding.AwayFromZero)
            : 0m;

        NetAPayer = TtcDevise - (RasIs + RasTva);
    }

    /// <summary>Replaces the VAT recap, keeping one row per rate. Zero rows are kept on purpose so the
    /// printed VAT table shows every configured rate.</summary>
    public void SetTvaRecap(IEnumerable<TvaFactureVente> rows)
    {
        _tva.Clear();
        _tva.AddRange(rows);
    }

    public DetailFactureVente AddLigne(DetailFactureVente ligne)
    {
        _lignes.Add(ligne);
        return ligne;
    }

    public void ClearLignes() => _lignes.Clear();

    /// <summary>Applies a company exemption: zeroes the VAT on every line and records the reason.</summary>
    public void ApplyExoneration(string? commentaireExoneration)
    {
        foreach (var ligne in _lignes)
        {
            ligne.ZeroTva();
        }

        foreach (var tva in _tva)
        {
            tva.Zero();
        }

        CommentaireExoneration = Trim(commentaireExoneration);
    }

    /// <summary>
    /// Records a settlement allocation. When the paid amount reaches the outstanding balance the
    /// invoice is flagged as settled, reproducing the legacy side effect.
    /// </summary>
    public void RegisterPayment(decimal montant, bool negateForPurchase, DateTime dateReglement)
    {
        var delta = negateForPurchase ? -montant : montant;
        MtRegle += delta;
        DateDernierReglement = dateReglement;

        if (MtRegle >= (NetAPayer != 0m ? NetAPayer : TtcMad))
        {
            FactRegle = true;
        }
    }

    /// <summary>Reverses a settlement (de-lettrage).</summary>
    public void ReversePayment(decimal montant, bool negateForPurchase)
    {
        MtRegle -= negateForPurchase ? -montant : montant;
        FactRegle = false;
        DateDernierReglement = null;
    }

    /// <summary>Records the status used by the contestation / dunning modules.</summary>
    public void SetStatutFacture(string? statutFacture) => StatutFacture = Trim(statutFacture);

    public void MarkIntegrated() => Integre = true;
    public void SetAttachement(bool attachement) => Attachement = attachement;
    public void MarkEmailSent() => EnvoiEmail = true;
    public void LinkReleve(string? keyReleve) => KeyReleveFacture = Trim(keyReleve);
    public void LinkExoneration(string? keyExoneration) => KeyExoneration = Trim(keyExoneration);
    public void MarkCommissionGenerated(DateTime date) => DateCommission = date;

    public void LinkAvoir(string? keyAvoir, string? numAvoir)
    {
        KeyAvoir = Trim(keyAvoir);
        NumAvoir = Trim(numAvoir);
    }

    /// <summary>
    /// Builds the counter-document for a conversion difference (the legacy "OD" entry): a
    /// <see cref="TypeFacture.Od"/> invoice keyed on the payment, with the difference as its total.
    /// </summary>
    public static EnteteFactureVente CreateEcartOd(
        string keyReglement,
        string societeKey,
        string? keyClient,
        string devise,
        decimal ecart,
        DateTime dateReglement)
    {
        var od = new EnteteFactureVente(
            keyReglement,
            societeKey,
            numFacture: null,
            TypeFacture.Od,
            keyClient,
            intituleClient: null,
            devise,
            dateReglement);

        od.CommentaireFacture = $"Ecart suite Réglement du {dateReglement:dd/MM/yyyy}";
        od.TtcMad = ecart;
        od.TtcDevise = ecart;
        od.NetAPayer = ecart;
        return od;
    }

    /// <summary>
    /// Creates the full credit note ("avoir total") for this invoice. Every monetary column is
    /// negated and each line is duplicated with its key re-prefixed by 'A'.
    /// </summary>
    public EnteteFactureVente CreateAvoirTotal(string keyAvoir, string? numAvoir, bool avoirTotalFactRegle)
    {
        if (TypeFacture != TypeFacture.Facture)
        {
            throw new BusinessRuleException("avoir.source", "Un avoir total ne peut être généré que depuis une facture.");
        }

        var avoir = new EnteteFactureVente(
            keyAvoir,
            SocieteKey,
            numAvoir,
            TypeFacture.Avoir,
            KeyClient,
            IntituleClient,
            DeviseFacture,
            DateFacture);

        avoir.SetEntete(AgFact, Langue, TypeOperation, NatureService, KeyDossier, KeyVoyageInt, DateEcheance, CommentaireFacture, MagasinDouane);
        avoir.SetClientSnapshot(IntituleClient, Adresse1Client, Adresse2Client, CpClient, VilleClient, PaysClient);
        avoir.DeviseReg = DeviseReg;
        avoir.TauxDeviseFacture = TauxDeviseFacture;
        avoir.TauxDeviseReg = TauxDeviseReg;

        avoir.TtcMad = -TtcMad;
        avoir.TtcDevise = -TtcDevise;
        avoir.TtcReg = -TtcReg;
        avoir.HtMad = -HtMad;
        avoir.TvaMad = -TvaMad;
        avoir.DeboursMad = -DeboursMad;
        avoir.AssigneMad = -AssigneMad;
        avoir.TaxeRegionale = -TaxeRegionale;
        avoir.RasIs = -RasIs;
        avoir.RasTva = -RasTva;
        avoir.NetAPayer = -NetAPayer;

        foreach (var ligne in _lignes)
        {
            avoir.AddLigne(ligne.ToAvoirLine(keyAvoir));
        }

        avoir.SetTvaRecap(_tva.Select(t => t.ToAvoirRow(keyAvoir)).ToArray());

        // Legacy rule: an avoir may be considered already settled depending on a company parameter.
        if (avoirTotalFactRegle)
        {
            avoir.FactRegle = true;
            avoir.MtRegle = -NetAPayer;
        }

        KeyFactureAvoir = keyAvoir;
        NumFactureAvoir = numAvoir;

        return avoir;
    }

    private decimal Sum(Func<DetailFactureVente, bool> predicate) =>
        Math.Round(_lignes.Where(predicate).Sum(l => l.MtHtMad), 2, MidpointRounding.AwayFromZero);

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Invoice document kind (<c>TYPE_FACTURE</c>).</summary>
public enum TypeFacture
{
    /// <summary>'F' — plain invoice.</summary>
    Facture = 'F',
    /// <summary>'A' — credit note.</summary>
    Avoir = 'A',
    /// <summary>'D' — debit note.</summary>
    DebitNote = 'D',
    /// <summary>'C' — credit note (commercial).</summary>
    CreditNote = 'C',
    /// <summary>'O' — balance / miscellaneous-operation line raised by a payment difference.</summary>
    Od = 'O',
    /// <summary>'I' — unpaid item.</summary>
    Impaye = 'I'
}

/// <summary>Bucket a line belongs to (<c>nature_Rubrique</c> / <c>NATURE_LIGNE</c>).</summary>
public enum NatureLigne
{
    Produit,
    Debours,
    Assigne,
    TaxeRegionale,
    Charge
}

/// <summary>
/// Printed invoice line (<c>T_DETAIL_FACTURE_VENTE</c>), aggregated by rubrique from the taxation
/// detail. No tenant column — scoping comes from the parent invoice.
/// </summary>
public sealed class DetailFactureVente : Entity
{
    private DetailFactureVente() { }

    public DetailFactureVente(
        string keyLigne,
        string keyFacture,
        string? libelleRubrique,
        NatureLigne natureLigne,
        decimal mtTotalDevise,
        decimal tauxRemise,
        decimal deviseTauxChange,
        decimal tauxTva)
    {
        Id = Require(keyLigne, nameof(keyLigne));
        KeyFacture = Require(keyFacture, nameof(keyFacture));
        LibelleRubrique = Trim(libelleRubrique);
        NatureLigne = natureLigne;
        MtTotalDevise = mtTotalDevise;
        TauxRemise = tauxRemise;
        TauxChange = deviseTauxChange <= 0 ? 1m : deviseTauxChange;
        TauxTvaInit = tauxTva;
        TauxTva = tauxTva;

        // Net = gross less the discount; MAD counter-value = net * rate.
        MtDevise = Math.Round(mtTotalDevise * (100m - tauxRemise) / 100m, 3, MidpointRounding.AwayFromZero);
        MtHtMad = Math.Round(MtDevise * TauxChange, 2, MidpointRounding.AwayFromZero);
    }

    public string KeyLigne => Id;
    public string KeyFacture { get; private set; } = string.Empty;
    public string? CodeRubrique { get; private set; }
    public string? SocRubrique { get; private set; }
    public int? Position { get; private set; }
    public string? LibelleRubrique { get; private set; }
    public string? Commentaire { get; private set; }

    /// <summary>Gross amount before discount, in the line currency.</summary>
    public decimal MtTotalDevise { get; private set; }
    public decimal TauxRemise { get; private set; }
    /// <summary>Net amount after discount, in the line currency.</summary>
    public decimal MtDevise { get; private set; }
    public string? Devise { get; private set; }
    public decimal TauxChange { get; private set; }
    /// <summary>MAD counter-value = <see cref="MtDevise"/> × <see cref="TauxChange"/>.</summary>
    public decimal MtHtMad { get; private set; }

    /// <summary>Rate before any exemption — used to detect whether an exemption actually applied.</summary>
    public decimal TauxTvaInit { get; private set; }
    public decimal TauxTva { get; private set; }
    public string? CodeTva { get; private set; }
    public bool Imprimable { get; private set; }
    public NatureLigne NatureLigne { get; private set; }

    /// <summary>True when the line resists an exemption (its effective rate still differs from the original).</summary>
    public bool IsExonerationEffective => TauxTva != TauxTvaInit;

    /// <summary>Zeroes the VAT rate — the exemption path.</summary>
    public void ZeroTva()
    {
        TauxTva = 0m;
        CodeTva = "0%";
    }

    /// <summary>Duplicates this line as a credit-note line: amounts negated, key re-prefixed with 'A'.</summary>
    public DetailFactureVente ToAvoirLine(string keyAvoir)
    {
        var key = Id.Length > 1 && Id[0] == 'A' ? Id : "A" + Id[1..];

        var avoir = new DetailFactureVente(
            key,
            keyAvoir,
            LibelleRubrique,
            NatureLigne,
            -MtTotalDevise,
            TauxRemise,
            TauxChange,
            TauxTva)
        {
            CodeRubrique = CodeRubrique,
            SocRubrique = SocRubrique,
            Position = Position,
            Commentaire = Commentaire,
            Devise = Devise,
            CodeTva = CodeTva,
            Imprimable = Imprimable
        };

        return avoir;
    }

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// VAT recap row per invoice and rate (<c>T_TVA_FACTURE_VENTE</c>). One row per rate, deliberately
/// including zero rows so the printed table lists every configured rate.
/// </summary>
public sealed class TvaFactureVente : Entity
{
    private TvaFactureVente() { }

    public TvaFactureVente(string keyFacture, string codeTva, string societeKey, decimal ht, decimal tva)
    {
        Id = $"{keyFacture}:{codeTva}";
        KeyFacture = Require(keyFacture, nameof(keyFacture));
        CodeTva = Require(codeTva, nameof(codeTva));
        SocieteKey = Require(societeKey, nameof(societeKey));
        Ht = ht;
        Tva = tva;
    }

    public string KeyFacture { get; private set; } = string.Empty;
    public string CodeTva { get; private set; } = string.Empty;
    public string SocieteKey { get; private set; } = string.Empty;
    public decimal Ht { get; private set; }
    public decimal Tva { get; private set; }
    /// <summary>Portion of HT+TVA already consumed by a settlement.</summary>
    public decimal MtImpute { get; private set; }

    /// <summary>Remaining VAT not yet imputed to a payment.</summary>
    public decimal Reliquat => MtImpute - Ht;

    public bool IsFullyImputed => Ht + Tva <= MtImpute;

    public void Impute(decimal montant) => MtImpute += montant;

    public void Zero()
    {
        Ht = 0m;
        Tva = 0m;
    }

    /// <summary>Duplicates this recap row for a credit note, negating HT and TVA.</summary>
    public TvaFactureVente ToAvoirRow(string keyAvoir) =>
        new(keyAvoir, CodeTva, SocieteKey, -Ht, -Tva);

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();
}

/// <summary>
/// Cost/revenue allocation across dossiers and voyages (<c>T_REPARTITION_DOSSIER</c>). Written for
/// both sales (<c>nature_repartition = 'Vente'</c>) and purchases (<c>'Achat'</c>), and also carries
/// the internal voyage-cost postings.
/// </summary>
/// <remarks>
/// The legacy algorithm is delete-then-reinsert per invoice. When a sales line carries a voyage but
/// no dossier, the amount is split across the voyage's dossiers <em>pro rata to
/// <c>t_dossiers.poids_taxable</c></em>; internal voyage costing instead splits <em>equally</em> by
/// <c>t_voyages.nb_doss_voyage_saisi</c>. Both are reproduced by <see cref="AllocationCalculator"/>.
/// </remarks>
public sealed class RepartitionDossier : Entity
{
    /// <summary>Sentinel used by the legacy code when a line belongs to no specific dossier/voyage.</summary>
    public const string Divers = "DIVERS";

    private RepartitionDossier() { }

    public RepartitionDossier(
        string keyFacture,
        string? keyLigneDetailFact,
        string? keyVoyageInter,
        string? keyDossierInter,
        decimal montantMad,
        decimal montantDevise,
        string devise,
        NatureRepartition natureRepartition,
        string? typeRepartition,
        string? moisCout)
    {
        Id = $"{keyFacture}:{keyLigneDetailFact}:{keyDossierInter ?? Divers}:{typeRepartition}";
        KeyFacture = Require(keyFacture, nameof(keyFacture));
        KeyLigneDetailFact = Trim(keyLigneDetailFact);
        KeyVoyageInter = Trim(keyVoyageInter);
        KeyDossierInter = Trim(keyDossierInter);
        MontantMad = montantMad;
        MontantDevise = montantDevise;
        Devise = Require(devise, nameof(devise)).ToUpperInvariant();
        NatureRepartition = natureRepartition;
        TypeRepartition = Trim(typeRepartition);
        MoisCout = Trim(moisCout);
    }

    /// <summary>
    /// Surrogate identity key.
    /// </summary>
    /// <remarks>
    /// The legacy table has no primary key at all — every writer simply deletes by
    /// <c>key_facture</c> and re-inserts. A relational store needs a key, so a synthetic identity
    /// column is added. This is purely additive and therefore safe for a straight data migration;
    /// <see cref="Entity.Id"/> keeps the human-readable composite for logging and equality.
    /// </remarks>
    public long RepartitionId { get; private set; }

    public string KeyFacture { get; private set; } = string.Empty;
    public string? KeyLigneDetailFact { get; private set; }
    public string? KeyVoyageInter { get; private set; }
    public string? KeyDossierInter { get; private set; }
    public decimal MontantMad { get; private set; }
    public decimal MontantDevise { get; private set; }
    public string Devise { get; private set; } = string.Empty;
    public NatureRepartition NatureRepartition { get; private set; }
    public string? TypeRepartition { get; private set; }
    public string? MoisCout { get; private set; }
    /// <summary>Set when a purchase disbursement is billed on a sales invoice; cleared on de-assignment.</summary>
    public string? KeyLigneTaxation { get; private set; }

    public void LinkTaxationLine(string? keyLigneTaxation) => KeyLigneTaxation = Trim(keyLigneTaxation);

    public void SetMoisCout(string? moisCout) => MoisCout = Trim(moisCout);

    /// <summary>Rescales this row by a ratio — used by the pro-rata voyage split.</summary>
    public void ApplyRatio(decimal ratio)
    {
        MontantMad = Math.Round(MontantMad * ratio, 3, MidpointRounding.AwayFromZero);
        MontantDevise = Math.Round(MontantDevise * ratio, 3, MidpointRounding.AwayFromZero);
    }

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Allocation direction (<c>nature_repartition</c>).</summary>
public enum NatureRepartition
{
    Vente,
    Achat,
    /// <summary>'FP' — frais portuaire postings.</summary>
    Fp
}

/// <summary>
/// Pure pro-rata arithmetic behind <see cref="RepartitionDossier"/>, extracted so the two competing
/// legacy rules can be unit-tested without a database.
/// </summary>
public static class AllocationCalculator
{
    /// <summary>
    /// Splits an amount across a voyage's dossiers pro rata to taxable weight
    /// (<c>t_dossiers.poids_taxable</c>). Returns an empty list when the total weight is zero, in
    /// which case the caller must fall back to an equal split.
    /// </summary>
    public static IReadOnlyList<(string KeyDossier, decimal MontantMad, decimal MontantDevise)> SplitByPoidsTaxable(
        decimal montantMad,
        decimal montantDevise,
        IEnumerable<(string KeyDossier, decimal PoidsTaxable)> dossiers)
    {
        var list = dossiers.ToArray();
        var totalPoids = list.Sum(d => d.PoidsTaxable);

        if (totalPoids <= 0 || list.Length == 0)
        {
            return [];
        }

        return list
            .Select(d => (
                d.KeyDossier,
                Math.Round(montantMad * d.PoidsTaxable / totalPoids, 3, MidpointRounding.AwayFromZero),
                Math.Round(montantDevise * d.PoidsTaxable / totalPoids, 3, MidpointRounding.AwayFromZero)))
            .ToArray();
    }

    /// <summary>
    /// Splits an amount equally across N dossiers — the internal voyage-costing rule. Guards against
    /// the legacy division by zero when <c>nb_doss_voyage_saisi</c> is 0.
    /// </summary>
    public static IReadOnlyList<(string KeyDossier, decimal MontantMad, decimal MontantDevise)> SplitEqually(
        decimal montantMad,
        decimal montantDevise,
        IEnumerable<string> keyDossiers,
        int? nbDossVoyageSaisi)
    {
        var list = keyDossiers.ToArray();
        var divisor = nbDossVoyageSaisi.GetValueOrDefault() <= 0 ? 1 : nbDossVoyageSaisi!.Value;

        if (list.Length == 0)
        {
            return [];
        }

        return list
            .Select(k => (
                k,
                Math.Round(montantMad / divisor, 3, MidpointRounding.AwayFromZero),
                Math.Round(montantDevise / divisor, 3, MidpointRounding.AwayFromZero)))
            .ToArray();
    }
}
