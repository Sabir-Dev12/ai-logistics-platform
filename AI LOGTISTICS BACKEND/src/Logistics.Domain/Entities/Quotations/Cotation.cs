using Logistics.Domain.Common;
using Logistics.Domain.Entities.ReferenceData;

namespace Logistics.Domain.Entities.Quotations;

/// <summary>
/// How a price line's amount is interpreted (<c>t_prix_cotation.type_utilisation</c>).
/// </summary>
/// <remarks>
/// Stored as free text, not a code table. <see cref="Pourcentage"/> lines are resolved against the
/// freight base (selected by the <c>base_fret</c> company parameter — an earlier note called it
/// <c>param_base_fret</c>, which does not exist) when the quotation is costed, so the distinction is
/// load-bearing rather than cosmetic.
/// </remarks>
public enum TypeUtilisation
{
    Montant,
    Pourcentage
}

/// <summary>
/// Quotation header (<c>t_cotation</c>) — a commercial offer, and the origin of bookings and dossiers.
/// </summary>
/// <remarks>
/// <para>
/// The tenant column is <c>soc_cotation</c>, <b>not</b> <c>societe</c>. The sibling price-line table
/// <c>t_prix_cotation</c> uses plain <c>societe</c>, so the two must not be treated as interchangeable.
/// </para>
/// <para>
/// Unlike almost every other module, the quotation status is a free-text column holding French literals
/// (<c>Saisie</c>, <c>Envoyée</c>, <c>Gagnée</c>, …) rather than a <c>t_statut</c> code. The literals
/// here are the load-bearing ones observed in the legacy code.
/// </para>
/// <para>
/// The legacy table has 77 columns; the business-relevant set is modelled. Ten of those columns appear
/// only in <c>UPDATE … SET</c> or <c>SELECT</c> lists and never in an <c>INSERT</c> — a trap when
/// inferring the schema from insert statements alone.
/// </para>
/// </remarks>
public sealed class Cotation : Entity, ITenantScoped
{
    // Status literals — free text in the legacy column, reproduced verbatim.
    public const string StatutSaisie = "Saisie";
    public const string StatutEnvoyee = "Envoyée";
    public const string StatutGagnee = "Gagnée";
    public const string StatutPerdue = "Perdue";
    public const string StatutAnnulee = "Annulée";
    public const string StatutContreOffre = "Contre Offre";
    public const string StatutSupprimee = "Supprimée";

    /// <summary>
    /// A status the legacy code filtered on but never wrote. Kept so imported data still resolves.
    /// </summary>
    public const string StatutValideeLegacy = "Validée";

    public const string TypeTarifSpot = "Spot";
    public const string TypeTarifGeneral = "Général";

    private readonly List<ColisageCotation> _colisages = [];
    private readonly List<PrixCotation> _prixCotation = [];
    private readonly List<AchatCotation> _achats = [];
    private readonly List<TauxChangeCotation> _tauxChange = [];
    private readonly List<DocumentCotation> _documents = [];

    private Cotation() { }

    public Cotation(
        string keyCotation, string societeKey, string? numCotation, string? produitCotation,
        string keyClient, DateTime dateCotation)
    {
        Id = Require(keyCotation, nameof(keyCotation));
        SocieteKey = Require(societeKey, nameof(societeKey));
        NumCotation = Require(numCotation, nameof(numCotation));
        ProduitCotation = Require(produitCotation, nameof(produitCotation));
        KeyClient = Require(keyClient, nameof(keyClient));
        DateCotation = dateCotation;
        Statut = StatutSaisie;
        DateStatut = dateCotation;
        TypeTarif = TypeTarifSpot;
        MarchandiseGerbable = true;
    }

    public string KeyCotation => Id;
    public string SocieteKey { get; private set; } = string.Empty;
    public string? NumCotation { get; private set; }
    public string? AgenceCotation { get; private set; }
    public DateTime DateCotation { get; private set; }
    public DateTime? DateValidite { get; private set; }
    public string KeyClient { get; private set; } = string.Empty;
    public string? Contact { get; private set; }
    /// <summary>Personnel <em>key</em> of the sales rep; <see cref="CreateurCotation"/> is a name.</summary>
    public string? Commercial { get; private set; }
    /// <summary>Free text — the author's display name, not a personnel key.</summary>
    public string? CreateurCotation { get; private set; }

    /// <summary>Dossier type code; position 1 is the mode and position 2 the direction (I/E).</summary>
    public string? ProduitCotation { get; private set; }
    /// <summary>Complet / Groupage, from the TYPE_CHARGEMENT codification.</summary>
    public string? TypeTransport { get; private set; }
    public string? NatureMarchandise { get; private set; }
    public string? Mode { get; private set; }
    public string? ModeTraitement { get; private set; }
    public string? TypeTarif { get; private set; }
    public string? FrequenceDepart { get; private set; }
    public string? TransitTime { get; private set; }
    public string? LangueClient { get; private set; }

    public string Statut { get; private set; } = StatutSaisie;
    public DateTime? DateStatut { get; private set; }
    public string? CommentaireStatut { get; private set; }

    // Measures
    public decimal? VolumeReel { get; private set; }
    public decimal? VolumeNgerbable { get; private set; }
    public decimal? VolumeTaxable { get; private set; }
    public decimal? PoidsTaxable { get; private set; }
    public decimal? PoidsReel { get; private set; }
    public decimal? MplReel { get; private set; }
    public decimal? EquivalenceVolumePoids { get; private set; }
    public decimal? EquivalenceMplPoids { get; private set; }
    public decimal? EquivalenceMplVolume { get; private set; }

    public string? CommentaireCotation { get; private set; }
    public string? CommentairePrix { get; private set; }
    public string? CommentaireAssurance { get; private set; }

    // Itinerary
    public string? IncotermCotation { get; private set; }
    public string? LieuChargement { get; private set; }
    public string? VilleChargement { get; private set; }
    public string? PaysChargement { get; private set; }
    public string? RegionChargement { get; private set; }
    public string? DepartementChargement { get; private set; }
    public string? LieuDechargement { get; private set; }
    public string? VilleDechargement { get; private set; }
    public string? PaysDechargement { get; private set; }
    public string? RegionDechargement { get; private set; }
    public string? DepartementDechargement { get; private set; }
    public string? PortChargement { get; private set; }
    public string? PortDechargement { get; private set; }
    public string? Provenance { get; private set; }
    public string? Destination { get; private set; }
    public DateTime? DateDepartPrev { get; private set; }
    public DateTime? DateLimiteEnlevement { get; private set; }

    public string? KeyCompagnie { get; private set; }
    public string? IntituleCompagnie { get; private set; }

    // Cargo
    public bool MarchandiseDangereuse { get; private set; }
    public string? ClasseMarchandise { get; private set; }
    public bool MarchandiseFragile { get; private set; }
    public bool MarchandiseGerbable { get; private set; }
    public bool Assurance { get; private set; }
    public decimal? ValeurMarchandise { get; private set; }
    public string? Devise { get; private set; }
    public bool TemperatureDirigee { get; private set; }
    public decimal? TemperatureMin { get; private set; }
    public decimal? TemperatureMax { get; private set; }
    public decimal? FranchiseMagasinage { get; private set; }
    public decimal? FranchiseSurestarie { get; private set; }

    // Commercial outcome
    public string? Concurrent { get; private set; }
    public decimal? MontantConcurrent { get; private set; }
    public string? DeviseConcurrent { get; private set; }
    public string? MotifCotationPerdue { get; private set; }
    public string? KeyRegroupement { get; private set; }
    /// <summary>Key of the quotation this one is a counter-offer to.</summary>
    public string? CotationContreOffre { get; private set; }

    /// <summary>Denormalised "has documents" flag, maintained by the document operations.</summary>
    public bool Attachement { get; private set; }

    public string? ConditionGenerale { get; private set; }
    public string? ConditionPaiement { get; private set; }
    public string? PrestationNonIncluse { get; private set; }

    /// <summary>Soft-delete flag (<c>supp</c>) — the legacy delete is always soft.</summary>
    public bool Supprime { get; private set; }

    public IReadOnlyCollection<ColisageCotation> Colisages => _colisages.AsReadOnly();
    public IReadOnlyCollection<PrixCotation> PrixCotation => _prixCotation.AsReadOnly();
    public IReadOnlyCollection<AchatCotation> Achats => _achats.AsReadOnly();
    public IReadOnlyCollection<TauxChangeCotation> TauxChange => _tauxChange.AsReadOnly();
    public IReadOnlyCollection<DocumentCotation> Documents => _documents.AsReadOnly();

    /// <summary>Direction from the second character of the product code (I = Import, E = Export).</summary>
    public string? Sens => ProduitCotation is { Length: >= 2 } p ? p[1] switch
    {
        'I' or 'i' => "Import",
        'E' or 'e' => "Export",
        _ => null
    } : null;

    public bool IsGroupage => TypeTransport is not null
        && TypeTransport.StartsWith("Groupage", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A quotation may only become a booking or dossier once it is won and still within validity.
    /// </summary>
    public bool IsConvertible(DateTime aujourdhui) =>
        Statut == StatutGagnee && !Supprime
        && (DateValidite is null || DateValidite.Value.Date >= aujourdhui.Date);

    /// <summary>
    /// Whether further commercial operations are allowed.
    /// </summary>
    /// <remarks>
    /// The legacy screens blocked every operation once a quotation left <c>Saisie</c>/<c>Envoyée</c>,
    /// which is the rule that keeps a won quotation from being silently re-opened.
    /// </remarks>
    public bool IsOuverte => Statut is StatutSaisie or StatutEnvoyee;

    public void SetEntete(
        string? numCotation, string? typeTransport, string? natureMarchandise, string? mode,
        string? modeTraitement, string? typeTarif, string? frequenceDepart, string? transitTime,
        string? langueClient, string? commentaire, string? commentairePrix)
    {
        NumCotation = Require(numCotation, nameof(numCotation));
        TypeTransport = Trim(typeTransport);
        NatureMarchandise = Trim(natureMarchandise);
        Mode = Trim(mode);
        ModeTraitement = Trim(modeTraitement);
        TypeTarif = Trim(typeTarif) ?? TypeTarifSpot;
        FrequenceDepart = Trim(frequenceDepart);
        TransitTime = Trim(transitTime);
        LangueClient = Trim(langueClient);
        CommentaireCotation = Trim(commentaire);
        CommentairePrix = Trim(commentairePrix);
    }

    public void SetParties(string? agence, string? contact, string? commercial, string? createur)
    {
        AgenceCotation = Trim(agence);
        Contact = Trim(contact);
        Commercial = Trim(commercial);
        CreateurCotation = Trim(createur);
    }

    public void SetProduit(string? produitCotation, string? keyClient)
    {
        ProduitCotation = Require(produitCotation, nameof(produitCotation));
        KeyClient = Require(keyClient, nameof(keyClient));
    }

    public void SetValidite(DateTime? dateValidite, DateTime? dateLimiteEnlevement, DateTime? dateDepartPrev)
    {
        if (dateValidite.HasValue && dateValidite.Value.Date < DateCotation.Date)
        {
            throw new BusinessRuleException(
                "cotation.validite", "La date de validité ne peut pas être antérieure à la date de création.");
        }

        DateValidite = dateValidite;
        DateLimiteEnlevement = dateLimiteEnlevement;
        DateDepartPrev = dateDepartPrev;
    }

    public void SetCompagnie(string? keyCompagnie, string? intituleCompagnie)
    {
        KeyCompagnie = Trim(keyCompagnie);
        IntituleCompagnie = Trim(intituleCompagnie);
    }

    public void SetItineraire(
        string? incoterm, string? lieuChargement, string? villeChargement, string? paysChargement,
        string? regionChargement, string? departementChargement,
        string? lieuDechargement, string? villeDechargement, string? paysDechargement,
        string? regionDechargement, string? departementDechargement,
        string? portChargement, string? portDechargement, string? provenance, string? destination)
    {
        IncotermCotation = Trim(incoterm);
        LieuChargement = Trim(lieuChargement);
        VilleChargement = Trim(villeChargement);
        PaysChargement = Trim(paysChargement);
        RegionChargement = Trim(regionChargement);
        DepartementChargement = Trim(departementChargement);
        LieuDechargement = Trim(lieuDechargement);
        VilleDechargement = Trim(villeDechargement);
        PaysDechargement = Trim(paysDechargement);
        RegionDechargement = Trim(regionDechargement);
        DepartementDechargement = Trim(departementDechargement);
        PortChargement = Trim(portChargement);
        PortDechargement = Trim(portDechargement);
        Provenance = Trim(provenance);
        Destination = Trim(destination);
    }

    /// <summary>
    /// Sets the cargo description and its conditional requirements.
    /// </summary>
    /// <remarks>
    /// Three coupled rules the legacy form enforced: hazardous goods need a class, a requested assurance
    /// needs a declared value and its currency, and directed-temperature bounds must be ordered.
    /// </remarks>
    public void SetMarchandise(
        string? nature, bool dangereuse, string? classe, bool fragile, bool gerbable,
        bool assurance, decimal? valeurMarchandise, string? devise,
        bool temperatureDirigee, decimal? temperatureMin, decimal? temperatureMax)
    {
        if (dangereuse && string.IsNullOrWhiteSpace(classe))
        {
            throw new BusinessRuleException(
                "cotation.dangereuse.classe",
                "La classe est obligatoire pour une marchandise dangereuse.");
        }

        if (assurance && (valeurMarchandise is null || valeurMarchandise <= 0m))
        {
            throw new BusinessRuleException(
                "cotation.assurance.valeur",
                "La valeur de la marchandise est obligatoire lorsque l'assurance est demandée.");
        }

        if (assurance && string.IsNullOrWhiteSpace(devise))
        {
            throw new BusinessRuleException(
                "cotation.assurance.devise",
                "La devise de la valeur marchandise est obligatoire lorsque l'assurance est demandée.");
        }

        if (temperatureDirigee && temperatureMin.HasValue && temperatureMax.HasValue
            && temperatureMax < temperatureMin)
        {
            throw new BusinessRuleException(
                "cotation.temperature",
                "La température maximale doit être supérieure ou égale à la minimale.");
        }

        NatureMarchandise = Trim(nature);
        MarchandiseDangereuse = dangereuse;
        ClasseMarchandise = Trim(classe);
        MarchandiseFragile = fragile;
        MarchandiseGerbable = gerbable;
        Assurance = assurance;
        ValeurMarchandise = valeurMarchandise;
        Devise = Trim(devise);
        TemperatureDirigee = temperatureDirigee;
        TemperatureMin = temperatureMin;
        TemperatureMax = temperatureMax;
    }

    public void SetMesures(
        decimal? poidsReel, decimal? volumeReel, decimal? volumeNgerbable, decimal? poidsTaxable,
        decimal? volumeTaxable, decimal? mplReel,
        decimal? equivalenceVolumePoids, decimal? equivalenceMplPoids, decimal? equivalenceMplVolume)
    {
        PoidsReel = poidsReel;
        VolumeReel = volumeReel;
        VolumeNgerbable = volumeNgerbable;
        PoidsTaxable = poidsTaxable;
        VolumeTaxable = volumeTaxable;
        MplReel = mplReel;
        EquivalenceVolumePoids = equivalenceVolumePoids;
        EquivalenceMplPoids = equivalenceMplPoids;
        EquivalenceMplVolume = equivalenceMplVolume;
    }

    public void SetFranchises(decimal? magasinage, decimal? surestarie)
    {
        FranchiseMagasinage = magasinage;
        FranchiseSurestarie = surestarie;
    }

    /// <summary>
    /// Copies the per-dossier-type conditions and franchises, as the legacy form did on creation.
    /// </summary>
    public void ApplyConditions(FranchiseCotation? conditions)
    {
        if (conditions is null)
        {
            return;
        }

        ConditionGenerale = conditions.ConditionGeneraleFr;
        ConditionPaiement = conditions.ConditionPaiementFr;
        PrestationNonIncluse = conditions.PrestationNonIncluseFr;

        if (string.Equals(conditions.NatureFranchise, "Magasinage", StringComparison.OrdinalIgnoreCase))
        {
            FranchiseMagasinage = conditions.FranchiseComplet;
        }
        else if (string.Equals(conditions.NatureFranchise, "Surestarie", StringComparison.OrdinalIgnoreCase))
        {
            FranchiseSurestarie = conditions.FranchiseComplet;
        }
    }

    public void SetCommentaireAssurance(string? commentaire) => CommentaireAssurance = Trim(commentaire);

    /// <summary>
    /// Moves the quotation to <c>Envoyée</c>, enforcing the legacy pre-conditions.
    /// </summary>
    /// <param name="obligCloisage">
    /// Whether the company requires packing detail for groupage quotations
    /// (the legacy <c>param_oblig_cloisage</c> switch).
    /// </param>
    public void Envoyer(DateTime dateStatut, bool obligCloisage = false)
    {
        if (Supprime)
        {
            throw new BusinessRuleException("cotation.supprimee", "Une cotation supprimée ne peut pas être validée.");
        }

        if (!IsOuverte)
        {
            throw new BusinessRuleException(
                "cotation.statut", $"La cotation est au statut « {Statut} » : aucune opération n'est possible.");
        }

        // A quotation with no price line has nothing to send.
        if (_prixCotation.Count == 0)
        {
            throw new BusinessRuleException(
                "cotation.prix.requis", "La cotation doit comporter au moins une ligne de prix.");
        }

        if (obligCloisage && IsGroupage && _colisages.Count == 0)
        {
            throw new BusinessRuleException(
                "cotation.colisage.requis", "La cotation de groupage doit comporter au moins une ligne de colisage.");
        }

        Statut = StatutEnvoyee;
        DateStatut = dateStatut;
    }

    /// <summary>Closes the quotation as won or lost, recording the competitor details.</summary>
    public void Cloturer(
        bool gagnee, DateTime dateStatut, string? commentaire,
        string? concurrent = null, decimal? montantConcurrent = null, string? deviseConcurrent = null,
        string? motifPerdue = null)
    {
        if (!IsOuverte)
        {
            throw new BusinessRuleException(
                "cotation.statut", $"La cotation est au statut « {Statut} » : elle ne peut plus être clôturée.");
        }

        if (!gagnee && string.IsNullOrWhiteSpace(motifPerdue))
        {
            throw new BusinessRuleException(
                "cotation.motif_perdue", "Le motif est obligatoire pour une cotation perdue.");
        }

        Statut = gagnee ? StatutGagnee : StatutPerdue;
        DateStatut = dateStatut;
        CommentaireStatut = Trim(commentaire);
        Concurrent = Trim(concurrent);
        MontantConcurrent = montantConcurrent;
        DeviseConcurrent = Trim(deviseConcurrent);
        MotifCotationPerdue = Trim(motifPerdue);
    }

    /// <summary>
    /// Cancels the quotation.
    /// </summary>
    /// <remarks>
    /// Blocked once the quotation has been won, lost, cancelled or turned into a counter-offer — the
    /// same set of states the legacy "Annulation Cotation" operation refused.
    /// </remarks>
    public void Annuler(DateTime dateStatut, string? motif)
    {
        if (Statut is StatutAnnulee or StatutGagnee or StatutPerdue or StatutContreOffre)
        {
            throw new BusinessRuleException(
                "cotation.annulation.statut", $"Une cotation au statut « {Statut} » ne peut pas être annulée.");
        }

        Statut = StatutAnnulee;
        DateStatut = dateStatut;
        CommentaireStatut = Trim(motif);
    }

    /// <summary>Flags the original when a counter-offer has been issued from it.</summary>
    public void MarquerContreOffre(DateTime dateStatut) =>
        SetStatut(StatutContreOffre, dateStatut, null);

    public void LinkContreOffre(string? keyCotationSource) => CotationContreOffre = Trim(keyCotationSource);

    public void SetRegroupement(string? keyRegroupement) => KeyRegroupement = Trim(keyRegroupement);

    public void SetStatut(string statut, DateTime? dateStatut, string? commentaire)
    {
        Statut = Require(statut, nameof(statut));
        DateStatut = dateStatut;
        CommentaireStatut = Trim(commentaire);
    }

    /// <summary>
    /// Soft-deletes the quotation (<c>supp=1</c> + status <c>Supprimée</c>).
    /// </summary>
    /// <remarks>
    /// The legacy delete was always soft, and it was additionally refused while the quotation carried
    /// documents or <c>t_frais_annexe_cotation</c> rows. Those guards belong to the service, which can
    /// query for them; the flag transition lives here.
    /// </remarks>
    public void SoftDelete(DateTime dateStatut)
    {
        Supprime = true;
        Statut = StatutSupprimee;
        DateStatut = dateStatut;
    }

    public void SetAttachement(bool attachement) => Attachement = attachement;

    public void SetCreateur(string? createur) => CreateurCotation = Trim(createur);

    public void AddColisage(ColisageCotation colisage) => _colisages.Add(colisage);
    public void AddPrix(PrixCotation prix) => _prixCotation.Add(prix);
    public void AddAchat(AchatCotation achat) => _achats.Add(achat);
    public void AddTaux(TauxChangeCotation taux) => _tauxChange.Add(taux);
    public void AddDocument(DocumentCotation document) => _documents.Add(document);

    public void ClearColisages() => _colisages.Clear();
    public void ClearPrix() => _prixCotation.Clear();
    public void ClearTaux() => _tauxChange.Clear();

    /// <summary>
    /// The exchange rate for a currency, defaulting to 1 for the company's own currency.
    /// </summary>
    /// <remarks>
    /// The legacy code always inserted the société currency at rate 1 and treated a missing row as 0 via
    /// <c>isnull(taux_change,0)</c>. Returning 1 instead of 0 for an unknown foreign currency would
    /// silently treat it as the local one, so unknown currencies fall back to 0 and the caller decides.
    /// </remarks>
    public decimal TauxPour(string? devise, string deviseSociete)
    {
        if (string.IsNullOrWhiteSpace(devise))
        {
            return 1m;
        }

        if (string.Equals(devise, deviseSociete, StringComparison.OrdinalIgnoreCase))
        {
            return 1m;
        }

        return _tauxChange
            .FirstOrDefault(t => string.Equals(t.Devise, devise, StringComparison.OrdinalIgnoreCase))
            ?.TauxChange ?? 0m;
    }

    private static string Require(string? value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Packing / equipment lines of a quotation (<c>t_colisage_cotation</c>). <b>No tenant column</b> —
/// scope comes through <c>key_cotation</c>.
/// </summary>
public sealed class ColisageCotation : Entity
{
    private ColisageCotation() { }

    public ColisageCotation(string keyColisageCotation, string keyCotation, string? typeColis, decimal? qte)
    {
        Id = Require(keyColisageCotation, nameof(keyColisageCotation));
        KeyCotation = Require(keyCotation, nameof(keyCotation));
        TypeColis = Trim(typeColis);
        Qte = qte;
    }

    public string KeyColisageCotation => Id;
    public string KeyCotation { get; private set; } = string.Empty;
    /// <summary>Matched against <c>t_prix_cotation.base_calcul</c> for equipment-priced lines.</summary>
    public string? TypeColis { get; private set; }
    public decimal? Qte { get; private set; }
    public decimal? Poids { get; private set; }
    public bool Gerbable { get; private set; }
    public decimal? Longueur { get; private set; }
    public decimal? Largeur { get; private set; }
    public decimal? Hauteur { get; private set; }
    public decimal? Volume { get; private set; }
    public decimal? HauteurNgerbable { get; private set; }
    public decimal? VolumeNgerbable { get; private set; }
    /// <summary>Stores the equipment <em>label</em>, not a key.</summary>
    public string? TypeEquipement { get; private set; }
    /// <summary>Stores the equipment <em>label</em>, not a key.</summary>
    public string? NatureEquipement { get; private set; }
    public decimal? LargeurEquipement { get; private set; }
    public decimal? MplUnitaire { get; private set; }

    public void SetDimensions(
        decimal? poids, bool gerbable, decimal? longueur, decimal? largeur, decimal? hauteur, decimal? volume,
        decimal? hauteurNgerbable, decimal? volumeNgerbable, string? typeEquipement, string? natureEquipement,
        decimal? largeurEquipement, decimal? mplUnitaire)
    {
        Poids = poids;
        Gerbable = gerbable;
        Longueur = longueur;
        Largeur = largeur;
        Hauteur = hauteur;
        Volume = volume;
        HauteurNgerbable = hauteurNgerbable;
        VolumeNgerbable = volumeNgerbable;
        TypeEquipement = Trim(typeEquipement);
        NatureEquipement = Trim(natureEquipement);
        LargeurEquipement = largeurEquipement;
        MplUnitaire = mplUnitaire;
    }

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Selling-price lines (<c>t_prix_cotation</c>).
/// </summary>
/// <remarks>
/// The tenant column here is plain <c>societe</c> — unlike its parent <c>t_cotation</c>, which uses
/// <c>soc_cotation</c>. Getting that wrong silently breaks tenant isolation on price lines only.
/// </remarks>
public sealed class PrixCotation : Entity, ITenantScoped
{
    private PrixCotation() { }

    public PrixCotation(string keyLignePrix, string keyCotation, string societeKey)
    {
        Id = Require(keyLignePrix, nameof(keyLignePrix));
        KeyCotation = Require(keyCotation, nameof(keyCotation));
        SocieteKey = Require(societeKey, nameof(societeKey));
    }

    public string KeyLignePrix => Id;
    public string KeyCotation { get; private set; } = string.Empty;
    public string SocieteKey { get; private set; } = string.Empty;
    public int? Position { get; private set; }
    public string? CodeRubrique { get; private set; }
    public string? LibelleRubrique { get; private set; }
    public decimal? Quantite { get; private set; }
    public TypeUtilisation? TypeUtilisation { get; private set; }
    public decimal? MontantValeur { get; private set; }
    /// <summary>Defaults to MAD when null in the legacy data (<c>isnull(devise,'MAD')</c>).</summary>
    public string? Devise { get; private set; }
    public string? BaseCalcul { get; private set; }
    public bool Arrondi { get; private set; }
    public decimal? MontantAchat { get; private set; }
    public string? DeviseAchat { get; private set; }
    public string? Commentaire { get; private set; }
    /// <summary>Stored as a percentage literal such as <c>"20%"</c>, not a fraction.</summary>
    public string? CodeTva { get; private set; }

    /// <summary>Normalised <c>code_tva</c>: <c>"20%"</c> becomes 0.20, unparseable becomes 0.</summary>
    public decimal TauxTva => ParseTaux(CodeTva);

    public void SetContenu(
        int? position, string? codeRubrique, string? libelleRubrique, decimal? quantite,
        TypeUtilisation? typeUtilisation, decimal? montantValeur, string? devise, string? baseCalcul,
        bool arrondi, decimal? montantAchat, string? deviseAchat, string? commentaire, string? codeTva)
    {
        // Qualified on purpose: the property `TypeUtilisation` shadows the enum type inside this class,
        // so an unqualified `TypeUtilisation.Pourcentage` would bind to the property and fail.
        if (typeUtilisation == Quotations.TypeUtilisation.Pourcentage && montantValeur is > 100m)
        {
            throw new BusinessRuleException(
                "cotation.prix.pourcentage",
                "Un montant exprimé en pourcentage ne peut pas dépasser 100.");
        }

        Position = position;
        CodeRubrique = Trim(codeRubrique);
        LibelleRubrique = Trim(libelleRubrique);
        Quantite = quantite;
        TypeUtilisation = typeUtilisation;
        MontantValeur = montantValeur;
        Devise = Trim(devise);
        BaseCalcul = Trim(baseCalcul);
        Arrondi = arrondi;
        MontantAchat = montantAchat;
        DeviseAchat = Trim(deviseAchat);
        Commentaire = Trim(commentaire);
        CodeTva = Trim(codeTva);
    }

    /// <summary>
    /// The line amount in the line's own currency.
    /// </summary>
    /// <remarks>
    /// A <c>Montant</c> line is quantity × unit amount. A <c>Pourcentage</c> line is a share of a base
    /// that the caller must supply (the freight base), so it cannot be computed in isolation — returning
    /// zero when no base is given is deliberate, because inventing a base would mis-price the line.
    /// </remarks>
    public decimal MontantLigne(decimal baseFret = 0m) => TypeUtilisation switch
    {
        // Fully qualified because the property shadows the enum type in this scope.
        Quotations.TypeUtilisation.Montant => Math.Round((Quantite ?? 0m) * (MontantValeur ?? 0m), 2, MidpointRounding.AwayFromZero),
        Quotations.TypeUtilisation.Pourcentage => Math.Round(baseFret * (MontantValeur ?? 0m) / 100m, 2, MidpointRounding.AwayFromZero),
        _ => 0m
    };

    private static decimal ParseTaux(string? codeTva)
    {
        if (string.IsNullOrWhiteSpace(codeTva))
        {
            return 0m;
        }

        var cleaned = codeTva.Replace("%", string.Empty).Replace(",", ".").Trim();

        return decimal.TryParse(cleaned, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value / 100m
            : 0m;
    }

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Purchase-side cost lines (<c>t_achat_cotation</c>, no direct tenant column —
/// <c>soc_rubrique</c> holds the <em>rubrique's</em> company).
/// </summary>
public sealed class AchatCotation : Entity
{
    private AchatCotation() { }

    public AchatCotation(string keyLigne, string keyCotation)
    {
        Id = Require(keyLigne, nameof(keyLigne));
        KeyCotation = Require(keyCotation, nameof(keyCotation));
    }

    public string KeyLigne => Id;
    public string KeyCotation { get; private set; } = string.Empty;
    public string? CodeRubrique { get; private set; }
    public string? SocRubrique { get; private set; }
    public string? LibelleRubrique { get; private set; }
    public decimal? MtDevise { get; private set; }
    public string? Devise { get; private set; }
    public string? KeyFournisseur { get; private set; }
    public string? LibelleFournisseur { get; private set; }
    public DateTime? DateValidite { get; private set; }
    public string? CheminJustif { get; private set; }
    public string? LibelleJustif { get; private set; }
    /// <summary>MAD equivalent, recomputed whenever the quotation's exchange rates change.</summary>
    public decimal? MtMad { get; private set; }

    public void SetContenu(
        string? codeRubrique, string? socRubrique, string? libelleRubrique, decimal? mtDevise, string? devise,
        string? keyFournisseur, string? libelleFournisseur, DateTime? dateValidite,
        string? cheminJustif, string? libelleJustif)
    {
        CodeRubrique = Trim(codeRubrique);
        SocRubrique = Trim(socRubrique);
        LibelleRubrique = Trim(libelleRubrique);
        MtDevise = mtDevise;
        Devise = Trim(devise);
        KeyFournisseur = Trim(keyFournisseur);
        LibelleFournisseur = Trim(libelleFournisseur);
        DateValidite = dateValidite;
        CheminJustif = Trim(cheminJustif);
        LibelleJustif = Trim(libelleJustif);
    }

    /// <summary>
    /// Validates the purchase line against the quotation's creation date.
    /// </summary>
    /// <remarks>
    /// The legacy message is "La date validité ne doit pas être supérieur à date création cotation" —
    /// the purchase justification may not post-date the quotation itself.
    /// </remarks>
    public void Validate(DateTime dateCotation)
    {
        if (MtDevise is null or <= 0m)
        {
            throw new BusinessRuleException("cotation.achat.montant", "Le montant d'achat doit être supérieur à zéro.");
        }

        if (string.IsNullOrWhiteSpace(Devise))
        {
            throw new BusinessRuleException("cotation.achat.devise", "La devise d'achat est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(KeyFournisseur))
        {
            throw new BusinessRuleException("cotation.achat.fournisseur", "Le fournisseur est obligatoire.");
        }

        if (DateValidite.HasValue && DateValidite.Value.Date > dateCotation.Date)
        {
            throw new BusinessRuleException(
                "cotation.achat.validite",
                "La date de validité ne peut pas être postérieure à la date de création de la cotation.");
        }
    }

    /// <summary>Recomputes the MAD equivalent from a rate.</summary>
    public void RecalculerMad(decimal tauxChange) =>
        MtMad = Math.Round((MtDevise ?? 0m) * tauxChange, 2, MidpointRounding.AwayFromZero);

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Quotation-level exchange rates (<c>t_taux_change_cotation</c>).
/// </summary>
/// <remarks>
/// Composite primary key <c>(key_cotation, devise)</c> with <b>no surrogate key and no tenant column</b>.
/// The legacy sale and purchase rate grids both write here and each save deletes every row first, so the
/// two grids are mutually destructive: a currency present in both collides on the same key.
/// </remarks>
public sealed class TauxChangeCotation : Entity
{
    private TauxChangeCotation() { }

    public TauxChangeCotation(string keyCotation, string devise, decimal? tauxChange)
    {
        Id = $"{keyCotation}:{devise}";
        KeyCotation = Require(keyCotation, nameof(keyCotation));
        Devise = Require(devise, nameof(devise));
        TauxChange = tauxChange;
    }

    public string KeyCotation { get; private set; } = string.Empty;
    public string Devise { get; private set; } = string.Empty;
    public decimal? TauxChange { get; private set; }

    public void SetTaux(decimal? taux)
    {
        if (taux is < 0m)
        {
            throw new BusinessRuleException("cotation.taux", "Le taux de change ne peut pas être négatif.");
        }

        TauxChange = taux;
    }

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();
}

/// <summary>
/// Attached documents of a quotation (<c>t_document_cotation</c>). No tenant column.
/// </summary>
/// <remarks>
/// Presence of any document blocks deletion of the quotation <em>and</em> drives
/// <c>t_cotation.attachement</c>, which is reset when the last document goes away.
/// </remarks>
public sealed class DocumentCotation : Entity
{
    private DocumentCotation() { }

    public DocumentCotation(string keyDocument, string keyCotation, string? libelleDocument, string? cheminDocument)
    {
        Id = Require(keyDocument, nameof(keyDocument));
        KeyCotation = Require(keyCotation, nameof(keyCotation));
        LibelleDocument = Trim(libelleDocument);
        CheminDocument = Trim(cheminDocument);
    }

    public string KeyDocument => Id;
    public string KeyCotation { get; private set; } = string.Empty;
    public string? LibelleDocument { get; private set; }
    /// <summary>File path; the legacy code tested it for null to mean "no file attached".</summary>
    public string? CheminDocument { get; private set; }
    public string? TypePieceDocument { get; private set; }

    public void SetPiece(string? typePieceDocument) => TypePieceDocument = Trim(typePieceDocument);

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Base-calculation referential (<c>t_base_calcul_cotation</c>).
/// </summary>
/// <remarks>
/// Seeded outside the application: there is no <c>INSERT</c> or <c>UPDATE</c> anywhere in the legacy
/// source, so this is read-only from the API's point of view.
/// </remarks>
public sealed class BaseCalculCotation : Entity
{
    private BaseCalculCotation() { }

    public BaseCalculCotation(string baseCalcul, string? utilisation = null, decimal? coefficient = null, bool arrondi = false)
    {
        Id = Require(baseCalcul, nameof(baseCalcul));
        Utilisation = Trim(utilisation);
        Coefficient = coefficient;
        Arrondi = arrondi;
    }

    public string BaseCalcul => Id;
    public bool Arrondi { get; private set; }
    /// <summary>Free text probed with <c>LIKE</c> against the type de chargement.</summary>
    public string? Utilisation { get; private set; }
    /// <summary>Read with <c>isnull(coefficient,1)</c> in the legacy code.</summary>
    public decimal? Coefficient { get; private set; }

    public decimal CoefficientEffectif => Coefficient ?? 1m;

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Per-company, per-dossier-type franchises and the standard quotation conditions
/// (<c>t_franchise_cotation</c>).
/// </summary>
/// <remarks>
/// Composite key <c>(societe, type_dossier, nature_franchise)</c>, tenant column <c>societe</c>. Like
/// the base-calcul referential it is seeded outside the application — only <c>UPDATE</c> appears in the
/// legacy code.
/// </remarks>
public sealed class FranchiseCotation : Entity, ITenantScoped
{
    public const string NatureMagasinage = "Magasinage";
    public const string NatureSurestarie = "Surestarie";

    private FranchiseCotation() { }

    public FranchiseCotation(string societeKey, string typeDossier, string natureFranchise)
    {
        Id = $"{societeKey}:{typeDossier}:{natureFranchise}";
        SocieteKey = Require(societeKey, nameof(societeKey));
        TypeDossier = Require(typeDossier, nameof(typeDossier));
        NatureFranchise = Require(natureFranchise, nameof(natureFranchise));
    }

    public string SocieteKey { get; private set; } = string.Empty;
    public string TypeDossier { get; private set; } = string.Empty;
    /// <summary>Magasinage or Surestarie.</summary>
    public string NatureFranchise { get; private set; } = string.Empty;
    public decimal? FranchiseComplet { get; private set; }
    /// <summary>Stored as a string, not a number, in the legacy column.</summary>
    public string? FranchiseGroupage { get; private set; }
    public string? ConditionGeneraleFr { get; private set; }
    public string? ConditionPaiementFr { get; private set; }
    public string? PrestationNonIncluseFr { get; private set; }
    public string? ConditionGeneraleAng { get; private set; }
    public string? ConditionPaiementAng { get; private set; }
    public string? PrestationNonIncluseAng { get; private set; }

    public void SetConditions(
        decimal? franchiseComplet, string? franchiseGroupage,
        string? conditionGeneraleFr, string? conditionPaiementFr, string? prestationNonIncluseFr,
        string? conditionGeneraleAng, string? conditionPaiementAng, string? prestationNonIncluseAng)
    {
        FranchiseComplet = franchiseComplet;
        FranchiseGroupage = Trim(franchiseGroupage);
        ConditionGeneraleFr = Trim(conditionGeneraleFr);
        ConditionPaiementFr = Trim(conditionPaiementFr);
        PrestationNonIncluseFr = Trim(prestationNonIncluseFr);
        ConditionGeneraleAng = Trim(conditionGeneraleAng);
        ConditionPaiementAng = Trim(conditionPaiementAng);
        PrestationNonIncluseAng = Trim(prestationNonIncluseAng);
    }

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
