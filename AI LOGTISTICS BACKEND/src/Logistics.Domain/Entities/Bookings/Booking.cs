using Logistics.Domain.Common;
using Logistics.Domain.Entities.ReferenceData;

namespace Logistics.Domain.Entities.Bookings;

/// <summary>
/// Booking header (<c>t_booking</c>) — the pre-booking raised before a dossier or voyage exists.
/// The tenant column is <c>societe</c>.
/// </summary>
/// <remarks>
/// <para>
/// The legacy table has <b>128 columns</b>; the business-meaningful subset is modelled here. Two of
/// those columns are easy to misread: <c>devise_assurance</c> is the currency of
/// <c>valeur_marchandise</c> generally, not an insurance-only field, and
/// <c>libelle_statut_mead</c> is a denormalised copy of <c>t_statut.libelle_statut</c> that is never
/// joined back.
/// </para>
/// <para>
/// There are two parallel sets of measures: the "prévision" block captured at booking time
/// (<see cref="PoidsBrutPrevision"/>, <see cref="VolumePrevision"/>, …) and the "réel" block filled in
/// at dépotage (<see cref="PoidsBrut"/>, <see cref="Volume"/>, …). The voyage capacity check uses the
/// <em>prévision</em> volume, which is why the free-capacity calculation lives here rather than on the
/// voyage.
/// </para>
/// </remarks>
public sealed class Booking : Entity, ITenantScoped
{
    public const string StatutAttenteRattachement = "$ATRM";
    public const string StatutConverti = "$BOCV";
    public const string StatutDuplication = "$BOKC";
    public const string StatutAnnule = "$BOKAN";
    public const string StatutSupprime = "$SBOK";

    public const string StatutMeadAttenteDepotage = "$ATDEPOTB";
    public const string StatutMeadEnStock = "$STOCKB";
    public const string StatutMeadSortie = "$SORTB";

    public const string PaysMaroc = "MAROC";

    private readonly List<ColisageBooking> _colisages = [];
    private readonly List<EquipementBooking> _equipements = [];

    private Booking() { }

    public Booking(string keyBooking, string societeKey, string? numBooking, string? typeBooking, DateTime dateCreation)
    {
        Id = Require(keyBooking, nameof(keyBooking));
        SocieteKey = Require(societeKey, nameof(societeKey));
        NumBooking = Require(numBooking, nameof(numBooking));
        TypeBooking = Require(typeBooking, nameof(typeBooking));
        DateCreation = dateCreation;
        KeyStatutBooking = StatutAttenteRattachement;
        DateStatutBooking = dateCreation;
        Supprime = false;
    }

    public string KeyBooking => Id;
    public string SocieteKey { get; private set; } = string.Empty;
    public string? NumBooking { get; private set; }
    public DateTime DateCreation { get; private set; }
    public string? AgenceBooking { get; private set; }
    public string? IntituleAgenceBooking { get; private set; }

    /// <summary>Dossier/voyage type code, from <c>t_type_dossier</c>.</summary>
    public string? TypeBooking { get; private set; }
    /// <summary>Complet / Groupage / Groupage Confrére / Vide, from <c>t_codification_type</c>.</summary>
    public string? TypeChargement { get; private set; }
    public string? Incoterm { get; private set; }
    public string? Mode { get; private set; }
    public DateTime? DateValidite { get; private set; }

    public string? KeyDossier { get; private set; }
    public string? KeyCotation { get; private set; }

    public string? KeyAgent { get; private set; }
    public string? IntituleAgent { get; private set; }
    public string? CommercialBooking { get; private set; }
    public string? NomCommercialBooking { get; private set; }
    public string? KeyCompagnie { get; private set; }
    public string? IntituleCompagnie { get; private set; }
    public string? KeyClient { get; private set; }
    public string? IntituleClient { get; private set; }

    // Expediteur
    public string? KeyExpediteur { get; private set; }
    public string? IntituleExpediteur { get; private set; }
    public string? VilleExpediteur { get; private set; }
    /// <summary>Country <em>code</em>; the label is denormalised separately.</summary>
    public string? PaysExpediteur { get; private set; }
    public string? IntitulePaysExpediteur { get; private set; }

    // Destinataire
    public string? KeyDestinataire { get; private set; }
    public string? IntituleDestinataire { get; private set; }
    public string? VilleDestinataire { get; private set; }
    public string? PaysDestinataire { get; private set; }
    public string? IntitulePaysDestinataire { get; private set; }

    public string? DescriptionMarchandise { get; private set; }
    public bool MarchandiseGerbable { get; private set; }
    public bool MarchandiseDangereuse { get; private set; }
    public string? ClasseMarchandiseDangereuse { get; private set; }
    public bool MarchandiseFragile { get; private set; }
    public bool Assurance { get; private set; }
    public decimal? ValeurMarchandise { get; private set; }
    /// <summary>Currency of <see cref="ValeurMarchandise"/>; the legacy name suggests insurance only.</summary>
    public string? DeviseAssurance { get; private set; }
    public bool TemperatureDirigee { get; private set; }
    public decimal? TemperatureMin { get; private set; }
    public decimal? TemperatureMax { get; private set; }
    public decimal? Suspendu { get; private set; }
    public string? LangueClient { get; private set; }

    // Prévision measures — captured at booking time, used by the voyage capacity check.
    public decimal? PoidsBrutPrevision { get; private set; }
    public decimal? VolumePrevision { get; private set; }
    public decimal? MplPrevision { get; private set; }
    public decimal? PoidsTaxablePrevision { get; private set; }
    public decimal? VolumeTaxablePrevision { get; private set; }
    public decimal? EquivalenceVolumePoids { get; private set; }
    public decimal? EquivalenceMplPoids { get; private set; }
    public decimal? EquivalenceMplVolume { get; private set; }

    // Réel measures — filled in at dépotage.
    public decimal? PoidsBrut { get; private set; }
    public decimal? Volume { get; private set; }
    public decimal? Mpl { get; private set; }
    public decimal? PoidsTaxable { get; private set; }
    public decimal? VolumeTaxable { get; private set; }
    public decimal? PoidsMagasinage { get; private set; }

    public string? PortDepart { get; private set; }
    public string? PortArrivee { get; private set; }
    public DateTime? DateDepartPrevue { get; private set; }
    public DateTime? DateArriveePrevue { get; private set; }
    public string? TransitTime { get; private set; }
    public DateTime? DateDisponibiliteMarchandise { get; private set; }
    public string? Provenance { get; private set; }
    public string? Destination { get; private set; }
    public string? Commentaire { get; private set; }

    public string KeyStatutBooking { get; private set; } = StatutAttenteRattachement;
    public DateTime? DateStatutBooking { get; private set; }
    public string? CommentaireStatutBooking { get; private set; }

    // MEAD flow
    public string? KeyStatutBookingMead { get; private set; }
    public string? LibelleStatutMead { get; private set; }
    public DateTime? DateDernierEvenementMead { get; private set; }
    public string? Blocage { get; private set; }
    public DateTime? DateDebutDepotage { get; private set; }
    public DateTime? DateFinDepotage { get; private set; }
    public string? ConstatDepotageBooking { get; private set; }
    public decimal? ColisDepote { get; private set; }
    public bool Depote { get; private set; }
    public decimal? ColisSortie { get; private set; }
    public string? TypeColis { get; private set; }
    public decimal? NbTotalColis { get; private set; }
    public decimal? NbJrsFranchise { get; private set; }
    public string? CodeBureauDouane { get; private set; }
    public string? Magasin { get; private set; }

    public string? KeyVoyagePrevisionnel { get; private set; }
    public string? KeyVoyageEnlevement { get; private set; }
    public bool FinEnlevement { get; private set; }

    public string? NumDumBooking { get; private set; }
    public DateTime? DateDumBooking { get; private set; }

    public string? KeyPayeur { get; private set; }
    public string? IntitulePayeur { get; private set; }
    public string? NatureOperation { get; private set; }
    public string? KeyChargeOperation { get; private set; }
    public string? NomChargeOperation { get; private set; }
    public decimal? FranchiseSurestarie { get; private set; }

    // Prévision marge block
    public string? KeyFournisseurFret { get; private set; }
    public decimal? PrevisionAchatFret { get; private set; }
    public string? DeviseAchatFret { get; private set; }
    public decimal? TauxChangeAchatFret { get; private set; }
    public decimal? PrevisionVenteFret { get; private set; }
    public string? DeviseVenteFret { get; private set; }
    public decimal? TauxChangeVenteFret { get; private set; }
    public string? KeyFournisseurFraisLocaux { get; private set; }
    public decimal? PrevisionAchatFraisLocaux { get; private set; }
    public decimal? PrevisionVenteFraisLocaux { get; private set; }

    public bool Attachement { get; private set; }
    public bool Supprime { get; private set; }

    public IReadOnlyCollection<ColisageBooking> Colisages => _colisages.AsReadOnly();
    public IReadOnlyCollection<EquipementBooking> Equipements => _equipements.AsReadOnly();

    /// <summary>Direction implied by the second character of the type code (I = Import, E = Export).</summary>
    public string? Sens => TypeBooking is { Length: >= 2 } t ? t[1] switch
    {
        'I' or 'i' => "Import",
        'E' or 'e' => "Export",
        _ => null
    } : null;

    public bool IsAnnule => KeyStatutBooking == StatutAnnule;
    public bool IsConverti => KeyStatutBooking == StatutConverti;
    public bool IsMead => !string.IsNullOrWhiteSpace(KeyStatutBookingMead);

    /// <summary>Total equipment pieces across every equipment line.</summary>
    public decimal NbEquipements => _equipements.Sum(e => e.Qte);

    public void UpdateEntete(
        string? numBooking,
        string? typeBooking,
        string? typeChargement,
        string? incoterm,
        string? mode,
        DateTime? dateValidite,
        string? langueClient,
        string? commentaire,
        string? natureOperation)
    {
        NumBooking = Require(numBooking, nameof(numBooking));
        TypeBooking = Require(typeBooking, nameof(typeBooking));
        TypeChargement = Trim(typeChargement);
        Incoterm = Trim(incoterm);
        Mode = Trim(mode);
        DateValidite = dateValidite;
        LangueClient = Trim(langueClient);
        Commentaire = Trim(commentaire);
        NatureOperation = Trim(natureOperation);
    }

    public void SetAgence(string? agenceBooking, string? intituleAgenceBooking)
    {
        AgenceBooking = Trim(agenceBooking);
        IntituleAgenceBooking = Trim(intituleAgenceBooking);
    }

    public void SetParties(
        string? keyClient, string? intituleClient,
        string? keyAgent, string? intituleAgent,
        string? commercialBooking, string? nomCommercialBooking,
        string? keyPayeur, string? intitulePayeur)
    {
        KeyClient = Trim(keyClient);
        IntituleClient = Trim(intituleClient);
        KeyAgent = Trim(keyAgent);
        IntituleAgent = Trim(intituleAgent);
        CommercialBooking = Trim(commercialBooking);
        NomCommercialBooking = Trim(nomCommercialBooking);
        KeyPayeur = Trim(keyPayeur);
        IntitulePayeur = Trim(intitulePayeur);
    }

    public void SetExpediteur(string? key, string? intitule, string? ville, string? pays, string? intitulePays)
    {
        KeyExpediteur = Trim(key);
        IntituleExpediteur = Trim(intitule);
        VilleExpediteur = Trim(ville);
        PaysExpediteur = Trim(pays);
        IntitulePaysExpediteur = Trim(intitulePays);
    }

    public void SetDestinataire(string? key, string? intitule, string? ville, string? pays, string? intitulePays)
    {
        KeyDestinataire = Trim(key);
        IntituleDestinataire = Trim(intitule);
        VilleDestinataire = Trim(ville);
        PaysDestinataire = Trim(pays);
        IntitulePaysDestinataire = Trim(intitulePays);
    }

    public void SetCompagnie(string? keyCompagnie, string? intituleCompagnie)
    {
        KeyCompagnie = Trim(keyCompagnie);
        IntituleCompagnie = Trim(intituleCompagnie);
    }

    /// <summary>
    /// Sets the cargo description and the hazardous/refrigerated flags, enforcing the class and
    /// temperature rules the legacy form checked before saving.
    /// </summary>
    public void SetMarchandise(
        string? description,
        bool gerbable, bool dangereuse, string? classeDangereuse, bool fragile,
        bool assurance, decimal? valeurMarchandise, string? deviseAssurance,
        bool temperatureDirigee, decimal? temperatureMin, decimal? temperatureMax)
    {
        if (dangereuse && string.IsNullOrWhiteSpace(classeDangereuse))
        {
            throw new BusinessRuleException(
                "booking.dangerous_goods.class",
                "Une classe est obligatoire pour une marchandise dangereuse.");
        }

        if (temperatureDirigee && temperatureMin.HasValue && temperatureMax.HasValue && temperatureMax < temperatureMin)
        {
            throw new BusinessRuleException(
                "booking.temperature", "La température maximale doit être supérieure ou égale à la minimale.");
        }

        if (assurance && (valeurMarchandise is null || valeurMarchandise <= 0))
        {
            throw new BusinessRuleException(
                "booking.assurance.valeur", "La valeur de la marchandise est obligatoire lorsque l'assurance est demandée.");
        }

        DescriptionMarchandise = Trim(description);
        MarchandiseGerbable = gerbable;
        MarchandiseDangereuse = dangereuse;
        ClasseMarchandiseDangereuse = Trim(classeDangereuse);
        MarchandiseFragile = fragile;
        Assurance = assurance;
        ValeurMarchandise = valeurMarchandise;
        DeviseAssurance = Trim(deviseAssurance);
        TemperatureDirigee = temperatureDirigee;
        TemperatureMin = temperatureMin;
        TemperatureMax = temperatureMax;
    }

    public void SetPrevision(
        decimal? poidsBrut, decimal? volume, decimal? mpl, decimal? poidsTaxable, decimal? volumeTaxable,
        decimal? equivalenceVolumePoids, decimal? equivalenceMplPoids, decimal? equivalenceMplVolume)
    {
        PoidsBrutPrevision = poidsBrut;
        VolumePrevision = volume;
        MplPrevision = mpl;
        PoidsTaxablePrevision = poidsTaxable;
        VolumeTaxablePrevision = volumeTaxable;
        EquivalenceVolumePoids = equivalenceVolumePoids;
        EquivalenceMplPoids = equivalenceMplPoids;
        EquivalenceMplVolume = equivalenceMplVolume;
    }

    public void SetItineraire(
        string? portDepart, string? portArrivee,
        DateTime? dateDepartPrevue, DateTime? dateArriveePrevue,
        string? transitTime, DateTime? dateDisponibiliteMarchandise,
        string? provenance, string? destination)
    {
        PortDepart = Trim(portDepart);
        PortArrivee = Trim(portArrivee);
        DateDepartPrevue = dateDepartPrevue;
        DateArriveePrevue = dateArriveePrevue;
        TransitTime = Trim(transitTime);
        DateDisponibiliteMarchandise = dateDisponibiliteMarchandise;
        Provenance = Trim(provenance);
        Destination = Trim(destination);
    }

    public void SetPrevisionMarge(
        string? keyFournisseurFret, decimal? previsionAchatFret, string? deviseAchatFret, decimal? tauxChangeAchatFret,
        decimal? previsionVenteFret, string? deviseVenteFret, decimal? tauxChangeVenteFret,
        string? keyFournisseurFraisLocaux, decimal? previsionAchatFraisLocaux, decimal? previsionVenteFraisLocaux)
    {
        KeyFournisseurFret = Trim(keyFournisseurFret);
        PrevisionAchatFret = previsionAchatFret;
        DeviseAchatFret = Trim(deviseAchatFret);
        TauxChangeAchatFret = tauxChangeAchatFret;
        PrevisionVenteFret = previsionVenteFret;
        DeviseVenteFret = Trim(deviseVenteFret);
        TauxChangeVenteFret = tauxChangeVenteFret;
        KeyFournisseurFraisLocaux = Trim(keyFournisseurFraisLocaux);
        PrevisionAchatFraisLocaux = previsionAchatFraisLocaux;
        PrevisionVenteFraisLocaux = previsionVenteFraisLocaux;
    }

    /// <summary>
    /// Validates the booking before it may be saved, reproducing the legacy form checks.
    /// </summary>
    /// <param name="obligPort">
    /// Whether the company requires a loading port when a planned departure date is set
    /// (the legacy <c>param_oblig_port</c> switch).
    /// </param>
    public void Validate(bool obligPort = false)
    {
        if (Supprime)
        {
            throw new BusinessRuleException("booking.deleted", "Un booking supprimé ne peut pas être validé.");
        }

        if (DateValidite.HasValue && DateValidite.Value.Date < DateCreation.Date)
        {
            throw new BusinessRuleException(
                "booking.validite", "La date de validité ne peut pas être antérieure à la date de création.");
        }

        if (obligPort && DateDepartPrevue.HasValue && string.IsNullOrWhiteSpace(PortDepart))
        {
            throw new BusinessRuleException(
                "booking.port", "Le port de départ est obligatoire lorsqu'une date de départ est prévue.");
        }

        // The legacy rule is asymmetric and easy to get wrong: an import must not originate in Morocco
        // and an export must not be destined for it.
        var sens = Sens;

        if (string.Equals(sens, "Import", StringComparison.OrdinalIgnoreCase)
            && string.Equals(PaysExpediteur, PaysMaroc, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException(
                "booking.import.maroc", "Un booking à l'import ne peut pas avoir le Maroc comme pays expéditeur.");
        }

        if (string.Equals(sens, "Export", StringComparison.OrdinalIgnoreCase)
            && string.Equals(PaysDestinataire, PaysMaroc, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException(
                "booking.export.maroc", "Un booking à l'export ne peut pas avoir le Maroc comme pays destinataire.");
        }

        if (DateDisponibiliteMarchandise.HasValue && DateDepartPrevue.HasValue
            && DateDisponibiliteMarchandise.Value > DateDepartPrevue.Value)
        {
            throw new BusinessRuleException(
                "booking.disponibilite",
                "La date de disponibilité de la marchandise ne peut pas dépasser la date de départ prévue.");
        }

        if (IsComplet && _equipements.Count == 0)
        {
            throw new BusinessRuleException(
                "booking.equipement.required",
                "Un booking complet doit comporter au moins une ligne d'équipement.");
        }

        if (IsGroupage && (NbTotalColis ?? 0m) <= 0m)
        {
            throw new BusinessRuleException(
                "booking.colis.required", "Un booking de groupage doit comporter un nombre de colis supérieur à zéro.");
        }
    }

    public bool IsComplet => string.Equals(TypeChargement, "Complet", StringComparison.OrdinalIgnoreCase);

    public bool IsGroupage =>
        TypeChargement is not null
        && TypeChargement.StartsWith("Groupage", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True when the booking fits in the remaining voyage volume.
    /// </summary>
    /// <remarks>
    /// The legacy capacity check is
    /// <c>isnull(capacite_remorque,0) - sum(isnull(volume_prevision,0))</c> over the bookings linked
    /// through <c>t_detail_voyage_booking</c>, and it uses the <em>prévision</em> volume.
    /// </remarks>
    public bool FitsInCapacity(decimal capaciteRemorque, decimal volumeDejaReserve)
    {
        var reste = capaciteRemorque - volumeDejaReserve;
        return (VolumePrevision ?? 0m) <= reste;
    }

    /// <summary>Links the booking to a dossier and marks it converted.</summary>
    public void LinkDossier(string keyDossier, DateTime dateEvenement)
    {
        KeyDossier = Require(keyDossier, nameof(keyDossier));
        SetStatut(StatutConverti, dateEvenement, null);
    }

    public void LinkCotation(string? keyCotation) => KeyCotation = Trim(keyCotation);

    public void SetStatut(string keyStatut, DateTime dateEvenement, string? commentaire)
    {
        KeyStatutBooking = Require(keyStatut, nameof(keyStatut));
        DateStatutBooking = dateEvenement;
        CommentaireStatutBooking = Trim(commentaire);
    }

    /// <summary>Cancels the booking. The legacy lists exclude cancelled bookings from almost everything.</summary>
    public void Annuler(DateTime dateEvenement, string? motif) =>
        SetStatut(StatutAnnule, dateEvenement, motif);

    /// <summary>Soft delete — mirrors <c>supprimer=1, key_statut_booking='$SBOK'</c>.</summary>
    public void SoftDelete(DateTime dateEvenement)
    {
        Supprime = true;
        KeyStatutBooking = StatutSupprime;
        DateStatutBooking = dateEvenement;
    }

    /// <summary>Attaches the booking to a prévisionnel voyage.</summary>
    public void LinkVoyagePrevisionnel(string? keyVoyage) => KeyVoyagePrevisionnel = Trim(keyVoyage);

    /// <summary>
    /// Attaches the booking to an enlèvement (pick-up) voyage. The legacy code sets
    /// <c>fin_enlevement</c> rather than the (commented-out) <c>key_voyage_enlevement</c> column, so
    /// both are maintained here.
    /// </summary>
    public void LinkVoyageEnlevement(string? keyVoyage, bool finEnlevement)
    {
        KeyVoyageEnlevement = Trim(keyVoyage);
        FinEnlevement = finEnlevement;
    }

    /// <summary>Records dépotage and moves the MEAD status to "En Stock".</summary>
    public void Depotage(
        DateTime dateDepotage, string? constat, decimal? colisDepote, string? magasin, string? codeBureauDouane)
    {
        if (colisDepote is < 0)
        {
            throw new BusinessRuleException("booking.depotage.colis", "Le nombre de colis dépotés ne peut pas être négatif.");
        }

        DateDebutDepotage = dateDepotage;
        ConstatDepotageBooking = Trim(constat);
        ColisDepote = colisDepote;
        Magasin = Trim(magasin);
        CodeBureauDouane = Trim(codeBureauDouane);
        Depote = true;
        KeyStatutBookingMead = StatutMeadEnStock;
        LibelleStatutMead = "En Stock";
        DateDernierEvenementMead = dateDepotage;
    }

    /// <summary>Reverses a dépotage, clearing every depotage field as the legacy "annulation" did.</summary>
    public void CancelDepotage()
    {
        DateDebutDepotage = null;
        DateFinDepotage = null;
        ConstatDepotageBooking = null;
        ColisDepote = null;
        ColisSortie = 0m;
        Depote = false;
        Blocage = null;
        KeyStatutBookingMead = StatutMeadAttenteDepotage;
        LibelleStatutMead = "Attente Dépotage";
        DateDernierEvenementMead = DateTime.UtcNow;
    }

    public void SetColisSortie(decimal? colisSortie)
    {
        if (colisSortie is < 0)
        {
            throw new BusinessRuleException("booking.colis_sortie", "Le nombre de colis sortis ne peut pas être négatif.");
        }

        ColisSortie = colisSortie;
    }

    public void SetNbTotalColis(decimal? nbTotalColis) => NbTotalColis = nbTotalColis;
    public void SetBlocage(string? blocage) => Blocage = Trim(blocage);
    public void SetAttachement(bool attachement) => Attachement = attachement;
    public void SetDum(string? numDum, DateTime? dateDum)
    {
        NumDumBooking = Trim(numDum);
        DateDumBooking = dateDum;
    }

    /// <summary>
    /// Resets the "réel" measures to zero, as the legacy code does whenever the packing detail is
    /// regenerated (they are derived from it).
    /// </summary>
    public void ResetMesuresReelles()
    {
        PoidsBrut = 0m;
        Volume = 0m;
        Mpl = 0m;
        PoidsTaxable = 0m;
        VolumeTaxable = 0m;
    }

    public ColisageBooking AddColisage(ColisageBooking colisage)
    {
        _colisages.Add(colisage);
        return colisage;
    }

    public EquipementBooking AddEquipement(EquipementBooking equipement)
    {
        _equipements.Add(equipement);
        return equipement;
    }

    public void ClearColisages() => _colisages.Clear();

    private static string Require(string? value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Packing line of a booking (<c>t_colisage_booking</c>). No tenant column — scope comes from the
/// parent booking. Keyed by <c>(key_booking, key_colisage_booking)</c> in practice.
/// </summary>
public sealed class ColisageBooking : Entity
{
    private ColisageBooking() { }

    public ColisageBooking(string keyColisageBooking, string keyBooking, string? typeColis, decimal? qte)
    {
        Id = Require(keyColisageBooking, nameof(keyColisageBooking));
        KeyBooking = Require(keyBooking, nameof(keyBooking));
        TypeColis = Trim(typeColis);
        Qte = qte;
    }

    public string KeyColisageBooking => Id;
    public string KeyBooking { get; private set; } = string.Empty;
    public string? TypeColis { get; private set; }
    public decimal? Qte { get; private set; }
    public decimal? Poids { get; private set; }
    public bool Gerbable { get; private set; }
    public decimal? Longueur { get; private set; }
    public decimal? Largeur { get; private set; }
    public decimal? Hauteur { get; private set; }
    public decimal? Volume { get; private set; }
    /// <summary>
    /// Height of the non-stackable part. The legacy INSERT writes the height here when the package is
    /// not stackable and NULL otherwise.
    /// </summary>
    public decimal? HauteurNgerbable { get; private set; }
    public string? TypeEquipement { get; private set; }
    public string? NatureEquipement { get; private set; }

    public void SetDimensions(
        decimal? poids, bool gerbable, decimal? longueur, decimal? largeur, decimal? hauteur, decimal? volume,
        string? typeEquipement, string? natureEquipement)
    {
        Poids = poids;
        Gerbable = gerbable;
        Longueur = longueur;
        Largeur = largeur;
        Hauteur = hauteur;
        Volume = volume;
        // Mirrors the legacy derivation: the non-stackable height is the height only when the package
        // is not stackable.
        HauteurNgerbable = gerbable ? null : hauteur;
        TypeEquipement = Trim(typeEquipement);
        NatureEquipement = Trim(natureEquipement);
    }

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Equipment line of a booking (<c>t_equipement_booking</c>) — <b>exactly five columns</b>. No tenant
/// column.
/// </summary>
public sealed class EquipementBooking : Entity
{
    private EquipementBooking() { }

    public EquipementBooking(string keyLigneEquipement, string keyBooking, string? typeEquipement, string? natureEquipement, decimal qte)
    {
        Id = Require(keyLigneEquipement, nameof(keyLigneEquipement));
        KeyBooking = Require(keyBooking, nameof(keyBooking));
        TypeEquipement = Trim(typeEquipement);
        NatureEquipement = Trim(natureEquipement);
        Qte = qte;
    }

    public string KeyLigneEquipement => Id;
    public string KeyBooking { get; private set; } = string.Empty;
    public string? TypeEquipement { get; private set; }
    /// <summary>Joins <c>t_nature_materiel</c>, whose <c>coefficient</c> yields the TEU count.</summary>
    public string? NatureEquipement { get; private set; }
    public decimal Qte { get; private set; }

    public void Update(string? typeEquipement, string? natureEquipement, decimal qte)
    {
        if (qte < 0)
        {
            throw new BusinessRuleException("equipement.booking.qte", "La quantité d'équipement ne peut pas être négative.");
        }

        TypeEquipement = Trim(typeEquipement);
        NatureEquipement = Trim(natureEquipement);
        Qte = qte;
    }

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Association between a booking and a voyage (<c>t_detail_voyage_booking</c>) — <b>exactly two
/// columns</b>, no surrogate key and no tenant column.
/// </summary>
public sealed class DetailVoyageBooking : Entity
{
    private DetailVoyageBooking() { }

    public DetailVoyageBooking(string keyVoyage, string keyBooking)
    {
        Id = $"{keyVoyage}:{keyBooking}";
        KeyVoyage = Require(keyVoyage, nameof(keyVoyage));
        KeyBooking = Require(keyBooking, nameof(keyBooking));
    }

    public string KeyVoyage { get; private set; } = string.Empty;
    public string KeyBooking { get; private set; } = string.Empty;

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();
}
