using Logistics.Domain.Common;
using Logistics.Domain.Entities.ReferenceData;
using Logistics.Domain.Enums;

namespace Logistics.Domain.Entities.Clients;

/// <summary>
/// Client master data (<c>t_clients</c>) — one row per billable customer per company.
/// </summary>
/// <remarks>
/// The tenant column is <c>soc_client</c>, not <c>societe</c>. Deletion in the legacy application is
/// never a cascade: it <em>refuses</em> the delete while the client is referenced by invoices,
/// dossiers, règlements, contacts, banks, dunning actions or VAT exemptions, and only then applies
/// the <c>supprimer = 1</c> soft-delete flag.
/// </remarks>
public sealed class Client : Entity, ITenantScoped
{
    private readonly List<AdresseClient> _adresses = [];
    private readonly List<TiersContact> _contacts = [];
    private readonly List<BanqueTiers> _banques = [];

    private Client() { }

    public Client(string keyClient, string societeKey, string raisonSociale, string? paysClient = null)
    {
        Id = Require(keyClient, nameof(keyClient));
        SocieteKey = Require(societeKey, nameof(societeKey));
        RaisonSociale = Require(raisonSociale, nameof(raisonSociale));
        PaysClient = Trim(paysClient);
        Actif = false;
        Supprime = false;
        Valide = false;
    }

    public string KeyClient => Id;
    public string SocieteKey { get; private set; } = string.Empty;

    /// <summary>Company name. Uniqueness is enforced per company on the space-stripped value.</summary>
    public string RaisonSociale { get; private set; } = string.Empty;
    /// <summary>External/logistics reference; also populated by the Globelink EDI import.</summary>
    public string? RefClient { get; private set; }

    // Classification
    public string? TypeClient { get; private set; }
    public string? StatutClient { get; private set; }
    public string? GroupeClient { get; private set; }
    public string? ClasseClient { get; private set; }
    public string? RisqueClient { get; private set; }
    public string? ActiviteClient { get; private set; }
    public string? DetailActivite { get; private set; }

    // Commercial ownership
    public string? Commercial { get; private set; }
    public string? AgenceCommercial { get; private set; }
    public string? AgenceExploitation { get; private set; }
    public string? TypeAffectCommercial { get; private set; }
    public string? KeyChargeClientele { get; private set; }
    /// <summary>Mirrors the RECOUVREMENT row of <c>t_tache_personnel_client</c>; nulled on removal.</summary>
    public string? AgentRecouvrement { get; private set; }

    // Address
    public string? Adresse1Client { get; private set; }
    public string? Adresse2Client { get; private set; }
    public string? CpClient { get; private set; }
    public string? VilleClient { get; private set; }
    public string? PaysClient { get; private set; }
    public string? DepartementClient { get; private set; }
    public string? RegionClient { get; private set; }
    public string? LangueClient { get; private set; }
    public string? TelClient { get; private set; }
    public string? FaxClient { get; private set; }
    public string? SiteWebClient { get; private set; }
    public int? AnneeCreation { get; private set; }
    public string? ObservationClient { get; private set; }

    // Legal identifiers. Legacy rule: when the country is not MAROC these must ALL be empty.
    public string? RcClient { get; private set; }
    public string? CentreRcClient { get; private set; }
    public string? PatenteClient { get; private set; }
    public string? IfClient { get; private set; }
    public string? CnssClient { get; private set; }
    public string? IceClient { get; private set; }

    // Commercial terms
    public string? DeviseFacturation { get; private set; }
    public string? PaiementClient { get; private set; }
    /// <summary>Base of the due-date calculation — a label, not a number.</summary>
    public string? Echeance { get; private set; }
    public int? NbEcheance { get; private set; }
    public bool FinMois { get; private set; }
    public decimal? Plafond { get; private set; }

    // Accounting
    public string? CompteComptable { get; private set; }
    public string? JournalVente { get; private set; }
    public string? JournalDebours { get; private set; }
    public PeriodiciteReleve ReleveFacture { get; private set; }
    public int? NbFactureReleve { get; private set; }
    /// <summary>Retenue à la source (IS).</summary>
    public bool Ras { get; private set; }

    // State
    public bool Valide { get; private set; }
    public bool Actif { get; private set; }
    /// <summary>Whether any document is attached. Maintained by the document module.</summary>
    public bool Attachement { get; private set; }
    public bool Supprime { get; private set; }

    /// <summary>
    /// Derived cache of the natures of service this client has subscribed, joined with '/'.
    /// Rebuilt from <c>t_services_client</c> after every change — never edited directly.
    /// </summary>
    public string? ServiceAcquis { get; private set; }

    public string? LieuDedouanement { get; private set; }
    public int? NbJrFranchise { get; private set; }
    public decimal? ImmobilisationMead { get; private set; }
    public decimal? ImmobilisationPort { get; private set; }
    public bool SuiviAt { get; private set; }
    public DateTime? DateCreation { get; private set; }
    /// <summary>Written only by the accounting (Sage) lettering synchronisation.</summary>
    public string? CodeLettrage { get; private set; }

    public IReadOnlyCollection<AdresseClient> Adresses => _adresses.AsReadOnly();
    public IReadOnlyCollection<TiersContact> Contacts => _contacts.AsReadOnly();
    public IReadOnlyCollection<BanqueTiers> Banques => _banques.AsReadOnly();

    /// <summary>Country whose legal identifiers (RC/IF/Patente/CNSS/ICE) the legacy UI requires.</summary>
    public const string IdentifiersRequiredCountry = "MAROC";

    /// <summary>
    /// True when the client is outside <see cref="IdentifiersRequiredCountry"/> and therefore must not
    /// carry Moroccan legal identifiers.
    /// </summary>
    public bool IsForeign => !string.Equals(PaysClient, IdentifiersRequiredCountry, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Applies the full "onglet principal" payload. Kept as one operation because the legacy screen
    /// saves the fiche as a unit.
    /// </summary>
    public void Update(ClientDetails details)
    {
        RaisonSociale = Require(details.RaisonSociale, nameof(details.RaisonSociale));
        RefClient = Trim(details.RefClient);
        TypeClient = Trim(details.TypeClient);
        StatutClient = Trim(details.StatutClient);
        GroupeClient = Trim(details.GroupeClient);
        ClasseClient = Trim(details.ClasseClient);
        RisqueClient = Trim(details.RisqueClient);
        ActiviteClient = Trim(details.ActiviteClient);
        DetailActivite = Trim(details.DetailActivite);

        Commercial = Trim(details.Commercial);
        AgenceCommercial = Trim(details.AgenceCommercial);
        AgenceExploitation = Trim(details.AgenceExploitation);
        TypeAffectCommercial = Trim(details.TypeAffectCommercial);
        KeyChargeClientele = Trim(details.KeyChargeClientele);

        Adresse1Client = Trim(details.Adresse1Client);
        Adresse2Client = Trim(details.Adresse2Client);
        CpClient = Trim(details.CpClient);
        VilleClient = Trim(details.VilleClient);
        PaysClient = Trim(details.PaysClient);
        DepartementClient = Trim(details.DepartementClient);
        RegionClient = Trim(details.RegionClient);
        LangueClient = Trim(details.LangueClient);
        TelClient = Trim(details.TelClient);
        FaxClient = Trim(details.FaxClient);
        SiteWebClient = Trim(details.SiteWebClient);
        AnneeCreation = details.AnneeCreation;
        ObservationClient = Trim(details.ObservationClient);

        RcClient = Trim(details.RcClient);
        CentreRcClient = Trim(details.CentreRcClient);
        PatenteClient = Trim(details.PatenteClient);
        IfClient = Trim(details.IfClient);
        CnssClient = Trim(details.CnssClient);
        IceClient = Trim(details.IceClient);

        DeviseFacturation = Trim(details.DeviseFacturation);
        PaiementClient = Trim(details.PaiementClient);
        Echeance = Trim(details.Echeance);
        NbEcheance = details.NbEcheance;
        FinMois = details.FinMois;
        Plafond = details.Plafond;

        JournalVente = Trim(details.JournalVente);
        JournalDebours = Trim(details.JournalDebours);
        ReleveFacture = details.ReleveFacture;
        NbFactureReleve = details.NbFactureReleve;
        Ras = details.Ras;

        LieuDedouanement = Trim(details.LieuDedouanement);
        NbJrFranchise = details.NbJrFranchise;
        ImmobilisationMead = details.ImmobilisationMead;
        ImmobilisationPort = details.ImmobilisationPort;
        SuiviAt = details.SuiviAt;
    }

    /// <summary>Sets the general-ledger account. Uniqueness is checked by the service layer.</summary>
    public void SetCompteComptable(string? compteComptable) => CompteComptable = Trim(compteComptable);

    /// <summary>
    /// Sets only the client reference.
    /// </summary>
    /// <remarks>
    /// Exists because the EDI import overwrites exactly this one field on the client master
    /// (<c>frm_Integration_EDI_IMPORT_STC.vb:1130</c>). <see cref="Update"/> would do it, but only by replacing
    /// every other detail as well — which an import must not do.
    /// </remarks>
    public void DefinirRefClient(string? refClient) => RefClient = Trim(refClient);

    /// <summary>
    /// Validates the fiche. Reproduces the legacy guard: when the company does not auto-generate
    /// ledger accounts and accounting transfer is enabled, a ledger account is mandatory.
    /// </summary>
    public void Validate(bool companyGeneratesComptes, bool transfertComptabilite)
    {
        if (IsForeign && HasMoroccanIdentifiers())
        {
            throw new BusinessRuleException(
                "client.identifiers",
                $"Un client dont le pays n'est pas '{IdentifiersRequiredCountry}' ne doit porter aucun identifiant "
                + "marocain (RC, Centre RC, Patente, IF, CNSS, ICE).");
        }

        if (!companyGeneratesComptes && transfertComptabilite && string.IsNullOrWhiteSpace(CompteComptable))
        {
            throw new BusinessRuleException(
                "client.compte_comptable",
                "Un compte comptable est obligatoire pour valider ce client.");
        }

        Valide = true;
    }

    /// <summary>Soft delete — mirrors <c>update t_clients set supprimer = '1'</c>.</summary>
    public void SoftDelete()
    {
        if (Valide)
        {
            throw new BusinessRuleException("client.delete.validated", "Un client validé ne peut pas être supprimé.");
        }

        Supprime = true;
    }

    public void Activate() => Actif = true;
    public void Deactivate() => Actif = false;

    /// <summary>Assigns the recouvrement agent, mirroring the RECOUVREMENT task row.</summary>
    public void AssignRecouvrementAgent(string? keyPersonnel) => AgentRecouvrement = Trim(keyPersonnel);

    public void SetAttachement(bool attachement) => Attachement = attachement;

    /// <summary>Rebuilds the denormalised <see cref="ServiceAcquis"/> from the subscribed services.</summary>
    public void RebuildServiceAcquis(IEnumerable<string> naturesService)
    {
        var ordered = naturesService
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        ServiceAcquis = ordered.Length == 0 ? null : string.Join('/', ordered);
    }

    private bool HasMoroccanIdentifiers() =>
        !string.IsNullOrWhiteSpace(RcClient)
        || !string.IsNullOrWhiteSpace(CentreRcClient)
        || !string.IsNullOrWhiteSpace(PatenteClient)
        || !string.IsNullOrWhiteSpace(IfClient)
        || !string.IsNullOrWhiteSpace(CnssClient)
        || !string.IsNullOrWhiteSpace(IceClient);

    internal static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", param)
            : value.Trim();

    internal static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Mutable payload for <see cref="Client.Update"/>. Modelled as a record so the service layer can
/// bind it straight from the request DTO without a long positional constructor.
/// </summary>
public sealed record ClientDetails
{
    public required string RaisonSociale { get; init; }
    public string? RefClient { get; init; }
    public string? TypeClient { get; init; }
    public string? StatutClient { get; init; }
    public string? GroupeClient { get; init; }
    public string? ClasseClient { get; init; }
    public string? RisqueClient { get; init; }
    public string? ActiviteClient { get; init; }
    public string? DetailActivite { get; init; }
    public string? Commercial { get; init; }
    public string? AgenceCommercial { get; init; }
    public string? AgenceExploitation { get; init; }
    public string? TypeAffectCommercial { get; init; }
    public string? KeyChargeClientele { get; init; }
    public string? Adresse1Client { get; init; }
    public string? Adresse2Client { get; init; }
    public string? CpClient { get; init; }
    public string? VilleClient { get; init; }
    public string? PaysClient { get; init; }
    public string? DepartementClient { get; init; }
    public string? RegionClient { get; init; }
    public string? LangueClient { get; init; }
    public string? TelClient { get; init; }
    public string? FaxClient { get; init; }
    public string? SiteWebClient { get; init; }
    public int? AnneeCreation { get; init; }
    public string? ObservationClient { get; init; }
    public string? RcClient { get; init; }
    public string? CentreRcClient { get; init; }
    public string? PatenteClient { get; init; }
    public string? IfClient { get; init; }
    public string? CnssClient { get; init; }
    public string? IceClient { get; init; }
    public string? DeviseFacturation { get; init; }
    public string? PaiementClient { get; init; }
    public string? Echeance { get; init; }
    public int? NbEcheance { get; init; }
    public bool FinMois { get; init; }
    public decimal? Plafond { get; init; }
    public string? JournalVente { get; init; }
    public string? JournalDebours { get; init; }
    public PeriodiciteReleve ReleveFacture { get; init; }
    public int? NbFactureReleve { get; init; }
    public bool Ras { get; init; }
    public string? LieuDedouanement { get; init; }
    public int? NbJrFranchise { get; init; }
    public decimal? ImmobilisationMead { get; init; }
    public decimal? ImmobilisationPort { get; init; }
    public bool SuiviAt { get; init; }
}

/// <summary>
/// Additional delivery address (<c>t_adresse_client</c>). No tenant column: scoping comes from the
/// parent client.
/// </summary>
public sealed class AdresseClient : Entity
{
    private AdresseClient() { }

    public AdresseClient(string keyAdresse, string keyClient, string? adresse1, string? adresse2, string ville, string? pays)
    {
        Id = Client.Require(keyAdresse, nameof(keyAdresse));
        KeyClient = Client.Require(keyClient, nameof(keyClient));

        if (string.IsNullOrWhiteSpace(adresse1) && string.IsNullOrWhiteSpace(adresse2))
        {
            throw new BusinessRuleException("adresse.required", "Vous devez spécifier une adresse de livraison.");
        }

        Adresse1 = Client.Trim(adresse1);
        Adresse2 = Client.Trim(adresse2);
        Ville = Client.Require(ville, nameof(ville));
        Pays = Client.Trim(pays);
        Type = "Livraison";
    }

    public string KeyAdresse => Id;
    public string KeyClient { get; private set; } = string.Empty;
    public string? Adresse1 { get; private set; }
    public string? Adresse2 { get; private set; }
    public string? Cp { get; private set; }
    public string Ville { get; private set; } = string.Empty;
    public string? Pays { get; private set; }
    public string? TelAdresse { get; private set; }
    public string? FaxAdresse { get; private set; }
    public string? Contact { get; private set; }
    /// <summary>
    /// Address kind. The legacy form never writes it but refuses to delete a row whose value is
    /// "Principal".
    /// </summary>
    public string? Type { get; private set; }

    /// <summary>The principal address cannot be deleted from the client screen.</summary>
    public bool IsPrincipal => string.Equals(Type, "Principal", StringComparison.OrdinalIgnoreCase);

    public void Update(string? adresse1, string? adresse2, string? cp, string ville, string? pays, string? tel, string? fax, string? contact)
    {
        if (string.IsNullOrWhiteSpace(adresse1) && string.IsNullOrWhiteSpace(adresse2))
        {
            throw new BusinessRuleException("adresse.required", "Vous devez spécifier une adresse de livraison.");
        }

        Adresse1 = Client.Trim(adresse1);
        Adresse2 = Client.Trim(adresse2);
        Cp = Client.Trim(cp);
        Ville = Client.Require(ville, nameof(ville));
        Pays = Client.Trim(pays);
        TelAdresse = Client.Trim(tel);
        FaxAdresse = Client.Trim(fax);
        Contact = Client.Trim(contact);
    }

    private static string Require(string value, string param) => Client.Require(value, param);
}

/// <summary>
/// Contact of any third party (<c>t_contact_tiers</c>) — polymorphic across clients, suppliers,
/// agents and generic tiers. Replaces the non-existent <c>t_contact_client</c>.
/// </summary>
public sealed class TiersContact : Entity
{
    private TiersContact() { }

    public TiersContact(string keyContact, string tiersContact, TypeTiers typeTiers, string nomPrenom, string? keyFonctionContact = null)
    {
        Id = Client.Require(keyContact, nameof(keyContact));
        TiersContactKey = Client.Require(tiersContact, nameof(tiersContact));
        TypeTiers = typeTiers;
        NomPrenom = Client.Require(nomPrenom, nameof(nomPrenom));
        KeyFonctionContact = Client.Trim(keyFonctionContact);
    }

    public string KeyContact => Id;
    /// <summary>Polymorphic owner key; resolved through <see cref="TypeTiers"/>.</summary>
    public string TiersContactKey { get; private set; } = string.Empty;
    public TypeTiers TypeTiers { get; private set; }
    public string NomPrenom { get; private set; } = string.Empty;
    public string? KeyFonctionContact { get; private set; }
    public string? EmailContact { get; private set; }
    public string? TelContact { get; private set; }
    public string? FaxContact { get; private set; }
    public string? GsmContact { get; private set; }
    /// <summary>Access-web account state.</summary>
    public bool CompteActive { get; private set; }
    /// <summary>Access-web tracking password, stored encrypted by the legacy application.</summary>
    public string? MotPasseTrack { get; private set; }

    public void Update(string nomPrenom, string? keyFonctionContact, string? email, string? tel, string? fax, string? gsm)
    {
        NomPrenom = Client.Require(nomPrenom, nameof(nomPrenom));
        KeyFonctionContact = Client.Trim(keyFonctionContact);
        EmailContact = Client.Trim(email);
        TelContact = Client.Trim(tel);
        FaxContact = Client.Trim(fax);
        GsmContact = Client.Trim(gsm);
    }

    public void SetAccessWeb(bool active, string? motPasseTrack)
    {
        CompteActive = active;
        MotPasseTrack = Client.Trim(motPasseTrack);
    }

    private static string Require(string value, string param) => Client.Require(value, param);
}

/// <summary>
/// Bank account (RIB) of any third party (<c>t_banque_tiers</c>). Composite key
/// <c>(key_banque, key_tier)</c>; polymorphic via <see cref="TypeTier"/>.
/// </summary>
public sealed class BanqueTiers : Entity
{
    private BanqueTiers() { }

    public BanqueTiers(string keyBanque, string keyTier, TypeTiers typeTier, string? banque, string? compteRib)
    {
        Id = Client.Require(keyBanque, nameof(keyBanque));
        KeyTier = Client.Require(keyTier, nameof(keyTier));
        TypeTier = typeTier;
        Banque = Client.Trim(banque);
        CompteRib = Client.Trim(compteRib);
    }

    public string KeyBanque => Id;
    /// <summary>Second half of the composite key: the owning tiers.</summary>
    public string KeyTier { get; private set; } = string.Empty;
    public TypeTiers TypeTier { get; private set; }
    public string? Banque { get; private set; }
    /// <summary>RIB. Globally unique per the legacy <c>Existe("compte_rib", …)</c> check.</summary>
    public string? CompteRib { get; private set; }
    /// <summary>Despite the name, the legacy UI fills this with the branch ("Agence").</summary>
    public string? CodeBanque { get; private set; }
    /// <summary>Despite the name, the legacy UI fills this with the town.</summary>
    public string? AdresseBanque { get; private set; }
    public string? Swift { get; private set; }
    public string? IbanCode { get; private set; }

    public void Update(string? banque, string? compteRib, string? codeBanque, string? adresseBanque, string? swift, string? iban)
    {
        Banque = Client.Trim(banque);
        CompteRib = Client.Trim(compteRib);
        CodeBanque = Client.Trim(codeBanque);
        AdresseBanque = Client.Trim(adresseBanque);
        Swift = Client.Trim(swift);
        IbanCode = Client.Trim(iban);
    }

    private static string Require(string value, string param) => Client.Require(value, param);
}
