using Logistics.Application.Abstractions;
using Logistics.Application.Features.Shipments;
using Logistics.Domain.Common;
using Logistics.Domain.Entities.Resources;
using Logistics.Domain.Entities.Shipments;

namespace Logistics.Application.Features.Resources;

public sealed record PersonnelDto(
    string KeyPersonnel, string? Matricule, string NomComplet, string? KeyFonctionPersonnel,
    string? KeyServicePersonnel, string? AgencePersonnel, string? VillePersonnel, string? GsmPersonnel,
    string? EmailPersonnel, DateTime? DateEmbauche, DateTime? DateSortie, string? MotifSortie,
    bool Bloque, bool Admin, bool AccesWeb, string? IdentifiantApp,
    decimal? Cout, string? DeviseCout, bool IsActif, bool IsChauffeur, bool Attachement)
{
    public string? Nom { get; init; }
    public string? Prenom { get; init; }
    public string? KeyDepartement { get; init; }
}

public sealed record MaterielDto(
    string KeyMateriel, string? Immatriculation, string? Intitule, string? TypeMateriel, string? NatureMateriel,
    string Statut, bool IsRetire, bool IsDisponible, bool IsTracteur, bool IsRemorque,
    string? FournisseurMateriel, DateTime? DateAcquisition, DateTime? DateMiseCirculation,
    DateTime? DateSortie, string? NatureSortie, decimal? MontantVente, decimal? KmSortie,
    decimal? CompteurKm, decimal? MoyenneKm, string? KeyChauffeur, string? Chauffeur,
    decimal? Cout, string? DeviseCout, bool Supprimer, bool Attachement)
{
    public string? MoyenPropre { get; init; }
    public DateTime? DateFinGarantie { get; init; }
}

public sealed record CreatePersonnelRequest(
    string? Nom,
    string? Prenom = null,
    string? Matricule = null,
    string? KeyFonction = null,
    string? KeyService = null,
    string? Agence = null,
    string? Ville = null,
    string? Gsm = null,
    string? Email = null,
    DateTime? DateEmbauche = null);

public sealed record UpdatePersonnelRequest(
    string? Matricule,
    string? KeyFonction,
    string? KeyService,
    string? KeyDepartement,
    string? Nom,
    string? Prenom,
    string? Gsm,
    string? Email,
    DateTime? DateEmbauche,
    string? Ville = null,
    string? Agence = null);

public sealed record SortiePersonnelRequest(DateTime DateSortie, string? Motif);

public sealed record CreateMaterielRequest(
    string? Immatriculation,
    string? TypeMateriel,
    string? NatureMateriel = null,
    string? Intitule = null,
    string? FournisseurMateriel = null,
    string? MoyenPropre = null,
    DateTime? DateAcquisition = null,
    DateTime? DateMiseCirculation = null,
    DateTime? DateFinGarantie = null);

public sealed record UpdateMaterielRequest(
    string? Immatriculation,
    string? Intitule,
    string? TypeMateriel,
    string? NatureMateriel,
    string? MoyenPropre,
    DateTime? DateAcquisition,
    DateTime? DateMiseCirculation,
    DateTime? DateFinGarantie);

public sealed record SortieMaterielRequest(DateTime DateSortie, string? NatureSortie, decimal? MontantVente, decimal? KmSortie);

public sealed record CoutMaterielRequest(decimal? Cout, string? DeviseCout);

public sealed record PersonnelSearchRequest(
    string? Recherche = null,
    string? KeyFonction = null,
    string? Agence = null,
    bool IncludeSortis = false,
    int Page = 1,
    int PageSize = 25);

public sealed record MaterielSearchRequest(
    string? Recherche = null,
    string? TypeMateriel = null,
    StatutMateriel? Statut = null,
    bool IncludeRetires = false,
    int Page = 1,
    int PageSize = 25);

/// <summary>
/// A fully resolved set of cost inputs for one voyage, ready for the cost engine.
/// </summary>
/// <param name="NbTrajetsDiviseur">
/// The divisor applied to the monthly master costs. Exposed because the legacy routine reads a single
/// <c>nb_trajet</c> for the whole run without making clear whether it counts the voyage's trajets or the
/// fleet's trajets for the month — reporting it lets a caller audit or override the assumption.
/// </param>
/// <param name="Avertissements">
/// Rates that could not be resolved. Reported rather than defaulted to zero, because a silently missing
/// rate costs the voyage at nothing and looks like it worked.
/// </param>
public sealed record VoyageCostRatesResolus(
    string KeyVoyage,
    int NbTrajetsDiviseur,
    decimal? CoutSociete,
    decimal? CoutMead,
    decimal? CoutAgence,
    decimal? CoutAssuranceMarchandise,
    decimal CoutFraisBancaire,
    IReadOnlyList<VoyageCostLeg> Legs,
    IReadOnlyList<VoyageCostEquipment> Equipements,
    IReadOnlyList<VoyageCostPersonnel> Personnel,
    IReadOnlyList<string> Avertissements);

public interface IResourceRepository
{
    // Personnel
    Task<Personnel?> FindPersonnelAsync(string keyPersonnel, CancellationToken cancellationToken = default);
    Task<PagedResult<Personnel>> SearchPersonnelAsync(PersonnelSearchRequest request, CancellationToken cancellationToken = default);
    Task<Personnel?> FindChauffeurAsync(string keyChauffeur, CancellationToken cancellationToken = default);
    void Add(Personnel personnel);

    // Matériel
    Task<Materiel?> FindMaterielAsync(string keyMateriel, CancellationToken cancellationToken = default);
    Task<PagedResult<Materiel>> SearchMaterielAsync(MaterielSearchRequest request, CancellationToken cancellationToken = default);
    void Add(Materiel materiel);

    // Referentials
    Task<TypeMateriel?> FindTypeMaterielAsync(string typeMateriel, CancellationToken cancellationToken = default);
    Task<NatureMateriel?> FindNatureMaterielAsync(string natureMateriel, CancellationToken cancellationToken = default);

    /// <summary>
    /// The per-kilometre fuel cost in force for a tractor on a date.
    /// </summary>
    /// <remarks>
    /// Must be filtered by product: a tractor normally has two live rows in the same window (Gasoil and
    /// AdBlue), so an unfiltered lookup returns an arbitrary one.
    /// </remarks>
    Task<CoutKilometrageGasoil?> FindCoutKmAsync(
        string keyTracteur, DateTime date, ProduitGasoil produit, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IResourceService
{
    Task<PagedResult<PersonnelDto>> SearchPersonnelAsync(PersonnelSearchRequest request, CancellationToken cancellationToken = default);
    Task<PersonnelDto> GetPersonnelAsync(string keyPersonnel, CancellationToken cancellationToken = default);
    Task<PersonnelDto> CreatePersonnelAsync(CreatePersonnelRequest request, CancellationToken cancellationToken = default);
    Task<PersonnelDto> UpdatePersonnelAsync(string keyPersonnel, UpdatePersonnelRequest request, CancellationToken cancellationToken = default);
    Task<PersonnelDto> EnregistrerSortieAsync(string keyPersonnel, SortiePersonnelRequest request, CancellationToken cancellationToken = default);
    Task<PersonnelDto> SetCoutPersonnelAsync(string keyPersonnel, CoutMaterielRequest request, CancellationToken cancellationToken = default);

    Task<PagedResult<MaterielDto>> SearchMaterielAsync(MaterielSearchRequest request, CancellationToken cancellationToken = default);
    Task<MaterielDto> GetMaterielAsync(string keyMateriel, CancellationToken cancellationToken = default);
    Task<MaterielDto> CreateMaterielAsync(CreateMaterielRequest request, CancellationToken cancellationToken = default);
    Task<MaterielDto> UpdateMaterielAsync(string keyMateriel, UpdateMaterielRequest request, CancellationToken cancellationToken = default);
    Task<MaterielDto> SortirDuParcAsync(string keyMateriel, SortieMaterielRequest request, CancellationToken cancellationToken = default);
    Task<MaterielDto> EntrerReparationAsync(string keyMateriel, CancellationToken cancellationToken = default);
    Task<MaterielDto> SortirReparationAsync(string keyMateriel, CancellationToken cancellationToken = default);
    Task<MaterielDto> SetCoutMaterielAsync(string keyMateriel, CoutMaterielRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads the voyage cost rates the legacy engine assembled in SQL.
/// </summary>
/// <remarks>
/// <para>
/// This is the component that removes the hand-supplied rates from the cost endpoint. It encodes the
/// correction established during extraction: the masters hold a <b>monthly</b> cost
/// (<c>t_materiel.cout</c>, <c>t_personnel.cout</c>), which the legacy engine divides by the month's
/// trajet count. The only genuinely per-unit rate is the fuel cost per kilometre
/// (<c>t_cout_kilometrage_gasoil.cout_km</c>).
/// </para>
/// <para>
/// It also applies the <c>t_type_materiel.generation_cout</c> gate: a type with the flag unset takes no
/// part in costing, whatever cost its vehicles carry.
/// </para>
/// </remarks>
public interface IVoyageCostRateProvider
{
    /// <summary>
    /// Resolves the cost inputs for a voyage.
    /// </summary>
    /// <param name="nbTrajetsDiviseur">
    /// Overrides the divisor applied to monthly costs. When omitted, the voyage's own trajet count is
    /// used; the value actually applied is reported in the result.
    /// </param>
    Task<VoyageCostRatesResolus> ResolveAsync(
        string keyVoyage, int? nbTrajetsDiviseur = null, CancellationToken cancellationToken = default);
}

public sealed class VoyageCostRateProvider(
    IResourceRepository resources,
    IVoyageRepository voyages,
    IReferenceDataRepository referenceData,
    ITenantContext tenant) : IVoyageCostRateProvider
{
    public async Task<VoyageCostRatesResolus> ResolveAsync(
        string keyVoyage, int? nbTrajetsDiviseur = null, CancellationToken cancellationToken = default)
    {
        var voyage = await voyages.GetAsync(keyVoyage, true, cancellationToken)
            ?? throw new EntityNotFoundException("Voyage", keyVoyage);

        var avertissements = new List<string>();

        // The legacy divisor. A voyage with no trajet cannot apportion anything, so the guard keeps the
        // division defined rather than producing an infinite per-trajet cost.
        var nbTrajets = nbTrajetsDiviseur ?? voyage.Trajets.Count;
        if (nbTrajets <= 0)
        {
            nbTrajets = 1;
            avertissements.Add(
                "Aucun trajet sur ce voyage : le coût mensuel n'a pas pu être réparti (diviseur forcé à 1).");
        }

        var societe = await referenceData.FindAsync<Domain.Entities.ReferenceData.Societe>(tenant.SocieteKey);

        var legs = new List<VoyageCostLeg>();
        var equipements = new List<VoyageCostEquipment>();
        var personnel = new List<VoyageCostPersonnel>();

        foreach (var trajet in voyage.Trajets)
        {
            var coutTracteur = await CoutParTrajetAsync(trajet.KeyTracteur, "TRACTEUR", nbTrajets, avertissements, cancellationToken);
            var coutRemorque = await CoutParTrajetAsync(trajet.KeyRemorque, "REMORQUE", nbTrajets, avertissements, cancellationToken);
            var coutChauffeur = await CoutChauffeurParTrajetAsync(trajet.KeyChauffeur1, nbTrajets, avertissements, cancellationToken);

            // Fuel rates are keyed on the departure date, matching the legacy window lookup.
            var dateRef = voyage.DateVoyage ?? voyage.DateDepart ?? DateTime.UtcNow;
            var coutKmGasoil = await CoutKmAsync(trajet.KeyTracteur, dateRef, ProduitGasoil.Gasoil, avertissements, cancellationToken);
            var coutKmAdBlue = await CoutKmAsync(trajet.KeyTracteur, dateRef, ProduitGasoil.AdBlue, avertissements, cancellationToken);

            legs.Add(new VoyageCostLeg(
                trajet.KeyTrajet,
                trajet.KeyTracteur,
                trajet.KeyRemorque,
                trajet.KeyChauffeur1,
                trajet.KeyChauffeur2,
                trajet.Kilometrage ?? 0m,
                CoutKmTracteur: 0m,
                CoutTracteurParTrajet: coutTracteur,
                CoutRemorqueParTrajet: coutRemorque,
                CoutChauffeurParTrajet: coutChauffeur,
                CoutKmGasoil: coutKmGasoil,
                CoutKmAdBlue: coutKmAdBlue));
        }

        // Equipment on the voyage, gated by generation_cout.
        foreach (var equipement in voyage.Equipements)
        {
            // t_type_materiel is keyed by a free-text label ("TRACTEUR"/"REMORQUE"), while the voyage
            // side holds an enum, so the name is what bridges the two.
            var libelle = equipement.TypeEquipement?.ToString() ?? string.Empty;

            var type = await resources.FindTypeMaterielAsync(libelle, cancellationToken);

            if (type is null)
            {
                avertissements.Add($"Type de matériel « {libelle} » inconnu : ligne non costée.");
                continue;
            }

            if (!type.GenerationCout)
            {
                avertissements.Add(
                    $"Type « {type.TypeMaterielCode} » exclu du calcul de coût (generation_cout = 0).");
                continue;
            }

            // The equipment line carries no cost of its own; the monthly rate lives on the vehicle.
            equipements.Add(new VoyageCostEquipment(
                equipement.KeyLigneEquipement, libelle, (int)equipement.Qte, 0m));
        }

        // Second driver, if any, is charged like the first.
        foreach (var trajet in voyage.Trajets.Where(t => !string.IsNullOrWhiteSpace(t.KeyChauffeur2)))
        {
            var cout = await CoutChauffeurParTrajetAsync(trajet.KeyChauffeur2, nbTrajets, avertissements, cancellationToken);
            if (cout > 0m)
            {
                personnel.Add(new VoyageCostPersonnel(trajet.KeyChauffeur2!, cout));
            }
        }

        // The agency cost comes from the exploitation agency, as in the voyage cost service.
        Domain.Entities.ReferenceData.Agence? agence = null;
        if (!string.IsNullOrWhiteSpace(voyage.KeyAgenceExploitation))
        {
            agence = await referenceData.FindAsync<Domain.Entities.ReferenceData.Agence>(voyage.KeyAgenceExploitation!);
        }

        return new VoyageCostRatesResolus(
            KeyVoyage: keyVoyage,
            NbTrajetsDiviseur: nbTrajets,
            CoutSociete: societe?.CoutSociete,
            CoutMead: societe?.CoutMead,
            CoutAgence: agence?.CoutAgence,
            CoutAssuranceMarchandise: societe?.CoutAssuranceMarchandise,
            CoutFraisBancaire: 0m,
            Legs: legs,
            Equipements: equipements,
            Personnel: personnel,
            Avertissements: avertissements.Distinct(StringComparer.Ordinal).ToArray());
    }

    /// <summary>
    /// Monthly cost of a vehicle apportioned across the period's trajets.
    /// </summary>
    /// <remarks>
    /// Only types flagged <c>generation_cout</c> are costed — the legacy <c>frm_Gestion_Couts</c> filters
    /// on it, so an unflagged type contributes nothing even when its vehicles carry a monthly cost.
    /// </remarks>
    private async Task<decimal> CoutParTrajetAsync(
        string? keyMateriel, string typeAttendu, int nbTrajets, List<string> avertissements,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(keyMateriel))
        {
            return 0m;
        }

        var materiel = await resources.FindMaterielAsync(keyMateriel, cancellationToken);
        if (materiel is null)
        {
            avertissements.Add($"{typeAttendu} « {keyMateriel} » introuvable : coût non calculé.");
            return 0m;
        }

        var type = await resources.FindTypeMaterielAsync(materiel.TypeMateriel ?? string.Empty, cancellationToken);
        if (type is { GenerationCout: false })
        {
            avertissements.Add($"Type « {type.TypeMaterielCode} » exclu du calcul de coût (generation_cout = 0).");
            return 0m;
        }

        if (materiel.Cout is null or 0m)
        {
            avertissements.Add($"{typeAttendu} « {materiel.Immatriculation ?? keyMateriel} » sans coût mensuel.");
            return 0m;
        }

        return materiel.CoutParTrajet(nbTrajets);
    }

    private async Task<decimal> CoutChauffeurParTrajetAsync(
        string? keyChauffeur, int nbTrajets, List<string> avertissements, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(keyChauffeur))
        {
            return 0m;
        }

        var chauffeur = await resources.FindChauffeurAsync(keyChauffeur, cancellationToken);
        if (chauffeur is null)
        {
            avertissements.Add($"Chauffeur « {keyChauffeur} » introuvable : coût non calculé.");
            return 0m;
        }

        if (!chauffeur.IsChauffeur)
        {
            avertissements.Add(
                $"Personnel « {keyChauffeur} » n'a pas la fonction {Personnel.FonctionChauffeur} : coût non calculé.");
            return 0m;
        }

        if (chauffeur.Cout is null or 0m)
        {
            avertissements.Add($"Chauffeur « {chauffeur.NomComplet} » sans coût mensuel.");
            return 0m;
        }

        return chauffeur.CoutParTrajet(nbTrajets);
    }

    private async Task<decimal> CoutKmAsync(
        string? keyTracteur, DateTime date, ProduitGasoil produit, List<string> avertissements,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(keyTracteur))
        {
            return 0m;
        }

        var ligne = await resources.FindCoutKmAsync(keyTracteur, date, produit, cancellationToken);
        if (ligne?.CoutKm is null or 0m)
        {
            // Reported rather than defaulted: a missing fuel rate understates the voyage silently.
            avertissements.Add(
                $"Coût au km {produit} introuvable pour le tracteur « {keyTracteur} » à la date {date:dd/MM/yyyy}.");
            return 0m;
        }

        return ligne.CoutKm.Value;
    }
}

/// <summary>
/// Personnel and matériel use cases — the port of <c>ClassPersonnel.vb</c>,
/// <c>frm_Codification_Matériaux.vb</c>, <c>frm_Sortie_du_materiel.vb</c> and the repair screens.
/// </summary>
/// <remarks>
/// Changes are recorded through <c>JournalReferentiel</c> (<c>t_hist_referentiel</c>) rather than the
/// dedicated <c>t_trace_personnel</c> / <c>t_trace_materiel</c> tables: those two are not mapped yet, and
/// personnel and matériel are master data, which is exactly what the referential journal is for. The
/// per-entity trace tables are listed as a remaining gap.
/// </remarks>
public sealed class ResourceService(
    IResourceRepository resources,
    IKeyGenerator keys,
    IAuditTrail audit,
    ITenantContext tenant) : IResourceService
{
    public async Task<PagedResult<PersonnelDto>> SearchPersonnelAsync(PersonnelSearchRequest request, CancellationToken cancellationToken = default)
    {
        Guard(request.Page);

        var page = await resources.SearchPersonnelAsync(
            request with { PageSize = Math.Clamp(request.PageSize, 1, 200) }, cancellationToken);

        return new PagedResult<PersonnelDto>(
            page.Items.Select(MapPersonnel).ToArray(), page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<PersonnelDto> GetPersonnelAsync(string keyPersonnel, CancellationToken cancellationToken = default) =>
        MapPersonnel(await RequirePersonnelAsync(keyPersonnel, cancellationToken));

    public async Task<PersonnelDto> CreatePersonnelAsync(CreatePersonnelRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Nom))
        {
            throw new BusinessRuleException("personnel.nom", "Le nom est obligatoire.");
        }

        // The legacy key generator name for both personnel and matériel is "parametrage".
        var key = await keys.NextAsync("parametrage", cancellationToken);
        var personnel = new Personnel(key, tenant.SocieteKey, request.Nom, request.Prenom);

        personnel.SetIdentite(request.Matricule, request.KeyFonction, request.KeyService, null,
            request.Nom, request.Prenom, null, null);
        personnel.SetCoordonnees(null, null, null, request.Ville, null, request.Gsm, null, request.Email, request.Agence);
        personnel.SetEmbauche(request.DateEmbauche);

        // The legacy INSERT hard-codes these three to zero and the password flow to "first connection".
        personnel.SetAcces(bloque: false, admin: false, accesWeb: false);
        personnel.SetIdentifiant(null, null, null, premiereConnexion: true);

        resources.Add(personnel);
        await resources.SaveChangesAsync(cancellationToken);
        await audit.JournalAsync("Personnel", "Création", $"{key} — {personnel.NomComplet}", cancellationToken);

        return MapPersonnel(personnel);
    }

    public async Task<PersonnelDto> UpdatePersonnelAsync(string keyPersonnel, UpdatePersonnelRequest request, CancellationToken cancellationToken = default)
    {
        var personnel = await RequirePersonnelAsync(keyPersonnel, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Nom))
            throw new BusinessRuleException("personnel.nom", "Le nom est obligatoire.");

        personnel.SetIdentite(request.Matricule, request.KeyFonction, request.KeyService, request.KeyDepartement,
            request.Nom, request.Prenom, personnel.DateNaissance, personnel.SituationFamiliale);
        personnel.SetCoordonnees(personnel.Adresse1Personnel, personnel.Adresse2Personnel,
            personnel.CpPersonnel, request.Ville ?? personnel.VillePersonnel, personnel.PaysPersonnel,
            request.Gsm, personnel.FixePersonnel, request.Email, request.Agence ?? personnel.AgencePersonnel);
        personnel.SetEmbauche(request.DateEmbauche ?? personnel.DateEmbauche);

        await resources.SaveChangesAsync(cancellationToken);
        await audit.JournalAsync("Personnel", "Modification", keyPersonnel, cancellationToken);

        return MapPersonnel(personnel);
    }

    /// <summary>
    /// Records a departure: the date and reason, plus the login revocation.
    /// </summary>
    /// <remarks>
    /// This table has no delete flag, so a departure <b>is</b> the removal. The login is revoked in the
    /// same operation because nothing else in the schema would disable the account.
    /// </remarks>
    public async Task<PersonnelDto> EnregistrerSortieAsync(string keyPersonnel, SortiePersonnelRequest request, CancellationToken cancellationToken = default)
    {
        var personnel = await RequirePersonnelAsync(keyPersonnel, cancellationToken);
        if (!personnel.IsActif)
            throw new BusinessRuleException("personnel.deja_sorti", "La sortie de cet employé est déjà enregistrée.");
        personnel.EnregistrerSortie(request.DateSortie, request.Motif);

        await resources.SaveChangesAsync(cancellationToken);
        await audit.JournalAsync("Personnel", "Sortie", $"{keyPersonnel} — {request.Motif}", cancellationToken);

        return MapPersonnel(personnel);
    }

    public async Task<PersonnelDto> SetCoutPersonnelAsync(string keyPersonnel, CoutMaterielRequest request, CancellationToken cancellationToken = default)
    {
        var personnel = await RequirePersonnelAsync(keyPersonnel, cancellationToken);
        personnel.SetCout(request.Cout, request.DeviseCout);

        await resources.SaveChangesAsync(cancellationToken);
        await audit.JournalAsync("Personnel", "Coût", $"{keyPersonnel} — {request.Cout} {request.DeviseCout}", cancellationToken);

        return MapPersonnel(personnel);
    }

    public async Task<PagedResult<MaterielDto>> SearchMaterielAsync(MaterielSearchRequest request, CancellationToken cancellationToken = default)
    {
        Guard(request.Page);

        var page = await resources.SearchMaterielAsync(
            request with { PageSize = Math.Clamp(request.PageSize, 1, 200) }, cancellationToken);

        return new PagedResult<MaterielDto>(
            page.Items.Select(MapMateriel).ToArray(), page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<MaterielDto> GetMaterielAsync(string keyMateriel, CancellationToken cancellationToken = default) =>
        MapMateriel(await RequireMaterielAsync(keyMateriel, cancellationToken));

    public async Task<MaterielDto> CreateMaterielAsync(CreateMaterielRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.TypeMateriel))
        {
            throw new BusinessRuleException("materiel.type", "Le type de matériel est obligatoire.");
        }

        var key = await keys.NextAsync("parametrage", cancellationToken);
        var materiel = new Materiel(key, tenant.SocieteKey, request.Immatriculation, request.TypeMateriel);

        materiel.SetIdentite(request.Immatriculation, request.Intitule, request.TypeMateriel, request.NatureMateriel, request.MoyenPropre);
        materiel.SetDates(request.DateAcquisition, request.DateMiseCirculation, request.DateFinGarantie);

        resources.Add(materiel);
        await resources.SaveChangesAsync(cancellationToken);
        await audit.JournalAsync("Materiel", "Création", $"{key} — {request.Immatriculation}", cancellationToken);

        return MapMateriel(materiel);
    }

    public async Task<MaterielDto> UpdateMaterielAsync(string keyMateriel, UpdateMaterielRequest request, CancellationToken cancellationToken = default)
    {
        var materiel = await RequireMaterielAsync(keyMateriel, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.TypeMateriel))
            throw new BusinessRuleException("materiel.type", "Le type de matériel est obligatoire.");

        materiel.SetIdentite(request.Immatriculation, request.Intitule, request.TypeMateriel, request.NatureMateriel, request.MoyenPropre);
        materiel.SetDates(request.DateAcquisition, request.DateMiseCirculation, request.DateFinGarantie);

        await resources.SaveChangesAsync(cancellationToken);
        await audit.JournalAsync("Materiel", "Modification", keyMateriel, cancellationToken);

        return MapMateriel(materiel);
    }

    /// <summary>
    /// Disposes of a vehicle: status <c>Sorti</c>, disposal date, reason and sale value together.
    /// </summary>
    /// <remarks>
    /// A disposed vehicle is <b>not</b> deleted — it remains visible as history, which is why this sets a
    /// business status rather than the <c>supprimer</c> flag.
    /// </remarks>
    public async Task<MaterielDto> SortirDuParcAsync(string keyMateriel, SortieMaterielRequest request, CancellationToken cancellationToken = default)
    {
        var materiel = await RequireMaterielAsync(keyMateriel, cancellationToken);

        if (materiel.IsRetire)
        {
            throw new BusinessRuleException("materiel.deja_sorti", "Ce matériel est déjà sorti du parc.");
        }

        materiel.SortirDuParc(request.DateSortie, request.NatureSortie, request.MontantVente, request.KmSortie);

        await resources.SaveChangesAsync(cancellationToken);
        await audit.JournalAsync("Materiel", "Sortie du parc", $"{keyMateriel} — {request.NatureSortie}", cancellationToken);

        return MapMateriel(materiel);
    }

    public async Task<MaterielDto> EntrerReparationAsync(string keyMateriel, CancellationToken cancellationToken = default)
    {
        var materiel = await RequireMaterielAsync(keyMateriel, cancellationToken);

        if (materiel.IsRetire)
        {
            throw new BusinessRuleException("materiel.retire", "Un matériel sorti du parc ne peut pas être mis en réparation.");
        }

        materiel.EntrerReparation();
        await resources.SaveChangesAsync(cancellationToken);
        await audit.JournalAsync("Materiel", "Entrée réparation", keyMateriel, cancellationToken);

        return MapMateriel(materiel);
    }

    public async Task<MaterielDto> SortirReparationAsync(string keyMateriel, CancellationToken cancellationToken = default)
    {
        var materiel = await RequireMaterielAsync(keyMateriel, cancellationToken);
        materiel.SortirReparation();

        await resources.SaveChangesAsync(cancellationToken);
        await audit.JournalAsync("Materiel", "Sortie réparation", keyMateriel, cancellationToken);

        return MapMateriel(materiel);
    }

    public async Task<MaterielDto> SetCoutMaterielAsync(string keyMateriel, CoutMaterielRequest request, CancellationToken cancellationToken = default)
    {
        var materiel = await RequireMaterielAsync(keyMateriel, cancellationToken);
        materiel.SetCout(request.Cout, request.DeviseCout);

        await resources.SaveChangesAsync(cancellationToken);
        await audit.JournalAsync("Materiel", "Coût", $"{keyMateriel} — {request.Cout} {request.DeviseCout}", cancellationToken);

        return MapMateriel(materiel);
    }

    private static void Guard(int page)
    {
        if (page < 1)
        {
            throw new ArgumentException("La page doit être supérieure ou égale à 1.", nameof(page));
        }
    }

    private async Task<Personnel> RequirePersonnelAsync(string key, CancellationToken cancellationToken) =>
        await resources.FindPersonnelAsync(key, cancellationToken)
        ?? throw new EntityNotFoundException("Personnel", key);

    private async Task<Materiel> RequireMaterielAsync(string key, CancellationToken cancellationToken) =>
        await resources.FindMaterielAsync(key, cancellationToken)
        ?? throw new EntityNotFoundException("Materiel", key);

    private static PersonnelDto MapPersonnel(Personnel p) => new(
        p.KeyPersonnel, p.Matricule, p.NomComplet, p.KeyFonctionPersonnel, p.KeyServicePersonnel,
        p.AgencePersonnel, p.VillePersonnel, p.GsmPersonnel, p.EmailPersonnel, p.DateEmbauche,
        p.DateSortie, p.MotifSortie, p.Bloque, p.Admin, p.AccesWeb, p.IdentifiantApp,
        p.Cout, p.DeviseCout, p.IsActif, p.IsChauffeur, p.Attachement)
        { Nom = p.NomPersonnel, Prenom = p.PrenomPersonnel, KeyDepartement = p.KeyDepartementPersonnel };

    private static MaterielDto MapMateriel(Materiel m) => new(
        m.KeyMateriel, m.Immatriculation, m.Intitule, m.TypeMateriel, m.NatureMateriel,
        m.Statut.ToString(), m.IsRetire, m.IsDisponible, m.IsTracteur, m.IsRemorque,
        m.FournisseurMateriel, m.DateAcquisition, m.DateMiseCirculation,
        m.DateSortie, m.NatureSortie, m.MontantVente, m.KmSortie,
        m.CompteurKm, m.MoyenneKm, m.KeyChauffeur, m.Chauffeur,
        m.Cout, m.DeviseCout, m.Supprimer, m.Attachement)
        { MoyenPropre = m.MoyenPropre, DateFinGarantie = m.DateFinGarantie };
}
