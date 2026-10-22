using Logistics.Domain.Common;
using Logistics.Domain.Entities.ReferenceData;

namespace Logistics.Domain.Entities.Shipments;

/// <summary>
/// Voyage (trip) type. <c>t_type_dossier</c> doubles as the voyage-type referential; the codes are
/// hard-coded in dozens of legacy queries (<c>EL</c>, <c>LV</c>, <c>NV</c>, <c>MD</c>, <c>TN</c>,
/// <c>TD</c>, <c>AC</c>, <c>VI</c>, <c>VE</c>, <c>AF</c>).
/// </summary>
public static class TypeVoyageCatalogue
{
    /// <summary>Codes the legacy lists always exclude from the international voyage screens.</summary>
    public static readonly string[] Excluded =
        ["EL", "LV", "NV"];

    public static readonly string[] ExcludedWithMead =
        ["MD", "EL", "LV", "NV"];

    public const string National = "TN";
    public const string Service = "TD";
    public const string Acquit = "AC";
    public const string Previsionnel = "AF";
    public const string Import = "VI";
    public const string Export = "VE";
}

/// <summary>How the cargo is consolidated (<c>t_voyages.nature_voyage</c>).</summary>
public enum NatureVoyage
{
    Complet,
    Groupage,
    GroupageConfrere
}

/// <summary>Voyage category (<c>t_voyages.categorie_voyage</c>).</summary>
public enum CategorieVoyage
{
    International,
    Previsionnel,
    Enlevement,
    Autre,
    Mead
}

/// <summary>Reception state of a voyage (<c>t_voyages.statut_reception</c>).</summary>
public enum StatutReception
{
    Receptionne,
    AttenteReception
}

/// <summary>Invoicing state of a dossier (<c>t_dossiers.statut_facturation</c>).</summary>
public enum StatutFacturation
{
    ANePasFacturer,
    NonFacture,
    Facture
}

/// <summary>Closing state of a dossier (<c>t_dossiers.statut_cloture</c>).</summary>
public enum StatutClotureDossier
{
    Encours,
    Cloture
}

/// <summary>Equipment line kind (<c>t_equipement_voyage.type_equipement</c>).</summary>
public enum TypeEquipement
{
    Remorque,
    Tracteur,
    Caisse,
    Lcl
}

/// <summary>Place where a dossier was unpacked (<c>t_dossiers.lieu_depotage</c>).</summary>
public enum LieuDepotage
{
    Remorque,
    Tc
}

/// <summary>
/// Referential of event/status codes (<c>t_statut</c>).
/// </summary>
/// <remarks>
/// System codes are prefixed <c>$</c> and are hidden from user selection and protected from deletion
/// in the legacy UI. <see cref="SocieteKey"/> is nullable on purpose: a NULL means the code is global.
/// </remarks>
public sealed class Statut : Entity, ITenantScoped
{
    private Statut() { }

    public Statut(string keyStatut, string libelleStatut, string? utilisation = null, string? societeKey = null)
    {
        Id = Require(keyStatut, nameof(keyStatut));
        LibelleStatut = Require(libelleStatut, nameof(libelleStatut));
        Utilisation = Trim(utilisation);
        SocieteKey = string.IsNullOrWhiteSpace(societeKey) ? string.Empty : societeKey.Trim();
        Supprime = false;
    }

    public string KeyStatut => Id;
    public string LibelleStatut { get; private set; } = string.Empty;
    public string? TypeStatut { get; private set; }
    /// <summary>Scope: 'Voyage', 'Dossier', 'Booking', 'Personnel', 'Matériel', …</summary>
    public string? Utilisation { get; private set; }
    /// <summary>Empty means the status is shared across every company.</summary>
    public string SocieteKey { get; private set; } = string.Empty;
    public bool DocObligatoire { get; private set; }
    public bool QteObligatoire { get; private set; }
    public bool Supprime { get; private set; }

    /// <summary>True for the <c>$</c>-prefixed system events the legacy UI never offers to users.</summary>
    public bool IsSystem => Id.StartsWith('$');

    public void Update(string libelleStatut, string? utilisation, string? typeStatut)
    {
        LibelleStatut = Require(libelleStatut, nameof(libelleStatut));
        Utilisation = Trim(utilisation);
        TypeStatut = Trim(typeStatut);
    }

    public void SoftDelete() => Supprime = true;

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Marker recording that a cost operation has already been computed for a voyage
/// (<c>t_cout_operation_voyage_calcule</c>).
/// </summary>
/// <remarks>
/// This table has <b>exactly two columns</b> — <c>key_voyage</c> and <c>operation</c> — and holds no
/// amount. It exists purely so the cost generator can skip work it has already done
/// (<c>key_voyage not in (select key_voyage from t_cout_operation_voyage_calcule where operation=…)</c>).
/// The money lives in <c>T_REPARTITION_DOSSIER</c>. Recomputing means deleting the marker and the
/// matching allocation rows, then regenerating.
/// </remarks>
public sealed class CoutOperationVoyageCalcule : Entity
{
    /// <summary>
    /// The complete set of operation labels the legacy purge covers. Note that
    /// <c>Frais Autoroute</c> and <c>Frais Gasoil</c> are deliberately absent: they are purged
    /// separately by their own screens.
    /// </summary>
    public static readonly string[] StandardOperations =
    [
        "Coût Société",
        "Coût Tracteur",
        "Coût Remorque",
        "Coût Chauffeur",
        "Coût Assurance Marchandise",
        "Coût MEAD",
        "Coût Agence",
        "Coût Frais Bancaire",
        "Coût Gasoil",
        "Coût AdBlue",
        "Coût Tracteur sans Trajet",
        "Coût Remorque sans Trajet"
    ];

    public const string FraisAutoroute = "Frais Autoroute";
    public const string FraisGasoil = "Frais Gasoil";

    private CoutOperationVoyageCalcule() { }

    public CoutOperationVoyageCalcule(string keyVoyage, string operation)
    {
        Id = $"{keyVoyage}:{operation}";
        KeyVoyage = Require(keyVoyage, nameof(keyVoyage));
        Operation = Require(operation, nameof(operation));
    }

    public string KeyVoyage { get; private set; } = string.Empty;
    public string Operation { get; private set; } = string.Empty;

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();
}

/// <summary>
/// Voyage / trip master (<c>t_voyages</c>). The tenant column is <c>soc_voyage</c>.
/// </summary>
/// <remarks>
/// <para>
/// Two structural facts drive this entity. First, <see cref="DateVoyage"/> being NULL is the
/// application's definition of "not yet validated": every cost-generation query filters on
/// <c>date_voyage is not null</c>. Second, setting <see cref="DateCloture"/> is what closes the
/// voyage, and closing is rejected while any attached dossier is still awaiting invoicing.
/// </para>
/// <para>
/// Ports and vessels are stored as free-text <em>labels</em>, not keys — the legacy lists join
/// <c>t_voyages.port_depart</c> to <c>T_PORT.LIBELLE_PORT</c> rather than to <c>code_port</c>.
/// That is modelled faithfully.
/// </para>
/// </remarks>
public sealed class Voyage : Entity, ITenantScoped
{
    public const string StatutCree = "$VOYC";
    public const string StatutValide = "$VOYV";
    public const string StatutCloture = "$VOYCL";
    public const string StatutSupprime = "$SVOY";

    public const string StatutMeadAttenteEntree = "$ATEMEAD";
    public const string LibelleMeadAttenteEntree = "Attente Entrée MEAD";
    public const string StatutMeadEncoursDepotage = "$EDEPOT";
    public const string StatutMeadDepote = "$DEPOT";

    private readonly List<TrajetVoyage> _trajets = [];
    private readonly List<EquipementVoyage> _equipements = [];

    private Voyage() { }

    public Voyage(string keyVoyage, string societeKey, string? typeVoyage, string? numVoyage, DateTime dateCreation)
    {
        Id = Require(keyVoyage, nameof(keyVoyage));
        SocieteKey = Require(societeKey, nameof(societeKey));
        TypeVoyage = Trim(typeVoyage);
        NumVoyage = Trim(numVoyage);
        DateCreation = dateCreation;
        KeyStatutVoyage = StatutCree;
        DateStatutVoyage = dateCreation;
        CategorieVoyage = CategorieVoyage.International;
        Supprime = false;
    }

    public string KeyVoyage => Id;
    public string SocieteKey { get; private set; } = string.Empty;
    public DateTime DateCreation { get; private set; }
    public string? NumVoyage { get; private set; }
    /// <summary>FK to <c>t_type_dossier.type_dossier</c>; also the voyage-type referential.</summary>
    public string? TypeVoyage { get; private set; }
    public NatureVoyage? NatureVoyage { get; private set; }
    public CategorieVoyage CategorieVoyage { get; private set; }
    public string? ServiceVoyage { get; private set; }

    public string? KeyAgenceSaisie { get; private set; }
    public string? IntituleAgenceSaisie { get; private set; }
    public string? KeyAgenceExploitation { get; private set; }
    public string? IntituleAgenceExploitation { get; private set; }

    public DateTime? DateDepartPrevu { get; private set; }
    public DateTime? DateArriveePrevu { get; private set; }
    public DateTime? DateDepart { get; private set; }
    public DateTime? DateArrivee { get; private set; }
    public DateTime? DateDepartTrans { get; private set; }
    public DateTime? DateArriveeTrans { get; private set; }
    public string? PortTransbordement { get; private set; }

    /// <summary>Free-text port <em>label</em>, not a <c>t_port</c> key.</summary>
    public string? PortDepart { get; private set; }
    public string? PortArrivee { get; private set; }
    public string? NavireDepart { get; private set; }
    public string? NavireArrivee { get; private set; }
    public string? Provenance { get; private set; }
    public string? Destination { get; private set; }

    public string? KeyAgent { get; private set; }
    public string? IntituleAgent { get; private set; }
    public string? RefAgent { get; private set; }
    public string? KeyCompagnie { get; private set; }
    public string? IntituleCompagnie { get; private set; }
    public string? RefCompagnie { get; private set; }
    public string? KeyConsignataire { get; private set; }
    public string? IntituleConsignataire { get; private set; }
    public string? RefConsignataire { get; private set; }
    public string? KeyTractionnaire { get; private set; }
    public string? IntituleTractionnaire { get; private set; }
    public string? RefTractionnaire { get; private set; }
    public string? KeyTransporteurInter { get; private set; }
    public string? IntituleTransporteurInter { get; private set; }
    public string? RefTransporteurInter { get; private set; }

    public string? KeyExploitant { get; private set; }
    public string? IntituleExploitant { get; private set; }
    public string? KeyChauffeur1 { get; private set; }
    public string? IntituleChauffeur1 { get; private set; }
    public string? KeyChauffeur2 { get; private set; }
    public string? IntituleChauffeur2 { get; private set; }

    public string? NumAcquit { get; private set; }
    public DateTime? DateAcquit { get; private set; }
    public string? NumManifeste { get; private set; }
    public DateTime? DateManifeste { get; private set; }
    public string? NumDs { get; private set; }
    public DateTime? DateDs { get; private set; }
    public string? NumCnt { get; private set; }
    public string? NumTriptyque { get; private set; }
    public DateTime? DateTriptyque { get; private set; }

    public string? Commentaire { get; private set; }
    public string? ModeTraitement { get; private set; }

    /// <summary>Status code joining <c>t_statut.key_statut</c>. Defaults to <c>$VOYC</c>.</summary>
    public string KeyStatutVoyage { get; private set; } = StatutCree;
    public DateTime? DateStatutVoyage { get; private set; }
    public string? CommentaireStatutVoyage { get; private set; }

    /// <summary>MEAD (bonded warehouse) workflow status, set when any dossier has a customs warehouse.</summary>
    public string? KeyStatutVoyageMead { get; private set; }
    public string? LibelleStatutVoyageMead { get; private set; }
    public DateTime? DateDernierEvenementMead { get; private set; }
    public string? CommentaireDernierEvenementMead { get; private set; }
    public DateTime? EntreeMead { get; private set; }
    public string? MagasinMead { get; private set; }

    // Counts used by the costing engine: the divisor for the equal split.
    public decimal? NbDossVoyageSaisi { get; private set; }
    public decimal? TotalPoidsBrutSaisi { get; private set; }
    public decimal? TotalNbColisSaisi { get; private set; }

    public bool TemperatureDirigee { get; private set; }
    public int? NbJourFranchise { get; private set; }
    public StatutReception? StatutReception { get; private set; }

    /// <summary>
    /// NULL until the voyage is validated. Cost generation and every reporting query depend on this,
    /// which is why validation is an explicit operation rather than a flag.
    /// </summary>
    public DateTime? DateVoyage { get; private set; }

    public bool Supprime { get; private set; }
    public bool Attachement { get; private set; }
    public decimal? Tare { get; private set; }
    public string? NumCaisse { get; private set; }
    /// <summary>Non-null means the voyage is closed.</summary>
    public DateTime? DateCloture { get; private set; }
    public DateTime? DatePremiereFacture { get; private set; }

    public string? KeyBonCommande { get; private set; }
    public string? KeyVoyageEnlevementLivraison { get; private set; }
    public string? KeyDepart { get; private set; }
    public string? NumBooking { get; private set; }
    public string? NumRemorque { get; private set; }
    public string? NumTracteur { get; private set; }
    public string? NumTc { get; private set; }
    public string? LocaliteDepart { get; private set; }
    public string? LocaliteArrivee { get; private set; }
    public string? Client { get; private set; }
    public string? TypeOperation { get; private set; }
    public decimal? CapaciteRemorque { get; private set; }

    public DateTime? DateDebutDepotage { get; private set; }
    public DateTime? DateFinDepotage { get; private set; }
    public string? DepotePar { get; private set; }
    public string? ChargerMagasin { get; private set; }

    public DateTime? DateEchangeCompagnie { get; private set; }
    public DateTime? DatePrevisionEchange { get; private set; }

    public IReadOnlyCollection<TrajetVoyage> Trajets => _trajets.AsReadOnly();
    public IReadOnlyCollection<EquipementVoyage> Equipements => _equipements.AsReadOnly();

    public bool IsValide => DateVoyage.HasValue;
    public bool IsCloture => DateCloture.HasValue;

    /// <summary>True when the voyage carries a customs-warehouse status and must follow the MEAD flow.</summary>
    public bool IsMead => !string.IsNullOrWhiteSpace(KeyStatutVoyageMead);

    public void UpdateEntete(
        string? numVoyage,
        string? typeVoyage,
        NatureVoyage? natureVoyage,
        CategorieVoyage categorie,
        string? serviceVoyage,
        string? provenance,
        string? destination,
        string? commentaire)
    {
        NumVoyage = Trim(numVoyage);
        TypeVoyage = Trim(typeVoyage);
        NatureVoyage = natureVoyage;
        CategorieVoyage = categorie;
        ServiceVoyage = Trim(serviceVoyage);
        Provenance = Trim(provenance);
        Destination = Trim(destination);
        Commentaire = Trim(commentaire);
    }

    public void SetAgences(string? keySaisie, string? intituleSaisie, string? keyExploitation, string? intituleExploitation)
    {
        KeyAgenceSaisie = Trim(keySaisie);
        IntituleAgenceSaisie = Trim(intituleSaisie);
        KeyAgenceExploitation = Trim(keyExploitation);
        IntituleAgenceExploitation = Trim(intituleExploitation);
    }

    public void SetItineraire(
        DateTime? dateDepartPrevu, DateTime? dateArriveePrevu, DateTime? dateDepart, DateTime? dateArrivee,
        string? portDepart, string? portArrivee, string? navireDepart, string? navireArrivee,
        DateTime? dateDepartTrans, DateTime? dateArriveeTrans, string? portTransbordement)
    {
        DateDepartPrevu = dateDepartPrevu;
        DateArriveePrevu = dateArriveePrevu;
        DateDepart = dateDepart;
        DateArrivee = dateArrivee;
        PortDepart = Trim(portDepart);
        PortArrivee = Trim(portArrivee);
        NavireDepart = Trim(navireDepart);
        NavireArrivee = Trim(navireArrivee);
        DateDepartTrans = dateDepartTrans;
        DateArriveeTrans = dateArriveeTrans;
        PortTransbordement = Trim(portTransbordement);
    }

    public void SetPartenaires(
        string? keyAgent, string? intituleAgent, string? refAgent,
        string? keyCompagnie, string? intituleCompagnie, string? refCompagnie,
        string? keyConsignataire, string? intituleConsignataire, string? refConsignataire,
        string? keyTractionnaire, string? intituleTractionnaire, string? refTractionnaire)
    {
        KeyAgent = Trim(keyAgent);
        IntituleAgent = Trim(intituleAgent);
        RefAgent = Trim(refAgent);
        KeyCompagnie = Trim(keyCompagnie);
        IntituleCompagnie = Trim(intituleCompagnie);
        RefCompagnie = Trim(refCompagnie);
        KeyConsignataire = Trim(keyConsignataire);
        IntituleConsignataire = Trim(intituleConsignataire);
        RefConsignataire = Trim(refConsignataire);
        KeyTractionnaire = Trim(keyTractionnaire);
        IntituleTractionnaire = Trim(intituleTractionnaire);
        RefTractionnaire = Trim(refTractionnaire);
    }

    /// <summary>Attaches the exploitation team: exploitant and up to two drivers.</summary>
    public void SetPersonnel(
        string? keyExploitant, string? intituleExploitant,
        string? keyChauffeur1, string? intituleChauffeur1,
        string? keyChauffeur2, string? intituleChauffeur2)
    {
        KeyExploitant = Trim(keyExploitant);
        IntituleExploitant = Trim(intituleExploitant);
        KeyChauffeur1 = Trim(keyChauffeur1);
        IntituleChauffeur1 = Trim(intituleChauffeur1);
        KeyChauffeur2 = Trim(keyChauffeur2);
        IntituleChauffeur2 = Trim(intituleChauffeur2);
    }

    public void SetDouane(
        string? numAcquit, DateTime? dateAcquit, string? numManifeste, DateTime? dateManifeste,
        string? numDs, DateTime? dateDs, string? numCnt)
    {
        NumAcquit = Trim(numAcquit);
        DateAcquit = dateAcquit;
        NumManifeste = Trim(numManifeste);
        DateManifeste = dateManifeste;
        NumDs = Trim(numDs);
        DateDs = dateDs;
        NumCnt = Trim(numCnt);
    }

    public void SetMoyens(
        string? numRemorque, string? numTracteur, string? numTc, string? numCaisse,
        string? numTriptyque, DateTime? dateTriptyque, decimal? tare, decimal? capaciteRemorque)
    {
        NumRemorque = Trim(numRemorque);
        NumTracteur = Trim(numTracteur);
        NumTc = Trim(numTc);
        NumCaisse = Trim(numCaisse);
        NumTriptyque = Trim(numTriptyque);
        DateTriptyque = dateTriptyque;
        Tare = tare;
        CapaciteRemorque = capaciteRemorque;
    }

    public void SetLocalites(string? localiteDepart, string? localiteArrivee, string? typeOperation)
    {
        LocaliteDepart = Trim(localiteDepart);
        LocaliteArrivee = Trim(localiteArrivee);
        TypeOperation = Trim(typeOperation);
    }

    /// <summary>
    /// Validates the voyage: stamps <see cref="DateVoyage"/> and moves the status to
    /// <c>$VOYV</c>. This is the gate the whole costing module waits on.
    /// </summary>
    public void Validate(DateTime dateVoyage)
    {
        if (Supprime)
        {
            throw new BusinessRuleException("voyage.deleted", "Un voyage supprimé ne peut pas être validé.");
        }

        if (IsCloture)
        {
            throw new BusinessRuleException("voyage.cloture", "Un voyage clôturé ne peut pas être revalidé.");
        }

        DateVoyage = dateVoyage;
        KeyStatutVoyage = StatutValide;
        DateStatutVoyage = dateVoyage;
    }

    /// <summary>
    /// Closes the voyage. Mirrors the legacy rule that closing is refused while an attached dossier is
    /// still awaiting invoicing.
    /// </summary>
    /// <param name="hasUninvoicedDossier">Result of the legacy "Non Facturé" check.</param>
    public void Close(DateTime dateCloture, bool hasUninvoicedDossier)
    {
        if (!IsValide)
        {
            throw new BusinessRuleException("voyage.not_validated", "Un voyage doit être validé avant d'être clôturé.");
        }

        if (hasUninvoicedDossier)
        {
            throw new BusinessRuleException(
                "voyage.close.uninvoiced",
                "Clôture impossible : un dossier rattaché n'est pas encore facturé.");
        }

        DateCloture = dateCloture;
        KeyStatutVoyage = StatutCloture;
        DateStatutVoyage = dateCloture;
    }

    /// <summary>Reopens a closed voyage (<c>set date_cloture = null</c> in the legacy code).</summary>
    public void Reopen()
    {
        DateCloture = null;
        KeyStatutVoyage = StatutValide;
    }

    public void SetStatutReception(StatutReception statutReception) => StatutReception = statutReception;

    /// <summary>
    /// Switches the voyage into the MEAD flow when a dossier has a customs warehouse. The legacy code
    /// does this silently at validation time.
    /// </summary>
    public void EnterMeadFlow(string keyStatutMead, string libelleStatutMead, DateTime dateEvenement)
    {
        KeyStatutVoyageMead = Require(keyStatutMead, nameof(keyStatutMead));
        LibelleStatutVoyageMead = Require(libelleStatutMead, nameof(libelleStatutMead));
        DateDernierEvenementMead = dateEvenement;
    }

    public void SetStatutMead(string keyStatutMead, string libelleStatutMead, DateTime dateEvenement)
    {
        KeyStatutVoyageMead = Trim(keyStatutMead);
        LibelleStatutVoyageMead = Trim(libelleStatutMead);
        DateDernierEvenementMead = dateEvenement;
    }

    public void SetMagasinMead(string? magasinMead, DateTime? entreeMead)
    {
        MagasinMead = Trim(magasinMead);
        EntreeMead = entreeMead;
    }

    /// <summary>Records the counts the costing engine divides by.</summary>
    public void SetCompteurs(decimal nbDossiers, decimal totalPoidsBrut, decimal totalNbColis)
    {
        NbDossVoyageSaisi = nbDossiers;
        TotalPoidsBrutSaisi = totalPoidsBrut;
        TotalNbColisSaisi = totalNbColis;
    }

    public void SetTemperature(bool dirigee) => TemperatureDirigee = dirigee;
    public void SetNbJourFranchise(int? jours) => NbJourFranchise = jours;
    public void SetAttachement(bool attachement) => Attachement = attachement;
    public void MarkPremiereFacture(DateTime date) => DatePremiereFacture ??= date;

    public void SetModeTraitement(string? modeTraitement) => ModeTraitement = Trim(modeTraitement);

    public void SetDepotage(DateTime? debut, DateTime? fin, string? depotePar, string? chargerMagasin)
    {
        DateDebutDepotage = debut;
        DateFinDepotage = fin;
        DepotePar = Trim(depotePar);
        ChargerMagasin = Trim(chargerMagasin);
    }

    public void LinkEnlevementLivraison(string? keyVoyageEnlevementLivraison) =>
        KeyVoyageEnlevementLivraison = Trim(keyVoyageEnlevementLivraison);

    public void LinkBonCommande(string? keyBonCommande) => KeyBonCommande = Trim(keyBonCommande);

    public void LinkDepart(string? keyDepart) => KeyDepart = Trim(keyDepart);

    public void SetEchangeCompagnie(DateTime? dateEchange, DateTime? datePrevision)
    {
        DateEchangeCompagnie = dateEchange;
        DatePrevisionEchange = datePrevision;
    }

    /// <summary>Soft delete — mirrors <c>set supprimer=1, key_statut_voyage='$SVOY'</c>.</summary>
    public void SoftDelete()
    {
        Supprime = true;
        KeyStatutVoyage = StatutSupprime;
    }

    public void AddTrajet(TrajetVoyage trajet) => _trajets.Add(trajet);
    public void AddEquipement(EquipementVoyage equipement) => _equipements.Add(equipement);

    /// <summary>
    /// Merges an equipment line: the legacy code increments the quantity when a line for the same
    /// type/nature already exists instead of inserting a duplicate.
    /// </summary>
    public EquipementVoyage AddOrIncrementEquipement(string keyLigne, string typeEquipement, string? natureEquipement)
    {
        var existing = _equipements.FirstOrDefault(e =>
            string.Equals(e.TypeEquipementCode, typeEquipement, StringComparison.OrdinalIgnoreCase)
            && string.Equals(e.NatureEquipement, natureEquipement, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            existing.IncrementQuantity();
            return existing;
        }

        var created = new EquipementVoyage(keyLigne, Id, typeEquipement, natureEquipement);
        _equipements.Add(created);
        return created;
    }

    /// <summary>
    /// Recomputes <see cref="NbDossVoyageSaisi"/>, which is the divisor used by the equal-split costing
    /// rule. The legacy code guards against a zero divisor at every call site; here the guard lives in
    /// one place.
    /// </summary>
    public decimal CostDivisor => NbDossVoyageSaisi.GetValueOrDefault() <= 0 ? 1 : NbDossVoyageSaisi!.Value;

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Ordered leg (segment) of a voyage (<c>t_trajet_voyages</c> — note the plural, and note that the
/// singular <c>t_trajet_voyage</c> appears only in commented-out Oracle-era code).
/// </summary>
public sealed class TrajetVoyage : Entity
{
    private TrajetVoyage() { }

    public TrajetVoyage(string keyTrajet, string keyVoyage, int? ordre, string? provenance, string? destination)
    {
        Id = Require(keyTrajet, nameof(keyTrajet));
        KeyVoyage = Require(keyVoyage, nameof(keyVoyage));
        Ordre = ordre;
        Provenance = Trim(provenance);
        Destination = Trim(destination);
    }

    public string KeyTrajet => Id;
    public string KeyVoyage { get; private set; } = string.Empty;
    /// <summary>Leg order within the voyage.</summary>
    public int? Ordre { get; private set; }
    public string? Provenance { get; private set; }
    public string? Destination { get; private set; }
    public string? PaysProvenance { get; private set; }
    public string? PaysDestination { get; private set; }

    public DateTime? DateDepartPrevu { get; private set; }
    public DateTime? DateArriveePrevu { get; private set; }
    public DateTime? DateDepart { get; private set; }
    public DateTime? DateArrivee { get; private set; }

    public string? KeyTracteur { get; private set; }
    public string? NumTracteur { get; private set; }
    public string? KeyRemorque { get; private set; }
    public string? NumRemorque { get; private set; }
    public string? TypeRemorque { get; private set; }
    public string? KeyChauffeur1 { get; private set; }
    public string? IntituleChauffeur1 { get; private set; }
    public string? KeyChauffeur2 { get; private set; }
    public string? IntituleChauffeur2 { get; private set; }

    /// <summary>Distance driven on this leg; gasoil costing is skipped when it is zero.</summary>
    public decimal? Kilometrage { get; private set; }
    /// <summary>Whether toll fees apply to this leg (<c>frais_autoroute=1</c>).</summary>
    public bool FraisAutoroute { get; private set; }
    public string? NumPlomb { get; private set; }
    public string? Flux { get; private set; }
    public string? LocaliteDepart { get; private set; }
    public string? LocaliteArrivee { get; private set; }
    public string? IntituleLocaliteDepart { get; private set; }
    public string? IntituleLocaliteArrivee { get; private set; }

    /// <summary>True when the leg is eligible for gasoil costing.</summary>
    public bool IsCostable => Kilometrage.GetValueOrDefault() != 0 && !string.IsNullOrWhiteSpace(KeyTracteur);

    public void SetDates(DateTime? departPrevu, DateTime? arriveePrevu, DateTime? depart, DateTime? arrivee)
    {
        DateDepartPrevu = departPrevu;
        DateArriveePrevu = arriveePrevu;
        DateDepart = depart;
        DateArrivee = arrivee;
    }

    public void SetMoyens(
        string? keyTracteur, string? numTracteur, string? keyRemorque, string? numRemorque, string? typeRemorque,
        string? keyChauffeur1, string? intituleChauffeur1, string? keyChauffeur2, string? intituleChauffeur2)
    {
        KeyTracteur = Trim(keyTracteur);
        NumTracteur = Trim(numTracteur);
        KeyRemorque = Trim(keyRemorque);
        NumRemorque = Trim(numRemorque);
        TypeRemorque = Trim(typeRemorque);
        KeyChauffeur1 = Trim(keyChauffeur1);
        IntituleChauffeur1 = Trim(intituleChauffeur1);
        KeyChauffeur2 = Trim(keyChauffeur2);
        IntituleChauffeur2 = Trim(intituleChauffeur2);
    }

    public void SetLocalites(
        string? localiteDepart, string? localiteArrivee,
        string? intituleLocaliteDepart, string? intituleLocaliteArrivee)
    {
        LocaliteDepart = Trim(localiteDepart);
        LocaliteArrivee = Trim(localiteArrivee);
        IntituleLocaliteDepart = Trim(intituleLocaliteDepart);
        IntituleLocaliteArrivee = Trim(intituleLocaliteArrivee);
    }

    public void SetKilometrage(decimal? kilometrage, bool fraisAutoroute, string? numPlomb, string? flux)
    {
        if (kilometrage is < 0)
        {
            throw new BusinessRuleException("trajet.kilometrage", "Le kilométrage ne peut pas être négatif.");
        }

        Kilometrage = kilometrage;
        FraisAutoroute = fraisAutoroute;
        NumPlomb = Trim(numPlomb);
        Flux = Trim(flux);
    }

    public void SetOrdre(int? ordre) => Ordre = ordre;

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Equipment line on a voyage (<c>t_equipement_voyage</c>): one row per equipment type/nature pair,
/// with a quantity. Concrete units hang off <see cref="DetailEquipementVoyage"/>.
/// </summary>
public sealed class EquipementVoyage : Entity
{
    private readonly List<DetailEquipementVoyage> _details = [];

    private EquipementVoyage() { }

    public EquipementVoyage(string keyLigneEquipement, string keyVoyage, string typeEquipement, string? natureEquipement)
    {
        Id = Require(keyLigneEquipement, nameof(keyLigneEquipement));
        KeyVoyage = Require(keyVoyage, nameof(keyVoyage));
        TypeEquipementCode = Require(typeEquipement, nameof(typeEquipement)).ToUpperInvariant();
        NatureEquipement = Trim(natureEquipement);
        Qte = 1;
    }

    public string KeyLigneEquipement => Id;
    public string KeyVoyage { get; private set; } = string.Empty;
    /// <summary>REMORQUE / TRACTEUR / CAISSE / LCL.</summary>
    public string TypeEquipementCode { get; private set; } = string.Empty;
    public string? NatureEquipement { get; private set; }
    public int Qte { get; private set; }

    public IReadOnlyCollection<DetailEquipementVoyage> Details => _details.AsReadOnly();

    public TypeEquipement? TypeEquipement => TypeEquipementCode switch
    {
        "REMORQUE" => Shipments.TypeEquipement.Remorque,
        "TRACTEUR" => Shipments.TypeEquipement.Tracteur,
        "CAISSE" => Shipments.TypeEquipement.Caisse,
        "LCL" => Shipments.TypeEquipement.Lcl,
        _ => null
    };

    /// <summary>Mirrors the legacy <c>qte = qte + 1</c> merge behaviour.</summary>
    public void IncrementQuantity() => Qte++;

    public void SetQuantity(int qte)
    {
        if (qte < 1)
        {
            throw new BusinessRuleException("equipement.qte", "La quantité d'équipement doit être au moins 1.");
        }

        Qte = qte;
    }

    public DetailEquipementVoyage AddDetail(DetailEquipementVoyage detail)
    {
        _details.Add(detail);
        return detail;
    }

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Concrete equipment unit (<c>t_detail_equipement_voyage</c>) — a specific trailer plate or container
/// number. <see cref="KeyEquipement"/> is nullable: the LCL variant inserts only a number.
/// </summary>
public sealed class DetailEquipementVoyage : Entity
{
    private DetailEquipementVoyage() { }

    public DetailEquipementVoyage(string keyLigneEquipement, string? keyEquipement, string? numEquipement)
    {
        Id = $"{keyLigneEquipement}:{numEquipement ?? Guid.NewGuid().ToString("N")[..8]}";
        KeyLigneEquipement = Require(keyLigneEquipement, nameof(keyLigneEquipement));
        KeyEquipement = Trim(keyEquipement);
        NumEquipement = Trim(numEquipement);
    }

    public string KeyLigneEquipement { get; private set; } = string.Empty;
    /// <summary>FK to <c>t_materiel</c>; NULL for the LCL case.</summary>
    public string? KeyEquipement { get; private set; }
    public string? NumEquipement { get; private set; }
    public string? NumPlomb { get; private set; }

    /// <summary>Sets the seal number. Only the road branch of the EDI import supplies one.</summary>
    public void SetNumPlomb(string? numPlomb) => NumPlomb = string.IsNullOrWhiteSpace(numPlomb) ? null : numPlomb.Trim();
    public decimal? Tare { get; private set; }
    public DateTime? DateRestitution { get; private set; }
    public string? TypeColis { get; private set; }
    public string? TypeColisContenant { get; private set; }
    public decimal? NbColis { get; private set; }
    public decimal? PoidsBrut { get; private set; }
    public decimal? Volume { get; private set; }
    public string? NatureMarchandise { get; private set; }

    public void SetContenu(string? numPlomb, decimal? tare, string? typeColis, string? typeColisContenant,
        decimal? nbColis, decimal? poidsBrut, decimal? volume, string? natureMarchandise)
    {
        NumPlomb = Trim(numPlomb);
        Tare = tare;
        TypeColis = Trim(typeColis);
        TypeColisContenant = Trim(typeColisContenant);
        NbColis = nbColis;
        PoidsBrut = poidsBrut;
        Volume = volume;
        NatureMarchandise = Trim(natureMarchandise);
    }

    public void SetRestitution(DateTime? dateRestitution) => DateRestitution = dateRestitution;

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
