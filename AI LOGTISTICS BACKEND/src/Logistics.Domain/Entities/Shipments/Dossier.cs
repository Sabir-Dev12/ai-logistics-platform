using Logistics.Domain.Common;
using Logistics.Domain.Entities.ReferenceData;

namespace Logistics.Domain.Entities.Shipments;

/// <summary>
/// Dossier (shipment file) — <c>t_dossiers</c>. Crucially the tenant column here is called
/// <c>societe</c>, whereas <c>t_voyages</c> uses <c>soc_voyage</c> and the trace tables use
/// <c>soc_trace</c>. That inconsistency is inherited from the legacy schema and preserved.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="KeyVoyageInter"/> is the <em>single active</em> link between a dossier and a voyage.
/// The legacy design once used a many-to-many join table (<c>t_detail_dossier</c>, never inserted
/// into, and now dead); <c>t_dossiers_voyage</c> survives as an audit/association table written
/// alongside this column.
/// </para>
/// <para>
/// A non-null <see cref="MagasinDouane"/> is what pushes a dossier — and its voyage — into the MEAD
/// (bonded warehouse) workflow.
/// </para>
/// </remarks>
public sealed class Dossier : Entity, ITenantScoped
{
    public const string StatutCree = "$DOSC";
    public const string LibelleCree = "Dossier Créé";
    public const string StatutSupprime = "$SDOS";
    public const string LibelleSupprime = "Dossier Supprimé";

    public const string StatutMeadAttenteDepotage = "$ATDEPOT";
    public const string StatutMeadDepote = "$DEPOT";
    public const string StatutMeadSortie = "$SORTD";

    private readonly List<ColisageDossier> _colisages = [];

    private Dossier() { }

    public Dossier(string keyDossier, string societeKey, string? numDossier, string? typeDossier, DateTime dateCreation)
    {
        Id = Require(keyDossier, nameof(keyDossier));
        SocieteKey = Require(societeKey, nameof(societeKey));
        NumDossier = Require(numDossier, nameof(numDossier));
        TypeDossier = Require(typeDossier, nameof(typeDossier));
        DateCreation = dateCreation;
        KeyStatutDossier = StatutCree;
        LibelleStatutDossier = LibelleCree;
        // The legacy creation paths disagree on this: one writes the literal, the other relies on a
        // database default. Stating it explicitly removes the ambiguity.
        StatutFacturation = StatutFacturation.ANePasFacturer;
        StatutCloture = StatutClotureDossier.Encours;
        Supprime = false;
    }

    public string KeyDossier => Id;
    /// <summary>Tenant column — literally <c>societe</c> on this table.</summary>
    public string SocieteKey { get; private set; } = string.Empty;
    public string? NumDossier { get; private set; }
    public DateTime DateCreation { get; private set; }
    public string? TypeDossier { get; private set; }

    /// <summary>The active voyage link. NULL for a dossier not yet loaded on a voyage.</summary>
    public string? KeyVoyageInter { get; private set; }
    public string? KeyBooking { get; private set; }
    public string? KeyCotation { get; private set; }

    public string? KeyAgenceExploitation { get; private set; }
    public string? IntituleAgenceExploitation { get; private set; }
    public string? KeyAgenceCommerciale { get; private set; }
    public string? IntituleAgenceCommerciale { get; private set; }
    public string? KeyAgenceSaisie { get; private set; }
    public string? IntituleAgenceSaisie { get; private set; }

    public string? KeyClient { get; private set; }
    public string? IntituleClient { get; private set; }
    public string? CommercialDossier { get; private set; }
    public string? NomCommercialDossier { get; private set; }
    public string? KeyExploitant { get; private set; }
    public string? NomExploitant { get; private set; }

    public string? KeyAgent { get; private set; }
    public string? IntituleAgent { get; private set; }
    public string? RefAgent { get; private set; }
    public string? KeyTransitaire { get; private set; }
    public string? IntituleTransitaire { get; private set; }
    public string? ReferenceTransitaire { get; private set; }

    public string? NumHbl { get; private set; }

    // Expediteur / destinataire snapshots (denormalised on the legacy row)
    public string? KeyExpediteur { get; private set; }
    public string? IntituleExpediteur { get; private set; }
    public string? Adresse1Expediteur { get; private set; }
    public string? Adresse2Expediteur { get; private set; }
    public string? CpExpediteur { get; private set; }
    public string? VilleExpediteur { get; private set; }
    public string? PaysExpediteur { get; private set; }
    public string? IntitulePaysExpediteur { get; private set; }
    public string? RefExpediteur { get; private set; }

    public string? KeyDestinataire { get; private set; }
    public string? IntituleDestinataire { get; private set; }
    public string? Adresse1Destinataire { get; private set; }
    public string? Adresse2Destinataire { get; private set; }
    public string? CpDestinataire { get; private set; }
    public string? VilleDestinataire { get; private set; }
    public string? PaysDestinataire { get; private set; }
    public string? IntitulePaysDestinataire { get; private set; }
    public string? RefDestinataire { get; private set; }

    public string? KeyPayeur { get; private set; }
    public string? IntitulePayeur { get; private set; }
    public string? KeyPayeurMead { get; private set; }
    public string? IntitulePayeurMead { get; private set; }
    public string? KeyClientMead { get; private set; }
    public string? IntituleClientMead { get; private set; }

    /// <summary>
    /// Incoterm code. The legacy creation INSERT writes <c>incoterm</c> while
    /// <c>ClassIncoterm.Referencee</c> reads <c>incoterm_dossier</c>; the write path is treated as
    /// authoritative here, and the discrepancy is flagged in the README.
    /// </summary>
    public string? Incoterm { get; private set; }

    public string? DescriptionMarchandise { get; private set; }
    public bool MarchandiseGerbable { get; private set; }
    public bool MarchandiseDangereuse { get; private set; }
    public string? ClasseMarchandiseDangereuse { get; private set; }
    public bool MarchandiseFragile { get; private set; }
    public bool Assurance { get; private set; }
    public decimal? ValeurMarchandise { get; private set; }
    public string? DeviseValeurMarchandise { get; private set; }
    public decimal? TauxChangeValMarch { get; private set; }
    public string? Marque { get; private set; }

    public decimal? NbColis { get; private set; }
    public string? TypeColis { get; private set; }
    /// <summary>Count of suspended packages — a number despite the flag-like name.</summary>
    public decimal? Suspendu { get; private set; }
    public decimal? PoidsBrut { get; private set; }
    public decimal? Volume { get; private set; }
    public decimal? Mpl { get; private set; }
    /// <summary>Taxable weight — the basis of the sales allocation pro-rata split.</summary>
    public decimal? PoidsTaxable { get; private set; }
    public decimal? VolumeTaxable { get; private set; }
    public decimal? EquivalenceVolumePoids { get; private set; }
    public decimal? EquivalenceMplPoids { get; private set; }
    public decimal? EquivalenceMplVolume { get; private set; }

    /// <summary>Ordering inside the voyage; drives the legacy dossier renumbering <c>num-&lt;position&gt;</c>.</summary>
    public int? Position { get; private set; }
    public string? CommentaireDossier { get; private set; }
    public string? ServiceDossier { get; private set; }
    public string? NatureOperation { get; private set; }

    public string? MagasinDouane { get; private set; }
    public string? KeyStatutDossier { get; private set; }
    public string? LibelleStatutDossier { get; private set; }
    public DateTime? DateStatutDossier { get; private set; }
    public DateTime? DateDernierEvenement { get; private set; }

    public decimal? MontantAssigne { get; private set; }
    public string? DeviseMontantAssigne { get; private set; }

    // Customs
    public string? HsCode { get; private set; }
    public string? CodeRegime { get; private set; }
    public string? CodeBureau { get; private set; }
    public string? NumDum { get; private set; }
    public DateTime? DateDum { get; private set; }
    public decimal? ValeurDeclareeDouane { get; private set; }
    public string? LivraisonPar { get; private set; }

    public bool TemperatureDirigee { get; private set; }
    public decimal? TemperatureMin { get; private set; }
    public decimal? TemperatureMax { get; private set; }
    public decimal? MontantFret { get; private set; }
    public string? DeviseFret { get; private set; }
    public decimal? NbUmMead { get; private set; }

    public bool Attachement { get; private set; }
    public bool Supprime { get; private set; }

    // Depotage / warehouse
    public DateTime? DateDepotage { get; private set; }
    public LieuDepotage? LieuDepotage { get; private set; }
    public string? ConstatDepotage { get; private set; }
    public decimal? ColisDepote { get; private set; }
    public decimal? ColisSortie { get; private set; }
    public string? RefDepotage { get; private set; }
    public DateTime? DateClotureIncident { get; private set; }
    public string? CommentaireIncident { get; private set; }
    /// <summary>
    /// NOTE: the legacy query spells this <c>commentaite_cloture_incident</c>, almost certainly a typo
    /// for the name used here. The correct spelling is modelled and the discrepancy is documented.
    /// </summary>
    public string? CommentaireClotureIncident { get; private set; }

    public string? KeyStatutMead { get; private set; }
    public string? LibelleStatutDossierMead { get; private set; }
    public DateTime? DateDernierEvenementMead { get; private set; }
    public string? CommentaireDernierEvenementMead { get; private set; }

    public StatutFacturation StatutFacturation { get; private set; }
    public StatutClotureDossier StatutCloture { get; private set; }
    public string? Blocage { get; private set; }
    public bool Litige { get; private set; }
    public bool AssigneRegle { get; private set; }
    public bool DossierRegle { get; private set; }
    public decimal? TotalAssigneAchat { get; private set; }
    public decimal? TotalAssigneVente { get; private set; }
    public decimal? RemiseMagasinage { get; private set; }
    public bool Cod { get; private set; }

    public DateTime? DateEchange { get; private set; }
    public DateTime? DateDebut { get; private set; }
    public DateTime? DateFin { get; private set; }
    public DateTime? DateSortie { get; private set; }
    public string? NumBad { get; private set; }
    public DateTime? DateBad { get; private set; }

    public IReadOnlyCollection<ColisageDossier> Colisages => _colisages.AsReadOnly();

    /// <summary>Non-null warehouse means the dossier follows the MEAD workflow.</summary>
    public bool IsMead => !string.IsNullOrWhiteSpace(MagasinDouane);

    /// <summary>True while packages are still physically in the warehouse.</summary>
    public bool HasStockRemaining => (ColisDepote.GetValueOrDefault() - ColisSortie.GetValueOrDefault()) != 0;

    public void UpdateEntete(
        string? numDossier,
        string? typeDossier,
        string? keyClient,
        string? intituleClient,
        string? natureOperation,
        string? serviceDossier,
        int? position,
        string? commentaireDossier)
    {
        NumDossier = Require(numDossier, nameof(numDossier));
        TypeDossier = Require(typeDossier, nameof(typeDossier));
        KeyClient = Trim(keyClient);
        IntituleClient = Trim(intituleClient);
        NatureOperation = Trim(natureOperation);
        ServiceDossier = Trim(serviceDossier);
        Position = position;
        CommentaireDossier = Trim(commentaireDossier);
    }

    public void SetAgences(
        string? keyExploitation, string? intituleExploitation,
        string? keyCommerciale, string? intituleCommerciale,
        string? keySaisie, string? intituleSaisie)
    {
        KeyAgenceExploitation = Trim(keyExploitation);
        IntituleAgenceExploitation = Trim(intituleExploitation);
        KeyAgenceCommerciale = Trim(keyCommerciale);
        IntituleAgenceCommerciale = Trim(intituleCommerciale);
        KeyAgenceSaisie = Trim(keySaisie);
        IntituleAgenceSaisie = Trim(intituleSaisie);
    }

    public void SetParties(
        string? keyExpediteur, string? intituleExpediteur,
        string? keyDestinataire, string? intituleDestinataire,
        string? keyPayeur, string? intitulePayeur,
        string? keyAgent, string? intituleAgent, string? refAgent)
    {
        KeyExpediteur = Trim(keyExpediteur);
        IntituleExpediteur = Trim(intituleExpediteur);
        KeyDestinataire = Trim(keyDestinataire);
        IntituleDestinataire = Trim(intituleDestinataire);
        KeyPayeur = Trim(keyPayeur);
        IntitulePayeur = Trim(intitulePayeur);
        KeyAgent = Trim(keyAgent);
        IntituleAgent = Trim(intituleAgent);
        RefAgent = Trim(refAgent);
    }

    public void SetAdresses(
        string? adresse1Expediteur, string? adresse2Expediteur, string? cpExpediteur,
        string? villeExpediteur, string? paysExpediteur, string? intitulePaysExpediteur,
        string? adresse1Destinataire, string? adresse2Destinataire, string? cpDestinataire,
        string? villeDestinataire, string? paysDestinataire, string? intitulePaysDestinataire)
    {
        Adresse1Expediteur = Trim(adresse1Expediteur);
        Adresse2Expediteur = Trim(adresse2Expediteur);
        CpExpediteur = Trim(cpExpediteur);
        VilleExpediteur = Trim(villeExpediteur);
        PaysExpediteur = Trim(paysExpediteur);
        IntitulePaysExpediteur = Trim(intitulePaysExpediteur);
        Adresse1Destinataire = Trim(adresse1Destinataire);
        Adresse2Destinataire = Trim(adresse2Destinataire);
        CpDestinataire = Trim(cpDestinataire);
        VilleDestinataire = Trim(villeDestinataire);
        PaysDestinataire = Trim(paysDestinataire);
        IntitulePaysDestinataire = Trim(intitulePaysDestinataire);
    }

    /// <summary>
    /// Sets the cargo description and the physical measures. <see cref="PoidsTaxable"/> is the one the
    /// sales allocation pro-rata depends on, so it is validated as non-negative.
    /// </summary>
    public void SetMarchandise(
        string? descriptionMarchandise,
        bool gerbable, bool dangereuse, string? classeDangereuse, bool fragile, bool assurance,
        decimal? valeurMarchandise, string? deviseValeur, decimal? tauxChangeValMarch,
        decimal? nbColis, string? typeColis, decimal? suspendu, string? marque)
    {
        DescriptionMarchandise = Trim(descriptionMarchandise);
        MarchandiseGerbable = gerbable;
        MarchandiseDangereuse = dangereuse;
        ClasseMarchandiseDangereuse = Trim(classeDangereuse);

        if (dangereuse && string.IsNullOrWhiteSpace(ClasseMarchandiseDangereuse))
        {
            throw new BusinessRuleException(
                "dossier.dangerous_goods.class",
                "Une classe de marchandise dangereuse est obligatoire pour une marchandise dangereuse.");
        }

        MarchandiseFragile = fragile;
        Assurance = assurance;
        ValeurMarchandise = valeurMarchandise;
        DeviseValeurMarchandise = Trim(deviseValeur);
        TauxChangeValMarch = tauxChangeValMarch;
        NbColis = nbColis;
        TypeColis = Trim(typeColis);
        Suspendu = suspendu;
        Marque = Trim(marque);
    }

    public void SetMesures(
        decimal? poidsBrut, decimal? volume, decimal? mpl,
        decimal? poidsTaxable, decimal? volumeTaxable,
        decimal? equivalenceVolumePoids, decimal? equivalenceMplPoids, decimal? equivalenceMplVolume)
    {
        if (poidsTaxable is < 0)
        {
            throw new BusinessRuleException("dossier.poids_taxable", "Le poids taxable ne peut pas être négatif.");
        }

        PoidsBrut = poidsBrut;
        Volume = volume;
        Mpl = mpl;
        PoidsTaxable = poidsTaxable;
        VolumeTaxable = volumeTaxable;
        EquivalenceVolumePoids = equivalenceVolumePoids;
        EquivalenceMplPoids = equivalenceMplPoids;
        EquivalenceMplVolume = equivalenceMplVolume;
    }

    /// <summary>
    /// Attaches the dossier to a voyage. Only one voyage at a time is active; the caller is responsible
    /// for writing the <c>t_dossiers_voyage</c> association row.
    /// </summary>
    public void AssignVoyage(string? keyVoyage, int? position = null)
    {
        if (Supprime)
        {
            throw new BusinessRuleException("dossier.deleted", "Un dossier supprimé ne peut pas être affecté à un voyage.");
        }

        KeyVoyageInter = Trim(keyVoyage);
        Position = position ?? Position;
    }

    public void SetIncoterm(string? incoterm) => Incoterm = Trim(incoterm);
    public void LinkBooking(string? keyBooking) => KeyBooking = Trim(keyBooking);
    public void LinkCotation(string? keyCotation) => KeyCotation = Trim(keyCotation);

    public void SetMagasinDouane(string? magasinDouane)
    {
        MagasinDouane = Trim(magasinDouane);

        if (IsMead)
        {
            KeyStatutMead ??= StatutMeadAttenteDepotage;
            LibelleStatutDossierMead ??= "Attente Dépotage";
            DateDernierEvenementMead = DateTime.UtcNow;
        }
    }

    public void SetCustoms(
        string? hsCode, string? codeRegime, string? codeBureau,
        string? numDum, DateTime? dateDum, decimal? valeurDeclareeDouane, string? livraisonPar)
    {
        HsCode = Trim(hsCode);
        CodeRegime = Trim(codeRegime);
        CodeBureau = Trim(codeBureau);
        NumDum = Trim(numDum);
        DateDum = dateDum;
        ValeurDeclareeDouane = valeurDeclareeDouane;
        LivraisonPar = Trim(livraisonPar);
    }

    public void SetTransport(
        bool temperatureDirigee, decimal? temperatureMin, decimal? temperatureMax,
        decimal? montantFret, string? deviseFret)
    {
        if (temperatureMin.HasValue && temperatureMax.HasValue && temperatureMin > temperatureMax)
        {
            throw new BusinessRuleException(
                "dossier.temperature", "La température minimale ne peut pas dépasser la température maximale.");
        }

        TemperatureDirigee = temperatureDirigee;
        TemperatureMin = temperatureMin;
        TemperatureMax = temperatureMax;
        MontantFret = montantFret;
        DeviseFret = Trim(deviseFret);
    }

    /// <summary>Records unpacking and moves the MEAD status to "Dépoté".</summary>
    /// <remarks>
    /// The MEAD precondition is enforced here rather than in the service, so the invariant cannot be
    /// bypassed by any other caller.
    /// </remarks>
    public void Depotage(
        DateTime dateDepotage, LieuDepotage lieuDepotage, string? constatDepotage,
        decimal? colisDepote, string? refDepotage)
    {
        if (!IsMead)
        {
            throw new BusinessRuleException(
                "dossier.depotage.not_mead",
                "Le dépotage n'est possible que pour un dossier MEAD (magasin de douane renseigné).");
        }

        if (colisDepote is < 0)
        {
            throw new BusinessRuleException("dossier.depotage.colis", "Le nombre de colis dépotés ne peut pas être négatif.");
        }

        DateDepotage = dateDepotage;
        LieuDepotage = lieuDepotage;
        ConstatDepotage = Trim(constatDepotage);
        ColisDepote = colisDepote;
        RefDepotage = Trim(refDepotage);
        KeyStatutMead = StatutMeadDepote;
        LibelleStatutDossierMead = "Dépoté";
        DateDernierEvenementMead = dateDepotage;
    }

    /// <summary>Reverses a depotage — the legacy "annulation dépotage" clears every depotage field.</summary>
    public void CancelDepotage()
    {
        DateDepotage = null;
        LieuDepotage = null;
        ConstatDepotage = null;
        ColisDepote = null;
        ColisSortie = 0m;
        Blocage = null;
        KeyStatutMead = StatutMeadAttenteDepotage;
        LibelleStatutDossierMead = "Attente Dépotage";
        DateDernierEvenementMead = DateTime.UtcNow;
    }

    public void SetColisSortie(decimal? colisSortie)
    {
        if (colisSortie is < 0)
        {
            throw new BusinessRuleException("dossier.colis_sortie", "Le nombre de colis sortis ne peut pas être négatif.");
        }

        ColisSortie = colisSortie;
    }

    public void SetStatutDossier(string keyStatut, string libelleStatut, DateTime dateEvenement)
    {
        KeyStatutDossier = Require(keyStatut, nameof(keyStatut));
        LibelleStatutDossier = Require(libelleStatut, nameof(libelleStatut));
        DateStatutDossier = dateEvenement;
        DateDernierEvenement = dateEvenement;
    }

    public void SetStatutFacturation(StatutFacturation statut)
    {
        if (statut == StatutFacturation.Facture && Supprime)
        {
            throw new BusinessRuleException("dossier.facturation.deleted", "Un dossier supprimé ne peut pas être facturé.");
        }

        StatutFacturation = statut;
    }

    public void SetStatutCloture(StatutClotureDossier statut) => StatutCloture = statut;

    public void SetIncident(DateTime? dateClotureIncident, string? commentaireIncident, string? commentaireClotureIncident)
    {
        DateClotureIncident = dateClotureIncident;
        CommentaireIncident = Trim(commentaireIncident);
        CommentaireClotureIncident = Trim(commentaireClotureIncident);
    }

    public void SetLitige(bool litige) => Litige = litige;
    public void SetBlocage(string? blocage) => Blocage = Trim(blocage);

    public void SetTotauxAssigne(decimal? totalAssigneAchat, decimal? totalAssigneVente)
    {
        TotalAssigneAchat = totalAssigneAchat;
        TotalAssigneVente = totalAssigneVente;
    }

    public void MarkAssigneRegle(bool regle) => AssigneRegle = regle;
    public void MarkDossierRegle(bool regle) => DossierRegle = regle;

    public void SetAttachement(bool attachement) => Attachement = attachement;
    public void SetCod(bool cod) => Cod = cod;
    public void SetRemiseMagasinage(decimal? remise) => RemiseMagasinage = remise;

    public void SetBad(string? numBad, DateTime? dateBad)
    {
        NumBad = Trim(numBad);
        DateBad = dateBad;
    }

    public void SetDatesSansVoyage(DateTime? dateDebut, DateTime? dateFin)
    {
        DateDebut = dateDebut;
        DateFin = dateFin;
    }

    public void SetDateSortie(DateTime? dateSortie) => DateSortie = dateSortie;
    public void SetDateEchange(DateTime? dateEchange) => DateEchange = dateEchange;

    /// <summary>Soft delete — mirrors <c>supprimer=1</c> plus <c>key_statut_dossier='$SDOS'</c>.</summary>
    public void SoftDelete()
    {
        Supprime = true;
        KeyStatutDossier = StatutSupprime;
        LibelleStatutDossier = LibelleSupprime;
    }

    public ColisageDossier AddColisage(ColisageDossier colisage)
    {
        _colisages.Add(colisage);
        return colisage;
    }

    public void ClearColisages() => _colisages.Clear();

    private static string Require(string? value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Packing line of a dossier (<c>t_colisage_dossier</c>) — one row per package type with its count.
/// No tenant column: scope comes from the parent dossier.
/// </summary>
/// <remarks>
/// The legacy table has never been inserted into with <c>key_ligne_colisage</c>, and it is updated and
/// deleted by that numeric key, so the column is database-generated. It is modelled as an identity here.
/// There is no soft-delete column — rows are hard-deleted.
/// </remarks>
public sealed class ColisageDossier
{
    private ColisageDossier() { }

    public ColisageDossier(string keyDossier, string typeColis, decimal nbColis, string? natureMarchandise)
    {
        KeyDossier = string.IsNullOrWhiteSpace(keyDossier)
            ? throw new ArgumentException("Dossier key is required.", nameof(keyDossier))
            : keyDossier.Trim();
        TypeColis = string.IsNullOrWhiteSpace(typeColis)
            ? throw new ArgumentException("Package type is required.", nameof(typeColis))
            : typeColis.Trim();

        if (nbColis < 0)
        {
            throw new BusinessRuleException("colisage.nb_colis", "Le nombre de colis ne peut pas être négatif.");
        }

        NbColis = nbColis;
        NatureMarchandise = string.IsNullOrWhiteSpace(natureMarchandise) ? null : natureMarchandise.Trim();
    }

    /// <summary>Surrogate identity; the legacy table has no application-supplied key.</summary>
    public int KeyLigneColisage { get; private set; }
    public string KeyDossier { get; private set; } = string.Empty;
    public string TypeColis { get; private set; } = string.Empty;
    public decimal NbColis { get; private set; }
    public string? NatureMarchandise { get; private set; }
    public decimal? PoidsBrut { get; private set; }
    public decimal? Volume { get; private set; }
    public string? Marque { get; private set; }
    public string? CodeNgp { get; private set; }
    public decimal? ValeurDevise { get; private set; }
    /// <summary>ONSSA (Moroccan food-safety) flag; the legacy inserts always write the literal 0.</summary>
    public bool Onssa { get; private set; }

    public void Update(decimal nbColis, string? natureMarchandise, string? marque, string? codeNgp)
    {
        if (nbColis < 0)
        {
            throw new BusinessRuleException("colisage.nb_colis", "Le nombre de colis ne peut pas être négatif.");
        }

        NbColis = nbColis;
        NatureMarchandise = string.IsNullOrWhiteSpace(natureMarchandise) ? null : natureMarchandise.Trim();
        Marque = string.IsNullOrWhiteSpace(marque) ? null : marque.Trim();
        CodeNgp = string.IsNullOrWhiteSpace(codeNgp) ? null : codeNgp.Trim();
    }

    /// <summary>
    /// Back-fills the physical measures. The legacy code only does this when the header carries a single
    /// matching packing line, which is why these are populated separately from the count.
    /// </summary>
    public void SetMesures(decimal? poidsBrut, decimal? volume, decimal? valeurDevise)
    {
        PoidsBrut = poidsBrut;
        Volume = volume;
        ValeurDevise = valeurDevise;
    }

    public void SetOnssa(bool onssa) => Onssa = onssa;
}

/// <summary>
/// Association between a dossier and a voyage (<c>t_dossiers_voyage</c>), written alongside
/// <see cref="Dossier.KeyVoyageInter"/> as an audit trail of every pairing.
/// </summary>
public sealed class DossierVoyage : Entity
{
    private DossierVoyage() { }

    public DossierVoyage(string keyDossier, string keyVoyage)
    {
        Id = $"{keyDossier}:{keyVoyage}";
        KeyDossier = Require(keyDossier, nameof(keyDossier));
        KeyVoyage = Require(keyVoyage, nameof(keyVoyage));
    }

    public string KeyDossier { get; private set; } = string.Empty;
    public string KeyVoyage { get; private set; } = string.Empty;

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();
}
