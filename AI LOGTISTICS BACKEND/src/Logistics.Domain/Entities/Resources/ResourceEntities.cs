using Logistics.Domain.Common;
using Logistics.Domain.Entities.ReferenceData;

namespace Logistics.Domain.Entities.Resources;

/// <summary>Availability status of a <c>t_materiel</c> row.</summary>
/// <remarks>
/// The legacy availability query is <c>STATUT in ('D','R')</c>: a vehicle under repair is still part of
/// the fleet, so "disponible" and "en réparation" both count as held. Only <see cref="Sorti"/> removes
/// it.
/// </remarks>
public enum StatutMateriel
{
    Disponible,
    EnReparation,
    Sorti
}

/// <summary>Which fuel product a per-kilometre cost row applies to (<c>produit_gasoil</c>).</summary>
/// <remarks>
/// A tractor normally has <b>two</b> live rows in the same validity window — one per product — so a cost
/// lookup that ignores this discriminator returns an arbitrary one of the two.
/// </remarks>
public enum ProduitGasoil
{
    Gasoil,
    AdBlue
}

/// <summary>
/// Personnel master and login identity (<c>t_personnel</c>).
/// </summary>
/// <remarks>
/// <para>
/// Tenant column is <c>soc_personnel</c>.
/// </para>
/// <para>
/// <b>This table has no <c>supprimer</c> column.</b> Departure is recorded as
/// <c>date_sortie</c> + <c>motif_sortie</c>, with <c>bloque=1</c> and the login cleared. Any generic
/// "exclude deleted rows" helper that assumes <c>supprimer</c> will silently mishandle personnel, which
/// is why <see cref="IsActif"/> is defined here rather than left to a convention.
/// </para>
/// </remarks>
public sealed class Personnel : Entity, ITenantScoped
{
    /// <summary>Fonction key that identifies a driver — the costing engine filters on this literal.</summary>
    public const string FonctionChauffeur = "$CHAUFFEUR";

    private Personnel() { }

    public Personnel(string keyPersonnel, string societeKey, string? nomPersonnel, string? prenomPersonnel = null)
    {
        Id = Require(keyPersonnel, nameof(keyPersonnel));
        SocieteKey = Require(societeKey, nameof(societeKey));
        NomPersonnel = Trim(nomPersonnel);
        PrenomPersonnel = Trim(prenomPersonnel);
    }

    public string KeyPersonnel => Id;
    public string SocieteKey { get; private set; } = string.Empty;
    public string? Matricule { get; private set; }
    public string? AgencePersonnel { get; private set; }
    public string? KeyFonctionPersonnel { get; private set; }
    public string? KeyServicePersonnel { get; private set; }
    public string? KeyDepartementPersonnel { get; private set; }
    public string? NomPersonnel { get; private set; }
    public string? PrenomPersonnel { get; private set; }
    public string? Adresse1Personnel { get; private set; }
    public string? Adresse2Personnel { get; private set; }
    public string? CpPersonnel { get; private set; }
    public string? VillePersonnel { get; private set; }
    public string? PaysPersonnel { get; private set; }
    public string? GsmPersonnel { get; private set; }
    public string? FixePersonnel { get; private set; }
    public string? EmailPersonnel { get; private set; }
    public DateTime? DateEmbauche { get; private set; }
    public DateTime? DateNaissance { get; private set; }
    public string? SituationFamiliale { get; private set; }

    /// <summary>Departure date. Non-null means the employee has left — this table's only soft delete.</summary>
    public DateTime? DateSortie { get; private set; }
    public string? MotifSortie { get; private set; }

    public bool Bloque { get; private set; }
    public bool Admin { get; private set; }
    public bool AccesWeb { get; private set; }
    public bool PremiereConnexion { get; private set; }
    public string? IdentifiantApp { get; private set; }
    /// <summary>Encoded by <c>tools.Codage_Motpasse</c>; never returned by the API.</summary>
    public string? PassWord { get; private set; }

    /// <summary>
    /// Stores a new password, <b>already encoded</b>, and the first-connection flag.
    /// </summary>
    /// <remarks>
    /// Takes the encoded value rather than the plaintext deliberately: the encoding lives in
    /// <see cref="MotDePasseLegacy"/> so that the entity never holds a plaintext password, not even briefly as
    /// a parameter.
    /// <para>
    /// Clearing <c>premiere_connexion</c> is the self-service case — <c>ClassPersonnel.ModifyPWD</c>, line 968.
    /// The administrator case is the opposite and is a separate method, because the two differ in exactly the
    /// flag an auditor would ask about.
    /// </para>
    /// </remarks>
    public void ChangerMotDePasse(string motDePasseEncode, bool premiereConnexion)
    {
        PassWord = string.IsNullOrWhiteSpace(motDePasseEncode) ? null : motDePasseEncode.Trim();
        PremiereConnexion = premiereConnexion;
    }

    /// <summary>
    /// Forces a password change at the next sign-in.
    /// </summary>
    /// <remarks>
    /// What an administrator setting <b>someone else's</b> password does — <c>ClassPersonnel.vb:812</c> sets
    /// <c>premiere_connexion=1</c> on the target, so the person who knows the temporary password cannot keep
    /// using it.
    /// </remarks>
    public void ForcerChangementMotDePasse() => PremiereConnexion = true;
    public string? PassWordMessagerie { get; private set; }
    public string? TypeEnvoieMail { get; private set; }

    public string? CnssPersonnel { get; private set; }
    public string? CinPersonnel { get; private set; }
    public string? CimrPersonnel { get; private set; }
    public string? AssurancePersonnel { get; private set; }
    public string? BanquePersonnel { get; private set; }
    public string? RibPersonnel { get; private set; }
    public string? MagasinMead { get; private set; }
    public bool Attachement { get; private set; }

    /// <summary>
    /// <b>Monthly</b> cost of the employee, in <see cref="DeviseCout"/>.
    /// </summary>
    /// <remarks>
    /// Despite the bare "Coût" label in the UI, this is a monthly figure — the costing engine's comment
    /// reads "les matériels qui ont un coût mensuel" and it divides by the month's trajet count. There is
    /// no per-day cost column anywhere in the schema.
    /// </remarks>
    public decimal? Cout { get; private set; }
    public string? DeviseCout { get; private set; }

    public DateTime? DateDernierTrajet { get; private set; }
    public string? DerniereDestination { get; private set; }
    public string? StatutChauffeur { get; private set; }

    /// <summary>An employee is active while no departure date has been recorded.</summary>
    public bool IsActif => DateSortie is null;

    public bool IsChauffeur => string.Equals(KeyFonctionPersonnel, FonctionChauffeur, StringComparison.OrdinalIgnoreCase);

    public string NomComplet => string.Join(' ', new[] { NomPersonnel, PrenomPersonnel }.Where(p => !string.IsNullOrWhiteSpace(p)));

    /// <summary>
    /// The employee's monthly cost apportioned across a number of trips.
    /// </summary>
    /// <remarks>
    /// This is the legacy <c>cout_chauffeur / nb_trajet</c>. A non-positive trip count yields 0 rather
    /// than the full monthly cost: dividing by zero is undefined, and charging a whole month to a voyage
    /// would overstate it by the number of trips that month.
    /// </remarks>
    public decimal CoutParTrajet(int nbTrajets) => Apportion(Cout, nbTrajets);

    public void SetIdentite(
        string? matricule, string? keyFonction, string? keyService, string? keyDepartement,
        string? nom, string? prenom, DateTime? dateNaissance, string? situationFamiliale)
    {
        Matricule = Trim(matricule);
        KeyFonctionPersonnel = Trim(keyFonction);
        KeyServicePersonnel = Trim(keyService);
        KeyDepartementPersonnel = Trim(keyDepartement);
        NomPersonnel = Trim(nom);
        PrenomPersonnel = Trim(prenom);
        DateNaissance = dateNaissance;
        SituationFamiliale = Trim(situationFamiliale);
    }

    public void SetCoordonnees(
        string? adresse1, string? adresse2, string? cp, string? ville, string? pays,
        string? gsm, string? fixe, string? email, string? agence)
    {
        Adresse1Personnel = Trim(adresse1);
        Adresse2Personnel = Trim(adresse2);
        CpPersonnel = Trim(cp);
        VillePersonnel = Trim(ville);
        PaysPersonnel = Trim(pays);
        GsmPersonnel = Trim(gsm);
        FixePersonnel = Trim(fixe);
        EmailPersonnel = Trim(email);
        AgencePersonnel = Trim(agence);
    }

    public void SetAdministratif(
        string? cnss, string? cin, string? cimr, string? assurance,
        string? banque, string? rib, string? magasinMead)
    {
        CnssPersonnel = Trim(cnss);
        CinPersonnel = Trim(cin);
        CimrPersonnel = Trim(cimr);
        AssurancePersonnel = Trim(assurance);
        BanquePersonnel = Trim(banque);
        RibPersonnel = Trim(rib);
        MagasinMead = Trim(magasinMead);
    }

    public void SetEmbauche(DateTime? dateEmbauche)
    {
        if (dateEmbauche.HasValue && DateSortie.HasValue && dateEmbauche.Value.Date > DateSortie.Value.Date)
        {
            throw new BusinessRuleException(
                "personnel.embauche", "La date d'embauche ne peut pas être postérieure à la date de sortie.");
        }

        DateEmbauche = dateEmbauche;
    }

    /// <summary>
    /// Records the monthly cost and its currency.
    /// </summary>
    public void SetCout(decimal? cout, string? deviseCout)
    {
        if (cout is < 0m)
        {
            throw new BusinessRuleException("personnel.cout", "Le coût mensuel ne peut pas être négatif.");
        }

        Cout = cout;
        DeviseCout = Trim(deviseCout);
    }

    /// <summary>
    /// Records the departure: the date, the reason, and the login revocation the legacy code performed.
    /// </summary>
    /// <remarks>
    /// Clearing <see cref="IdentifiantApp"/> and <see cref="PassWord"/> is part of the legacy departure
    /// sequence, not an extra. Leaving them set would keep a departed employee's account usable, since
    /// nothing else in the schema disables it.
    /// </remarks>
    public void EnregistrerSortie(DateTime dateSortie, string? motif)
    {
        if (dateSortie.Date < DateEmbauche?.Date)
        {
            throw new BusinessRuleException(
                "personnel.sortie", "La date de sortie ne peut pas être antérieure à la date d'embauche.");
        }

        DateSortie = dateSortie;
        MotifSortie = Trim(motif);
        Bloque = true;
        IdentifiantApp = null;
        PassWord = null;
    }

    /// <summary>Reverses a departure, freeing the identifier again.</summary>
    public void Reintegrer(string? identifiantApp)
    {
        DateSortie = null;
        MotifSortie = null;
        Bloque = false;
        IdentifiantApp = Trim(identifiantApp);
    }

    public void SetAcces(bool bloque, bool admin, bool accesWeb)
    {
        Bloque = bloque;
        Admin = admin;
        AccesWeb = accesWeb;
    }

    public void SetIdentifiant(string? identifiantApp, string? passWord, string? passWordMessagerie, bool premiereConnexion)
    {
        IdentifiantApp = Trim(identifiantApp);
        PassWord = Trim(passWord);
        PassWordMessagerie = Trim(passWordMessagerie);
        PremiereConnexion = premiereConnexion;
    }

    public void SetAttachement(bool attachement) => Attachement = attachement;

    internal static decimal Apportion(decimal? coutMensuel, int nbTrajets)
    {
        if (coutMensuel is null or 0m || nbTrajets <= 0)
        {
            return 0m;
        }

        return Math.Round(coutMensuel.Value / nbTrajets, 4, MidpointRounding.AwayFromZero);
    }

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Rolling stock master (<c>t_materiel</c>).
/// </summary>
/// <remarks>
/// <para>
/// Tenant column is <c>societe</c>. Tractors, trailers and vehicles are <b>all rows of this table</b>,
/// discriminated by <see cref="TypeMateriel"/> — there is no <c>t_tracteur</c>, <c>t_remorque</c> or
/// <c>t_vehicule</c> table.
/// </para>
/// <para>
/// <b>Two overlapping retirement mechanisms exist</b> and both are written by active legacy code:
/// the <c>supprimer</c> flag, and disposal via <c>statut='S'</c> + <c>date_sortie</c> +
/// <c>nature_sortie</c>. Which one a given screen honours was not determinable from the source, so both
/// are modelled and <see cref="IsRetire"/> treats either as retired. Treating only one as authoritative
/// would resurrect disposed vehicles in some listings.
/// </para>
/// </remarks>
public sealed class Materiel : Entity, ITenantScoped
{
    private Materiel() { }

    public Materiel(string keyMateriel, string societeKey, string? immatriculation, string? typeMateriel)
    {
        Id = Require(keyMateriel, nameof(keyMateriel));
        SocieteKey = Require(societeKey, nameof(societeKey));
        Immatriculation = Trim(immatriculation);
        TypeMateriel = Trim(typeMateriel);
        Statut = StatutMateriel.Disponible;
    }

    public string KeyMateriel => Id;
    public string SocieteKey { get; private set; } = string.Empty;
    public string? Immatriculation { get; private set; }
    public string? Intitule { get; private set; }
    /// <summary>Discriminator: TRACTEUR / REMORQUE / … (free text, matches <c>t_type_materiel</c>).</summary>
    public string? TypeMateriel { get; private set; }
    /// <summary>Matches <c>t_nature_materiel</c>; stored with embedded quotes in the legacy data.</summary>
    public string? NatureMateriel { get; private set; }
    public StatutMateriel Statut { get; private set; }
    public string? FournisseurMateriel { get; private set; }
    public string? MoyenPropre { get; private set; }
    public DateTime? DateAcquisition { get; private set; }
    public DateTime? DateMiseCirculation { get; private set; }
    public DateTime? DateFinGarantie { get; private set; }

    // Disposal
    public DateTime? DateSortie { get; private set; }
    public string? NatureSortie { get; private set; }
    public decimal? MontantVente { get; private set; }
    public decimal? KmSortie { get; private set; }

    public string? Commentaire { get; private set; }
    public string? RefGps { get; private set; }
    public string? NumMastercard { get; private set; }
    public string? NumCarteAds { get; private set; }
    /// <summary>Denormalised copy of <c>t_carte_jawaz.num_carte_jawaz</c>; the two can drift.</summary>
    public string? NumCarteJawaz { get; private set; }
    public string? RfidMateriel { get; private set; }
    public string? RfidAbrMateriel { get; private set; }
    public string? KeyCarteMateriel { get; private set; }
    public string? KeyCarteJawaz { get; private set; }

    /// <summary>
    /// Average kilometres per day. The legacy label is "Moyenne Journaliére".
    /// </summary>
    /// <remarks>
    /// A <b>distance</b>, not a cost — it is used to project the next preventive service. The name is
    /// close enough to a daily rate to be mistaken for one.
    /// </remarks>
    public decimal? MoyenneKm { get; private set; }
    public decimal? CompteurKm { get; private set; }
    public DateTime? DateDernierCompteur { get; private set; }
    public DateTime? DateDernierTrajet { get; private set; }
    public string? DerniereDestination { get; private set; }
    public string? Chauffeur { get; private set; }
    public string? KeyChauffeur { get; private set; }
    public string? PaysGps { get; private set; }
    public string? VilleGps { get; private set; }
    public decimal? FuelLevelGps { get; private set; }
    public bool Attachement { get; private set; }

    /// <summary>Soft-delete flag. Coexists with disposal; see the type remarks.</summary>
    public bool Supprimer { get; private set; }

    /// <summary>
    /// <b>Monthly</b> cost of the vehicle, in <see cref="DeviseCout"/>.
    /// </summary>
    /// <remarks>
    /// Monthly, not daily. The costing engine apportions it across the month's trajets
    /// (<c>cout_tracteur / nb_trajet</c>), and only for types whose
    /// <c>t_type_materiel.generation_cout</c> is set.
    /// </remarks>
    public decimal? Cout { get; private set; }
    public string? DeviseCout { get; private set; }

    public bool IsRetire => Supprimer || Statut == StatutMateriel.Sorti || DateSortie.HasValue;

    /// <summary>Mirrors the legacy availability predicate <c>STATUT in ('D','R')</c>.</summary>
    public bool IsDisponible => !IsRetire && Statut is StatutMateriel.Disponible or StatutMateriel.EnReparation;

    public bool IsTracteur => string.Equals(TypeMateriel, "TRACTEUR", StringComparison.OrdinalIgnoreCase);

    public bool IsRemorque => string.Equals(TypeMateriel, "REMORQUE", StringComparison.OrdinalIgnoreCase);

    /// <summary>The vehicle's monthly cost apportioned across a number of trips.</summary>
    public decimal CoutParTrajet(int nbTrajets) => Personnel.Apportion(Cout, nbTrajets);

    public void SetIdentite(string? immatriculation, string? intitule, string? typeMateriel, string? natureMateriel, string? moyenPropre)
    {
        Immatriculation = Trim(immatriculation);
        Intitule = Trim(intitule);
        TypeMateriel = Trim(typeMateriel);
        // The legacy column stores labels wrapped in quotes; normalise on the way in.
        NatureMateriel = Trim(natureMateriel)?.Replace("'", string.Empty);
        MoyenPropre = Trim(moyenPropre);
    }

    public void SetDates(DateTime? acquisition, DateTime? miseCirculation, DateTime? finGarantie)
    {
        if (acquisition.HasValue && miseCirculation.HasValue && miseCirculation.Value.Date < acquisition.Value.Date)
        {
            throw new BusinessRuleException(
                "materiel.dates", "La date de mise en circulation ne peut pas précéder la date d'acquisition.");
        }

        DateAcquisition = acquisition;
        DateMiseCirculation = miseCirculation;
        DateFinGarantie = finGarantie;
    }

    public void SetEquipements(
        string? refGps, string? numMastercard, string? numCarteAds, string? rfid, string? rfidAbr,
        string? keyCarteMateriel, string? keyCarteJawaz, string? commentaire)
    {
        RefGps = Trim(refGps);
        NumMastercard = Trim(numMastercard);
        NumCarteAds = Trim(numCarteAds);
        RfidMateriel = Trim(rfid);
        RfidAbrMateriel = Trim(rfidAbr);
        KeyCarteMateriel = Trim(keyCarteMateriel);
        KeyCarteJawaz = Trim(keyCarteJawaz);
        Commentaire = Trim(commentaire);
    }

    public void SetCout(decimal? cout, string? deviseCout)
    {
        if (cout is < 0m)
        {
            throw new BusinessRuleException("materiel.cout", "Le coût mensuel ne peut pas être négatif.");
        }

        Cout = cout;
        DeviseCout = Trim(deviseCout);
    }

    public void SetStatut(StatutMateriel statut) => Statut = statut;

    /// <summary>Marks the vehicle as under repair. The legacy code sets <c>statut='R'</c> on entry.</summary>
    public void EntrerReparation() => Statut = StatutMateriel.EnReparation;

    /// <summary>Returns a repaired vehicle to service.</summary>
    public void SortirReparation()
    {
        if (Statut != StatutMateriel.EnReparation)
        {
            throw new BusinessRuleException("materiel.reparation", "Ce matériel n'est pas en réparation.");
        }

        Statut = StatutMateriel.Disponible;
    }

    /// <summary>
    /// Disposes of the vehicle: status, disposal date, reason and sale value together.
    /// </summary>
    /// <remarks>
    /// The legacy disposal writes all of these in one screen. Splitting them would leave a vehicle marked
    /// <c>Sorti</c> with no disposal date, which the "days out of service" reports read.
    /// </remarks>
    public void SortirDuParc(DateTime dateSortie, string? natureSortie, decimal? montantVente, decimal? kmSortie)
    {
        if (DateAcquisition.HasValue && dateSortie.Date < DateAcquisition.Value.Date)
            throw new BusinessRuleException("materiel.sortie_date", "La sortie ne peut pas précéder l’acquisition.");
        if (montantVente is < 0m || kmSortie is < 0m)
            throw new BusinessRuleException("materiel.sortie_valeur", "Le montant et le kilométrage doivent être positifs ou nuls.");
        DateSortie = dateSortie;
        NatureSortie = Trim(natureSortie);
        MontantVente = montantVente;
        KmSortie = kmSortie;
        Statut = StatutMateriel.Sorti;
    }

    /// <summary>Soft-deletes the row via the <c>supprimer</c> flag.</summary>
    public void SupprimerLogiquement() => Supprimer = true;

    public void SetCompteur(decimal? compteurKm, DateTime? dateCompteur, decimal? moyenneKm)
    {
        if (compteurKm is < 0m)
        {
            throw new BusinessRuleException("materiel.compteur", "Le compteur kilométrique ne peut pas être négatif.");
        }

        CompteurKm = compteurKm;
        DateDernierCompteur = dateCompteur;
        MoyenneKm = moyenneKm;
    }

    public void SetChauffeur(string? keyChauffeur, string? chauffeur)
    {
        KeyChauffeur = Trim(keyChauffeur);
        Chauffeur = Trim(chauffeur);
    }

    public void SetAttachement(bool attachement) => Attachement = attachement;

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Equipment type referential (<c>t_type_materiel</c>).
/// </summary>
/// <remarks>
/// <b>No tenant column</b>, and the primary key is the free-text label itself (TRACTEUR, REMORQUE, …)
/// rather than a code. <see cref="GenerationCout"/> gates participation in the voyage costing engine —
/// a type with the flag unset is never costed, whatever cost its vehicles carry.
/// </remarks>
public sealed class TypeMateriel : Entity
{
    private TypeMateriel() { }

    public TypeMateriel(string typeMateriel, bool generationCout = false)
    {
        Id = Require(typeMateriel, nameof(typeMateriel));
        GenerationCout = generationCout;
    }

    public string TypeMaterielCode => Id;
    /// <summary>True when vehicles of this type take part in voyage cost generation.</summary>
    public bool GenerationCout { get; private set; }
    /// <summary>Customs code; the legacy form intends exactly three digits.</summary>
    public string? CodeDouane { get; private set; }
    public string? CodeNgp { get; private set; }
    public bool Supprimer { get; private set; }

    public void SetCodes(string? codeDouane, string? codeNgp)
    {
        CodeDouane = Trim(codeDouane);
        CodeNgp = Trim(codeNgp);
    }

    /// <summary>Enables or disables costing for this type.</summary>
    public void SetGenerationCout(bool generationCout) => GenerationCout = generationCout;

    public void SupprimerLogiquement() => Supprimer = true;

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Equipment nature referential (<c>t_nature_materiel</c>).
/// </summary>
/// <remarks>
/// No tenant column; natural free-text key. <see cref="Coefficient"/> is the TEU coefficient the booking
/// list aggregates as <c>sum(t_equipement_booking.qte * t_nature_materiel.coefficient)</c>.
/// </remarks>
public sealed class NatureMateriel : Entity
{
    private NatureMateriel() { }

    public NatureMateriel(string natureMateriel, decimal? coefficient = null)
    {
        Id = Require(natureMateriel, nameof(natureMateriel));
        Coefficient = coefficient;
    }

    public string NatureMaterielCode => Id;
    public decimal? PoidsMax { get; private set; }
    public decimal? VolumeMax { get; private set; }
    /// <summary>TEU coefficient; read with an implicit 1 when null.</summary>
    public decimal? Coefficient { get; private set; }
    public bool Supprimer { get; private set; }

    /// <summary>The coefficient actually applied, defaulting to 1 as the legacy aggregate did.</summary>
    public decimal CoefficientEffectif => Coefficient ?? 1m;

    public void SetCapacites(decimal? poidsMax, decimal? volumeMax, decimal? coefficient)
    {
        PoidsMax = poidsMax;
        VolumeMax = volumeMax;
        Coefficient = coefficient;
    }

    public void SupprimerLogiquement() => Supprimer = true;

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();
}

/// <summary>
/// Per-kilometre fuel cost for a tractor over a validity window
/// (<c>t_cout_kilometrage_gasoil</c>).
/// </summary>
/// <remarks>
/// <para>
/// This is the one cost rate the schema <b>does</b> store per unit of distance — the counterpart to the
/// monthly <c>t_materiel.cout</c>. The legacy engine reads it as
/// <c>where key_tracteur=… and date_debut &lt;= date_depart and date_fin &gt;= date_depart</c> and
/// multiplies by the leg's kilometrage.
/// </para>
/// <para>
/// No tenant column: scope is reached through <c>key_tracteur</c>. A tractor normally has <b>two</b> rows
/// live in the same window, one per <see cref="ProduitGasoil"/>, so a lookup must always filter by
/// product.
/// </para>
/// </remarks>
public sealed class CoutKilometrageGasoil : Entity
{
    private CoutKilometrageGasoil() { }

    public CoutKilometrageGasoil(
        string keyLigne, string keyTracteur, DateTime dateDebut, DateTime dateFin, ProduitGasoil produitGasoil)
    {
        Id = Require(keyLigne, nameof(keyLigne));
        KeyTracteur = Require(keyTracteur, nameof(keyTracteur));
        DateDebut = dateDebut;
        DateFin = dateFin;
        ProduitGasoil = produitGasoil;
    }

    public string KeyLigne => Id;
    public string KeyTracteur { get; private set; } = string.Empty;
    public DateTime DateDebut { get; private set; }
    public DateTime DateFin { get; private set; }
    public int? TotalKm { get; private set; }
    public decimal? TotalLitre { get; private set; }
    public decimal? TotalMontant { get; private set; }
    /// <summary>The per-kilometre cost (<c>total_montant / total_km</c>).</summary>
    public decimal? CoutKm { get; private set; }
    /// <summary>Litres per 100 km (<c>total_litre / total_km * 100</c>).</summary>
    public decimal? RatioConsommation { get; private set; }
    public ProduitGasoil ProduitGasoil { get; private set; }

    /// <summary>Whether the window covers a date. Both bounds are inclusive, as in the legacy query.</summary>
    public bool Couvre(DateTime date) => date.Date >= DateDebut.Date && date.Date <= DateFin.Date;

    /// <summary>Recomputes the derived rates from the accumulated totals.</summary>
    public void SetTotaux(int? totalKm, decimal? totalLitre, decimal? totalMontant)
    {
        TotalKm = totalKm;
        TotalLitre = totalLitre;
        TotalMontant = totalMontant;

        // A zero distance cannot produce a rate; leaving the previous value would silently keep a stale
        // cost per kilometre.
        if (totalKm is null or 0)
        {
            CoutKm = null;
            RatioConsommation = null;
            return;
        }

        CoutKm = Math.Round((totalMontant ?? 0m) / totalKm.Value, 4, MidpointRounding.AwayFromZero);
        RatioConsommation = Math.Round((totalLitre ?? 0m) / totalKm.Value * 100m, 4, MidpointRounding.AwayFromZero);
    }

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();
}
