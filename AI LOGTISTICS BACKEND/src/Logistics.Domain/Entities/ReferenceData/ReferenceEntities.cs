using Logistics.Domain.Common;
using Logistics.Domain.Enums;

namespace Logistics.Domain.Entities.ReferenceData;

/// <summary>
/// Marks an entity whose rows are partitioned by company (the legacy tenant key, exposed under a
/// table-specific column name such as <c>soc_client</c>, <c>soc_fact</c>, <c>soc_reglement</c>,
/// <c>soc_agence</c>…). Tenant isolation in the legacy application is enforced <em>by convention
/// only</em> — every hand-written query appends <c>where soc_xxx = Constante.Societe.Keysociete</c>
/// and there is no row-level security. The new backend enforces it centrally through EF global
/// query filters (see <c>LogisticsDbContext</c>) so a forgotten predicate can no longer leak data
/// across companies.
/// </summary>
public interface ITenantScoped
{
    /// <summary>Value of the tenant column (FK to <see cref="Societe.KeySoc"/>).</summary>
    string SocieteKey { get; }
}

/// <summary>
/// Company / tenant master (<c>t_societe</c>). Root of the entire model: every other tenant-scoped
/// table points at <see cref="KeySoc"/>.
/// </summary>
/// <remarks>
/// The legacy application never inserts into this table (companies are provisioned out of band by
/// setup/DBA), so the column list here is derived from the two hand-written SELECT lists plus the
/// single UPDATE in <c>ClassSociete.vb</c>. Only the columns that carry behaviour the API needs are
/// modelled; the remaining ~40 printing/EDI/path settings are intentionally not mapped yet.
/// </remarks>
public sealed class Societe : Entity
{
    private Societe() { }

    public Societe(string keySoc, string intituleSoc, string deviseSoc)
    {
        Id = Require(keySoc, nameof(keySoc));
        IntituleSoc = Require(intituleSoc, nameof(intituleSoc));
        DeviseSoc = Require(deviseSoc, nameof(deviseSoc));
    }

    public string KeySoc => Id;
    public string IntituleSoc { get; private set; } = string.Empty;
    public string? SocAbrege { get; private set; }

    // Identity / legal
    public string? NumRc { get; private set; }
    public string? CentreRc { get; private set; }
    public string? NumIf { get; private set; }
    public string? NumPatente { get; private set; }
    public string? NumIce { get; private set; }
    public string? NumCnss { get; private set; }

    // Address
    public string? Adresse1Soc { get; private set; }
    public string? Adresse2Soc { get; private set; }
    public string? CpSoc { get; private set; }
    public string? VilleSoc { get; private set; }
    public string? PaysSoc { get; private set; }
    public string? TelSoc { get; private set; }
    public string? FaxSoc { get; private set; }
    public string? EmailSoc { get; private set; }
    public string? WebSoc { get; private set; }

    /// <summary>Functional currency. Amounts are converted to this currency for accounting.</summary>
    public string DeviseSoc { get; private set; } = string.Empty;

    // General-ledger defaults
    public string? CpGeneralClient { get; private set; }
    public string? CpGeneralFournisseur { get; private set; }
    public string? CpGeneralDebours { get; private set; }
    public string? JournalVenteSoc { get; private set; }
    public string? JournalAchatSoc { get; private set; }
    public string? JournalDeboursSoc { get; private set; }
    public string? JournalOdSoc { get; private set; }
    public string? CptGain { get; private set; }
    public string? CptPerte { get; private set; }
    public string? CptFraisBanque { get; private set; }
    public string? CptTvaBanque { get; private set; }
    public string? CptCommissionBanque { get; private set; }

    // Behavioural switches referenced by the invoicing/payment logic
    /// <summary><c>numerotation_facture</c>; application default when NULL is "Par Société".</summary>
    public string? NumerotationFacture { get; private set; }
    /// <summary><c>declaration_tva</c>.</summary>
    public string? DeclarationTva { get; private set; }
    /// <summary><c>cloture_mois_vente</c>.</summary>
    public string? ClotureMoisVente { get; private set; }
    /// <summary><c>cloture_mois_achat</c>.</summary>
    public string? ClotureMoisAchat { get; private set; }
    public string? GenerationDateFacture { get; private set; }

    /// <summary>Open sales-invoicing period (<c>PERIODE_FACTURATION</c>).</summary>
    public DateTime? PeriodeFacturation { get; private set; }
    /// <summary>Open purchasing period (<c>periode_achat</c>).</summary>
    public DateTime? PeriodeAchat { get; private set; }

    /// <summary>Withholding-tax rates (<c>TAUX_RAS_IS</c> / <c>TAUX_RAS_TVA</c>), in percent.</summary>
    public double? TauxRasIs { get; private set; }
    public double? TauxRasTva { get; private set; }
    /// <summary>Withholding ceiling (<c>PLAFOND_TTC_RAS</c>).</summary>
    public double? PlafondTtcRas { get; private set; }
    /// <summary>Amount excluded from withholding (<c>TTC_EXCLU_RAS</c>).</summary>
    public double? TtcExcluRas { get; private set; }

    /// <summary>Free-storage days used by MEAD storage-cost calculation.</summary>
    public int? NbJrFranchise { get; private set; }
    /// <summary>Licence ceiling on the number of agencies.</summary>
    public int? NbAgence { get; private set; }
    /// <summary>Days after which a client counts as inactive.</summary>
    public int? NbJrClientActif { get; private set; }

    /// <summary>Monthly overhead cost consumed by voyage costing (<c>cout_societe</c>).</summary>
    public decimal? CoutSociete { get; private set; }
    public decimal? CoutMead { get; private set; }
    public decimal? CoutAssuranceMarchandise { get; private set; }

    public string? DepartementSoc { get; private set; }
    public string? RegionSoc { get; private set; }
    public string? Langue { get; private set; }

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", param)
            : value.Trim();
}

/// <summary>Country/zone grouping (<c>t_zone_pays</c>) — a single-column table: the label is the key.</summary>
public sealed class ZonePays : Entity
{
    private ZonePays() { }

    public ZonePays(string keyZone) => Id = Require(keyZone);

    public string KeyZone => Id;

    private static string Require(string value) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Zone is required.", nameof(value)) : value.Trim();
}

/// <summary>Country (<c>T_PAYS</c>) — global, not tenant-scoped.</summary>
public sealed class Pays : Entity
{
    private Pays() { }

    public Pays(string codePays, string? intitulePays = null, string? alpha2 = null)
    {
        Id = Require(codePays, nameof(codePays));
        IntitulePays = Trim(intitulePays);
        Alpha2 = Trim(alpha2);
    }

    public string CodePays => Id;
    public string? IntitulePays { get; private set; }
    /// <summary>ISO 3166-1 alpha-2. Uniqueness is enforced by the application (legacy <c>ExisteCodeAlha</c>).</summary>
    public string? Alpha2 { get; private set; }
    public string? Accord { get; private set; }
    public string? ZonePaysKey { get; private set; }

    public void Update(string? intitulePays, string? alpha2, string? accord, string? zonePays)
    {
        IntitulePays = Trim(intitulePays);
        Alpha2 = Trim(alpha2);
        Accord = Trim(accord);
        ZonePaysKey = Trim(zonePays);
    }

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Code is required.", param) : value.Trim().ToUpperInvariant();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>City (<c>T_VILLE</c>) — the real geographic anchor used by agencies, clients and agents.</summary>
public sealed class Ville : Entity
{
    private Ville() { }

    public Ville(string keyVille, string intituleVille, string? paysVille = null)
    {
        Id = Require(keyVille, nameof(keyVille));
        IntituleVille = Require(intituleVille, nameof(intituleVille));
        PaysVille = Trim(paysVille);
    }

    public string KeyVille => Id;
    public string IntituleVille { get; private set; } = string.Empty;
    public string? PaysVille { get; private set; }

    public void Update(string intituleVille, string? paysVille)
    {
        IntituleVille = Require(intituleVille, nameof(intituleVille));
        PaysVille = Trim(paysVille);
    }

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Currency (<c>t_devise</c>) — global.</summary>
public sealed class Devise : Entity
{
    private Devise() { }

    public Devise(string codeDevise, string? libelleDevise = null, int nbDecimal = 2)
    {
        Id = Require(codeDevise, nameof(codeDevise));
        LibelleDevise = Trim(libelleDevise);
        NbDecimal = nbDecimal;
    }

    public string CodeDevise => Id;
    public string? LibelleDevise { get; private set; }
    /// <summary>Label printed on invoices; also used when spelling amounts out in words.</summary>
    public string? LibelleFacture { get; private set; }
    /// <summary>Label of the minor unit ("Centime").</summary>
    public string? LibelleCent { get; private set; }
    /// <summary>Decimal places used for rounding. The DB default is 2.</summary>
    public int NbDecimal { get; private set; }

    /// <summary>
    /// The two-character <b>EDI</b> code an inbound file carries for this currency
    /// (<c>t_devise.devise_edi</c>).
    /// </summary>
    /// <remarks>
    /// Distinct from the currency code: the file's own code ("00" for EUR) is meaningless outside the
    /// file and has to be mapped onto a real currency.
    /// </remarks>
    public string? DeviseEdi { get; private set; }

    public void Update(string? libelleDevise, string? libelleFacture, string? libelleCent, int nbDecimal)
    {
        LibelleDevise = Trim(libelleDevise);
        LibelleFacture = Trim(libelleFacture);
        LibelleCent = Trim(libelleCent);
        NbDecimal = nbDecimal < 0 ? 0 : nbDecimal;
    }

    /// <summary>Sets the EDI mapping code used by the inbound file resolution.</summary>
    public void SetDeviseEdi(string? deviseEdi) => DeviseEdi = Trim(deviseEdi)?.ToUpperInvariant();

    /// <summary>
    /// Rounds an amount to this currency's precision, reproducing the legacy
    /// <c>tools.MontantArrondi(devise, montant)</c> behaviour.
    /// </summary>
    public decimal Round(decimal amount) => Math.Round(amount, NbDecimal, MidpointRounding.AwayFromZero);

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Code is required.", param) : value.Trim().ToUpperInvariant();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Port (<c>t_port</c>). Despite its name, <see cref="VillePortKey"/> stores a city key, not a label.</summary>
public sealed class Port : Entity
{
    private Port() { }

    public Port(string codePort, string? libellePort = null, string? villePortKey = null)
    {
        Id = Require(codePort, nameof(codePort));
        LibellePort = Trim(libellePort);
        VillePortKey = Trim(villePortKey);
    }

    public string CodePort => Id;
    public string? LibellePort { get; private set; }
    /// <summary>FK to <see cref="Ville.KeyVille"/>.</summary>
    public string? VillePortKey { get; private set; }

    public void Update(string? libellePort, string? villePortKey)
    {
        LibellePort = Trim(libellePort);
        VillePortKey = Trim(villePortKey);
    }

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Incoterm (<c>t_incoterm</c>). One of the few referentials using soft delete.</summary>
public sealed class Incoterm : Entity
{
    private Incoterm() { }

    public Incoterm(string incoterm, string? libelle = null)
    {
        Id = Require(incoterm, nameof(incoterm));
        LibelleIncoterm = Trim(libelle);
        Supprime = false;
    }

    public string IncotermCode => Id;
    public string? LibelleIncoterm { get; private set; }
    /// <summary><c>supp_incoterm</c>: 1 = deleted. Every read must filter this.</summary>
    public bool Supprime { get; private set; }
    public bool FactureAssigneImport { get; private set; }
    public bool FactureAssigneExport { get; private set; }
    /// <summary>0 = livrer au CLIENT, 1 = livrer à la société elle-même.</summary>
    public bool Livraison { get; private set; }

    /// <summary>
    /// The two-character <b>EDI</b> code an inbound file carries for this incoterm
    /// (<c>t_incoterm.incoterm_edi</c>).
    /// </summary>
    /// <remarks>
    /// A separate column from the incoterm code itself: the file speaks EDI codes and the application
    /// speaks incoterm codes, so the two must be mapped rather than assumed equal.
    /// </remarks>
    public string? IncotermEdi { get; private set; }

    public void Update(string? libelle, bool factureAssigneImport, bool factureAssigneExport, bool livraison)
    {
        LibelleIncoterm = Trim(libelle);
        FactureAssigneImport = factureAssigneImport;
        FactureAssigneExport = factureAssigneExport;
        Livraison = livraison;
    }

    /// <summary>Sets the EDI mapping code used by the inbound file resolution.</summary>
    public void SetIncotermEdi(string? incotermEdi) => IncotermEdi = Trim(incotermEdi)?.ToUpperInvariant();

    /// <summary>Logical delete — mirrors <c>update t_incoterm set supp_incoterm = 1</c>.</summary>
    public void SoftDelete() => Supprime = true;

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim().ToUpperInvariant();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Agency / office of a company (<c>t_agence</c>). The tenant column is <c>soc_agence</c>, not
/// <c>societe</c>. Soft delete only (<c>supp</c>).
/// </summary>
public sealed class Agence : Entity, ITenantScoped
{
    private Agence() { }

    public Agence(string keyAgence, string societeKey, string? intituleAgence = null, string? codeAgence = null)
    {
        Id = Require(keyAgence, nameof(keyAgence));
        SocieteKey = Require(societeKey, nameof(societeKey));
        IntituleAgence = Trim(intituleAgence);
        CodeAgence = Trim(codeAgence);
    }

    public string KeyAgence => Id;
    public string SocieteKey { get; private set; } = string.Empty;
    public string? CodeAgence { get; private set; }
    public string? IntituleAgence { get; private set; }
    public string? AgenceAbrege { get; private set; }

    public string? Adresse1Agence { get; private set; }
    public string? Adresse2Agence { get; private set; }
    public string? VilleAgence { get; private set; }
    public string? PaysAgence { get; private set; }
    public string? CpAgence { get; private set; }
    public string? TelAgence { get; private set; }
    public string? FaxAgence { get; private set; }
    public string? EmailAgence { get; private set; }

    public string? JournalVenteAg { get; private set; }
    public string? JournalAchatAg { get; private set; }
    public string? JournalDeboursAg { get; private set; }

    /// <summary>Customs-clearance place; maintained in the legacy DB by <c>dbo.Recup_MagasinAgence</c>.</summary>
    public string? LieuDedouanement { get; private set; }
    public bool ScanMead { get; private set; }
    public string? ImprimanteEtiquette { get; private set; }
    /// <summary>Monthly agency cost consumed by voyage costing.</summary>
    public decimal? CoutAgence { get; private set; }
    /// <summary>Soft-delete flag (<c>supp</c>).</summary>
    public bool Supprime { get; private set; }

    public void Update(
        string? codeAgence,
        string? intituleAgence,
        string? agenceAbrege,
        string? adresse1,
        string? adresse2,
        string? ville,
        string? pays,
        string? cp,
        string? tel,
        string? fax,
        string? email,
        string? lieuDedouanement,
        decimal? coutAgence)
    {
        CodeAgence = Trim(codeAgence);
        IntituleAgence = Trim(intituleAgence);
        AgenceAbrege = Trim(agenceAbrege);
        Adresse1Agence = Trim(adresse1);
        Adresse2Agence = Trim(adresse2);
        VilleAgence = Trim(ville);
        PaysAgence = Trim(pays);
        CpAgence = Trim(cp);
        TelAgence = Trim(tel);
        FaxAgence = Trim(fax);
        EmailAgence = Trim(email);
        LieuDedouanement = Trim(lieuDedouanement);
        CoutAgence = coutAgence;
    }

    /// <summary>Logical delete — mirrors <c>update t_agence set supp = 1</c>.</summary>
    public void SoftDelete() => Supprime = true;

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Dossier (file) type (<c>t_type_dossier</c>). Drives the customs direction and the nature of
/// service; it is the referential behind <c>t_personnel_type_dossier</c> visibility rights.
/// </summary>
public sealed class TypeDossier : Entity
{
    private TypeDossier() { }

    public TypeDossier(string typeDossier, string? intituleType = null, Sens? sens = null, string? socTypeDossier = null)
    {
        Id = Require(typeDossier, nameof(typeDossier));
        IntituleType = Trim(intituleType);
        Sens = sens;
        SocTypeDossier = Trim(socTypeDossier);
    }

    public string TypeDossierCode => Id;
    public string? IntituleType { get; private set; }
    /// <summary>Import / Export / National.</summary>
    public Sens? Sens { get; private set; }
    public string? NatureService { get; private set; }
    public string? Produit { get; private set; }
    /// <summary>
    /// Which party becomes the booking's <c>key_client</c>: EXPEDITEUR, DESTINATAIRE or PAYEUR.
    /// </summary>
    /// <remarks>
    /// This single column drives the client-attribution rule on bookings, which is why it lives on the
    /// dossier type rather than being a booking-level choice.
    /// </remarks>
    public string? ClientDossier { get; private set; }
    /// <summary>NULL means "shared across all companies".</summary>
    public string? SocTypeDossier { get; private set; }

    public void Update(string? intituleType, Sens? sens, string? natureService, string? produit)
    {
        IntituleType = Trim(intituleType);
        Sens = sens;
        NatureService = Trim(natureService);
        Produit = Trim(produit);
    }

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Payment method (<c>t_mode_paiement</c>). Not merely a label: it carries the <em>default payment
/// status</em> per counterparty kind, which is how the legacy application decides whether a new
/// règlement starts as "Saisie", "Portefeuille", "Signé", … rather than hard-coding it.
/// </summary>
public sealed class ModePaiement : Entity
{
    private ModePaiement() { }

    public ModePaiement(string modePaiement, string? statutClient = null, string? statutFrs = null, string? statutAgent = null)
    {
        Id = Require(modePaiement, nameof(modePaiement));
        StatutClient = Trim(statutClient);
        StatutFrs = Trim(statutFrs);
        StatutAgent = Trim(statutAgent);
    }

    public string ModePaiementCode => Id;
    /// <summary>Default status for a client règlement using this mode.</summary>
    public string? StatutClient { get; private set; }
    public string? StatutFrs { get; private set; }
    public string? StatutAgent { get; private set; }
    public string? CorrespondPaiement { get; private set; }
    public string? CorrespondanceSominfac { get; private set; }

    /// <summary>True for the modes the legacy code treats as immediately banked (ESPECE, VIREMENT).</summary>
    public bool IsImmediate => Id is "ESPECE" or "VIREMENT";

    /// <summary>True when the due date (<c>date_echeance</c>) drives ageing instead of the operation date.</summary>
    public bool UsesEcheance => Id == "TRAITE";

    public void Update(string? statutClient, string? statutFrs, string? statutAgent, string? correspondPaiement)
    {
        StatutClient = Trim(statutClient);
        StatutFrs = Trim(statutFrs);
        StatutAgent = Trim(statutAgent);
        CorrespondPaiement = Trim(correspondPaiement);
    }

    /// <summary>Resolves the default status for a given counterparty kind.</summary>
    public string? DefaultStatutFor(TypeReglement type) => type switch
    {
        TypeReglement.Client => StatutClient,
        TypeReglement.Fournisseur => StatutFrs,
        TypeReglement.Personnel => StatutAgent,
        _ => StatutAgent
    };

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim().ToUpperInvariant();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// VAT code (<c>t_compte_tva</c>), keyed by <c>(code_tva, tva_soc)</c>. Maps a rate code such as
/// "20%" or the special "Débours" to a rate and to the sales/purchase VAT accounts.
/// </summary>
/// <remarks>
/// <see cref="Valeur"/> is a <em>fraction</em> (0.20 for 20%), not a percentage — the legacy
/// accounting integration multiplies line amounts by it directly. The "20%" in <see cref="Id"/> is
/// only a label.
/// </remarks>
public sealed class CompteTva : Entity, ITenantScoped
{
    private CompteTva() { }

    public CompteTva(string codeTva, string societeKey, decimal valeur, string? compteVente = null, string? compteAchat = null)
    {
        Id = Require(codeTva, nameof(codeTva));
        SocieteKey = Require(societeKey, nameof(societeKey));
        Valeur = valeur;
        CompteVente = Trim(compteVente);
        CompteAchat = Trim(compteAchat);
    }

    /// <summary>Composite identity: code + company.</summary>
    public string CodeTva => Id;
    public string SocieteKey { get; private set; } = string.Empty;
    /// <summary>VAT rate as a fraction (0.20 = 20%).</summary>
    public decimal Valeur { get; private set; }
    public string? CompteVente { get; private set; }
    public string? CompteAchat { get; private set; }
    public string? CodeTaxe { get; private set; }
    /// <summary>Whether this rate is offered on sales documents (<c>vente = 1</c>).</summary>
    public bool Vente { get; private set; }

    /// <summary>The special debit/credit-note bucket: VAT is not computed on disbursements.</summary>
    public bool IsDebours => string.Equals(Id, "Debours", StringComparison.OrdinalIgnoreCase);

    public decimal ComputeVat(decimal baseHt) =>
        IsDebours ? 0m : Math.Round(baseHt * Valeur, 2, MidpointRounding.AwayFromZero);

    public void Update(decimal valeur, string? compteVente, string? compteAchat, string? codeTaxe, bool vente)
    {
        Valeur = valeur;
        CompteVente = Trim(compteVente);
        CompteAchat = Trim(compteAchat);
        CodeTaxe = Trim(codeTaxe);
        Vente = vente;
    }

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Nature of cost/fee referential (<c>t_nature_frais</c>).</summary>
public sealed class NatureFrais : Entity, ITenantScoped
{
    private NatureFrais() { }

    public NatureFrais(string libelleNature, string societeKey)
    {
        Id = Require(libelleNature, nameof(libelleNature));
        SocieteKey = Require(societeKey, nameof(societeKey));
    }

    public string LibelleNature => Id;
    public string SocieteKey { get; private set; } = string.Empty;

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Client class referential (<c>t_classe_client</c>). PK is the natural label itself.</summary>
public sealed class ClasseClient : Entity, ITenantScoped
{
    private ClasseClient() { }

    public ClasseClient(string classeClient, string societeKey)
    {
        Id = Require(classeClient, nameof(classeClient));
        SocieteKey = Require(societeKey, nameof(societeKey));
    }

    public string ClasseClientLabel => Id;
    public string SocieteKey { get; private set; } = string.Empty;

    public void Rename(string newLabel) => Id = Require(newLabel, nameof(newLabel));

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();
}

/// <summary>Client status referential (<c>t_statut_client</c>). Global — it has no company column at all.</summary>
public sealed class StatutClient : Entity
{
    private StatutClient() { }

    public StatutClient(string statutClient) => Id = Require(statutClient);

    public string StatutClientLabel => Id;

    public void Rename(string newLabel) => Id = Require(newLabel);

    private static string Require(string value) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Status is required.", nameof(value)) : value.Trim();
}

/// <summary>Client risk level (<c>t_risque</c>). Global; the label is the key.</summary>
public sealed class Risque : Entity
{
    private Risque() { }

    public Risque(string intituleRisque) => Id = Require(intituleRisque);

    public string IntituleRisque => Id;

    public void Rename(string newLabel) => Id = Require(newLabel);

    private static string Require(string value) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Risk label is required.", nameof(value)) : value.Trim();
}

/// <summary>Business sector referential (<c>t_secteur_activite</c>).</summary>
public sealed class SecteurActivite : Entity, ITenantScoped
{
    private SecteurActivite() { }

    public SecteurActivite(string keySecteurActivite, string societeKey)
    {
        Id = Require(keySecteurActivite, nameof(keySecteurActivite));
        SocieteKey = Require(societeKey, nameof(societeKey));
    }

    public string KeySecteurActivite => Id;
    public string SocieteKey { get; private set; } = string.Empty;

    public void Rename(string newLabel) => Id = Require(newLabel, nameof(newLabel));

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();
}

/// <summary>Client group (<c>t_groupe_societe</c>) — used for consolidated reporting.</summary>
public sealed class GroupeSociete : Entity, ITenantScoped
{
    private GroupeSociete() { }

    public GroupeSociete(string libelleGroupe, string societeKey)
    {
        Id = Require(libelleGroupe, nameof(libelleGroupe));
        SocieteKey = Require(societeKey, nameof(societeKey));
    }

    public string LibelleGroupe => Id;
    public string SocieteKey { get; private set; } = string.Empty;

    public void Rename(string newLabel) => Id = Require(newLabel, nameof(newLabel));

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();
}

/// <summary>
/// Treasury account — bank or cash (<c>t_tresorie</c>). The "own account" side of every règlement
/// and bank fee, and the counterparty account in the accounting integration.
/// </summary>
public sealed class Tresorie : Entity, ITenantScoped
{
    private Tresorie() { }

    public Tresorie(string keyTresorie, string societeKey, string? libelleTresorie, TypeTresorie type)
    {
        Id = Require(keyTresorie, nameof(keyTresorie));
        SocieteKey = Require(societeKey, nameof(societeKey));
        LibelleTresorie = Trim(libelleTresorie);
        TypeTresorie = type;
    }

    public string KeyTresorie => Id;
    public string SocieteKey { get; private set; } = string.Empty;
    public string? AgenceTresorie { get; private set; }
    public string? LibelleTresorie { get; private set; }
    public TypeTresorie TypeTresorie { get; private set; }
    public string? AdresseTresorie { get; private set; }
    public string? CompteBanqueTresorie { get; private set; }
    public string? JournalComptabilite { get; private set; }
    /// <summary>When set, the remittance reference is auto-generated (<c>tools.getCompteur(soc,"remise")</c>).</summary>
    public bool GenerationRemise { get; private set; }
    public string? NumCompteComptable { get; private set; }
    public string? IfTresorie { get; private set; }
    public string? IceTresorie { get; private set; }

    public void Update(
        string? libelleTresorie,
        TypeTresorie type,
        string? agenceTresorie,
        string? adresseTresorie,
        string? compteBanqueTresorie,
        string? journalComptabilite,
        bool generationRemise,
        string? numCompteComptable)
    {
        LibelleTresorie = Trim(libelleTresorie);
        TypeTresorie = type;
        AgenceTresorie = Trim(agenceTresorie);
        AdresseTresorie = Trim(adresseTresorie);
        CompteBanqueTresorie = Trim(compteBanqueTresorie);
        JournalComptabilite = Trim(journalComptabilite);
        GenerationRemise = generationRemise;
        NumCompteComptable = Trim(numCompteComptable);
    }

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Bank-fee nature referential (<c>T_NATURE_FRAIS_BANCAIRE</c>) with its default VAT rate and the
/// general-ledger account debited by the accounting integration.
/// </summary>
public sealed class NatureFraisBancaire : Entity, ITenantScoped
{
    /// <summary>Default VAT rate applied to bank fees (the legacy code emits "10%" / 0.10).</summary>
    public const decimal DefaultTauxTva = 0.10m;

    private NatureFraisBancaire() { }

    public NatureFraisBancaire(string natureFrais, string societeKey, decimal? tauxTva = null, string? compteComptable = null)
    {
        Id = Require(natureFrais, nameof(natureFrais));
        SocieteKey = Require(societeKey, nameof(societeKey));
        TauxTva = tauxTva ?? DefaultTauxTva;
        CompteComptable = Trim(compteComptable);
    }

    public string NatureFrais => Id;
    public string SocieteKey { get; private set; } = string.Empty;
    public decimal TauxTva { get; private set; }
    public string? CompteComptable { get; private set; }

    public void Update(decimal tauxTva, string? compteComptable)
    {
        TauxTva = tauxTva;
        CompteComptable = Trim(compteComptable);
    }

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Payment-incident reason referential (<c>T_TYPE_INCIDENT_PAIEMENT</c>), written into
/// <c>t_entete_reglement.motif_impaye</c> when a payment bounces.
/// </summary>
public sealed class TypeIncidentPaiement : Entity, ITenantScoped
{
    private TypeIncidentPaiement() { }

    public TypeIncidentPaiement(string libelleIncident, string societeKey)
    {
        Id = Require(libelleIncident, nameof(libelleIncident));
        SocieteKey = Require(societeKey, nameof(societeKey));
    }

    public string LibelleIncident => Id;
    public string SocieteKey { get; private set; } = string.Empty;

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();
}

/// <summary>
/// Assigned-amount (COD) referential (<c>t_cod</c>).
/// </summary>
/// <remarks>
/// <para>
/// The key is the free-text label itself (<c>intitule_cod</c>), and <see cref="CodEdi"/> is the code an
/// inbound EDI file carries. The special file value <c>"10"</c> means <b>no COD at all</b>, which is why
/// the resolution treats it as an absence rather than a lookup.
/// </para>
/// <para>
/// It also backs <c>T_COD_DOSSIER</c> on the dossier side, so it is a genuine referential and not an
/// EDI-only artefact.
/// </para>
/// </remarks>
public sealed class Cod : Entity
{
    private Cod() { }

    public Cod(string intituleCod, string? codEdi = null)
    {
        Id = Require(intituleCod, nameof(intituleCod));
        CodEdi = Trim(codEdi);
    }

    public string IntituleCod => Id;
    /// <summary>The code the inbound file carries for this COD.</summary>
    public string? CodEdi { get; private set; }

    public void SetCodEdi(string? codEdi) => CodEdi = Trim(codEdi);

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
