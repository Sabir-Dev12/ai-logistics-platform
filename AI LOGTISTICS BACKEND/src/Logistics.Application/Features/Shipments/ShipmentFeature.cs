using Logistics.Application.Abstractions;
using Logistics.Application.Features.Authentication;
using Logistics.Domain.Common;
using Logistics.Domain.Entities.Resources;
using Logistics.Domain.Entities.Audit;
using Logistics.Domain.Entities.Invoicing;
using Logistics.Domain.Entities.Shipments;

namespace Logistics.Application.Features.Shipments;

public enum VoyageSearchStatus { Brouillon, Valide, Cloture }

// ---------------------------------------------------------------------------------------------
// DTOs
// ---------------------------------------------------------------------------------------------
public sealed record VoyageListItem(
    string KeyVoyage, string? NumVoyage, string? TypeVoyage, CategorieVoyage Categorie,
    NatureVoyage? Nature, string? PortDepart, string? PortArrivee, string? NavireDepart,
    DateTime? DateDepartPrevu, DateTime? DateArriveePrevu, DateTime? DateVoyage, DateTime? DateCloture,
    string KeyStatutVoyage, string? KeyAgenceExploitation, string? IntituleAgenceExploitation,
    bool IsMead, bool Supprime, string? Provenance = null, string? Destination = null);

public sealed record TrajetDto(string KeyTrajet, int? Ordre, string? Provenance, string? Destination,
    DateTime? DateDepart, DateTime? DateArrivee, string? NumTracteur, string? NumRemorque,
    string? IntituleChauffeur1, decimal? Kilometrage, bool FraisAutoroute, string? NumPlomb);

public sealed record EquipementDto(string KeyLigneEquipement, string TypeEquipement, string? NatureEquipement,
    int Qte, IReadOnlyList<DetailEquipementDto> Details);

/// <summary>
/// Concrete equipment unit. <see cref="KeyEquipement"/> is nullable because the LCL variant carries a
/// number with no <c>t_materiel</c> reference.
/// </summary>
public sealed record DetailEquipementDto(string? KeyEquipement, string? NumEquipement, string? NumPlomb,
    decimal? Tare, DateTime? DateRestitution, decimal? NbColis, decimal? PoidsBrut, decimal? Volume);

public sealed record VoyageDetail(
    string KeyVoyage, string SocieteKey, string? NumVoyage, string? TypeVoyage, NatureVoyage? Nature,
    CategorieVoyage Categorie, string? ServiceVoyage, string? KeyStatutVoyage, DateTime? DateStatutVoyage,
    DateTime? DateCreation, DateTime? DateVoyage, DateTime? DateCloture, bool IsValide, bool IsCloture,
    string? KeyAgenceSaisie, string? KeyAgenceExploitation, string? IntituleAgenceExploitation,
    DateTime? DateDepartPrevu, DateTime? DateArriveePrevu, DateTime? DateDepart, DateTime? DateArrivee,
    string? PortDepart, string? PortArrivee, string? NavireDepart, string? NavireArrivee,
    string? Provenance, string? Destination, string? KeyExploitant, string? KeyChauffeur1, string? KeyChauffeur2,
    string? NumAcquit, string? NumManifeste, string? NumDs, string? NumCnt, string? NumBooking,
    string? NumRemorque, string? NumTracteur, string? NumTc, string? NumCaisse,
    string? KeyStatutVoyageMead, string? LibelleStatutVoyageMead, string? MagasinMead, bool IsMead,
    decimal? NbDossVoyageSaisi, decimal? TotalPoidsBrutSaisi, decimal? TotalNbColisSaisi, decimal CostDivisor,
    StatutReception? StatutReception, string? Commentaire, bool Attachement, bool Supprime,
    IReadOnlyList<TrajetDto> Trajets, IReadOnlyList<EquipementDto> Equipements, SaveVoyageRequest? EditableFields = null);

public sealed record SaveVoyageRequest
{
    public string? NumVoyage { get; init; }
    public string? TypeVoyage { get; init; }
    public NatureVoyage? NatureVoyage { get; init; }
    public CategorieVoyage CategorieVoyage { get; init; } = CategorieVoyage.International;
    public string? ServiceVoyage { get; init; }
    public string? Provenance { get; init; }
    public string? Destination { get; init; }
    public string? Commentaire { get; init; }
    public string? KeyAgenceSaisie { get; init; }
    public string? IntituleAgenceSaisie { get; init; }
    public string? KeyAgenceExploitation { get; init; }
    public string? IntituleAgenceExploitation { get; init; }
    public DateTime? DateDepartPrevu { get; init; }
    public DateTime? DateArriveePrevu { get; init; }
    public DateTime? DateDepart { get; init; }
    public DateTime? DateArrivee { get; init; }
    public string? PortDepart { get; init; }
    public string? PortArrivee { get; init; }
    public string? NavireDepart { get; init; }
    public string? NavireArrivee { get; init; }
    public string? KeyExploitant { get; init; }
    public string? KeyChauffeur1 { get; init; }
    public string? KeyChauffeur2 { get; init; }
    public string? NumAcquit { get; init; }
    public string? NumManifeste { get; init; }
    public string? NumDs { get; init; }
    public string? NumCnt { get; init; }
    public string? NumBooking { get; init; }
    public string? NumRemorque { get; init; }
    public string? NumTracteur { get; init; }
    public string? NumTc { get; init; }
    public string? NumCaisse { get; init; }
    public string? MagasinMead { get; init; }
}

public sealed record SaveTrajetRequest(int? Ordre, string? Provenance, string? Destination,
    DateTime? DateDepart, DateTime? DateArrivee, string? KeyTracteur, string? NumTracteur,
    string? KeyRemorque, string? NumRemorque, string? KeyChauffeur1, string? KeyChauffeur2,
    decimal? Kilometrage, bool FraisAutoroute, string? NumPlomb);

public sealed record DossierListItem(
    string KeyDossier, string? NumDossier, string? TypeDossier, string? KeyClient, string? IntituleClient,
    string? KeyVoyageInter, DateTime? DateCreation, decimal? PoidsBrut, decimal? PoidsTaxable,
    decimal? NbColis, string? KeyStatutDossier, string? LibelleStatutDossier, string? MagasinDouane,
    StatutFacturation StatutFacturation, StatutClotureDossier StatutCloture, bool Supprime);

public sealed record ColisageDto(int KeyLigneColisage, string TypeColis, decimal NbColis,
    string? NatureMarchandise, decimal? PoidsBrut, decimal? Volume, string? Marque, string? CodeNgp);

public sealed record DossierDetail(
    string KeyDossier, string SocieteKey, string? NumDossier, string? TypeDossier, DateTime? DateCreation,
    string? KeyVoyageInter, string? KeyBooking, string? KeyCotation, string? KeyClient, string? IntituleClient,
    string? KeyPayeur, string? KeyExpediteur, string? IntituleExpediteur, string? KeyDestinataire,
    string? IntituleDestinataire, string? Incoterm, string? DescriptionMarchandise,
    bool MarchandiseGerbable, bool MarchandiseDangereuse, string? ClasseMarchandiseDangereuse,
    bool MarchandiseFragile, bool Assurance, decimal? NbColis, string? TypeColis, decimal? PoidsBrut,
    decimal? Volume, decimal? PoidsTaxable, decimal? VolumeTaxable, int? Position,
    string? MagasinDouane, string? KeyStatutDossier, string? LibelleStatutDossier,
    string? KeyStatutMead, string? LibelleStatutDossierMead, DateTime? DateDepotage, LieuDepotage? LieuDepotage,
    decimal? ColisDepote, decimal? ColisSortie, bool HasStockRemaining, bool IsMead,
    StatutFacturation StatutFacturation, StatutClotureDossier StatutCloture, bool Litige, bool Cod,
    string? HsCode, string? CodeRegime, decimal? MontantFret, string? DeviseFret, bool Attachement,
    bool Supprime, IReadOnlyList<ColisageDto> Colisages,
    SaveDossierRequest? EditableFields = null, string? ConstatDepotage = null, string? RefDepotage = null);

public sealed record SaveDossierRequest
{
    public required string NumDossier { get; init; }
    public required string TypeDossier { get; init; }
    public string? KeyClient { get; init; }
    public string? IntituleClient { get; init; }
    public string? NatureOperation { get; init; }
    public string? ServiceDossier { get; init; }
    public int? Position { get; init; }
    public string? CommentaireDossier { get; init; }
    public string? KeyAgenceExploitation { get; init; }
    public string? IntituleAgenceExploitation { get; init; }
    public string? KeyExpediteur { get; init; }
    public string? IntituleExpediteur { get; init; }
    public string? KeyDestinataire { get; init; }
    public string? IntituleDestinataire { get; init; }
    public string? KeyPayeur { get; init; }
    public string? IntitulePayeur { get; init; }
    public string? KeyAgent { get; init; }
    public string? IntituleAgent { get; init; }
    public string? Incoterm { get; init; }
    public string? DescriptionMarchandise { get; init; }
    public bool MarchandiseGerbable { get; init; }
    public bool MarchandiseDangereuse { get; init; }
    public string? ClasseMarchandiseDangereuse { get; init; }
    public bool MarchandiseFragile { get; init; }
    public bool Assurance { get; init; }
    public decimal? NbColis { get; init; }
    public string? TypeColis { get; init; }
    public string? Marque { get; init; }
    public decimal? PoidsBrut { get; init; }
    public decimal? Volume { get; init; }
    public decimal? PoidsTaxable { get; init; }
    public decimal? VolumeTaxable { get; init; }
    public string? MagasinDouane { get; init; }
    public string? HsCode { get; init; }
    public string? CodeRegime { get; init; }
    public string? CodeBureau { get; init; }
    public decimal? MontantFret { get; init; }
    public string? DeviseFret { get; init; }
    public bool TemperatureDirigee { get; init; }
    public decimal? TemperatureMin { get; init; }
    public decimal? TemperatureMax { get; init; }
}

public sealed record SaveColisageRequest(string TypeColis, decimal NbColis, string? NatureMarchandise,
    string? Marque, string? CodeNgp, decimal? PoidsBrut, decimal? Volume);

public sealed record DepotageRequest(DateTime DateDepotage, LieuDepotage LieuDepotage, string? ConstatDepotage,
    decimal? ColisDepote, string? RefDepotage);

public sealed record CostOperationState(string Operation, bool IsCalculated);

// ---------------------------------------------------------------------------------------------
// Repository abstractions
// ---------------------------------------------------------------------------------------------
public interface IVoyageRepository
{
    Task<Voyage?> GetAsync(string keyVoyage, bool includeChildren, CancellationToken cancellationToken = default);
    Task<PagedResult<Voyage>> SearchAsync(string? search, CategorieVoyage? categorie, string? keyAgence,
        DateTime? from, DateTime? to, bool? validatedOnly, int page, int pageSize, CancellationToken cancellationToken = default, VoyageSearchStatus? statut = null);
    Task<bool> NumVoyageExistsAsync(string numVoyage, string? excludingKey, CancellationToken cancellationToken = default);
    /// <summary>Counts attached dossiers, and how many are still awaiting invoicing.</summary>
    Task<(int Total, int Uninvoiced)> CountDossiersAsync(string keyVoyage, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetCalculatedOperationsAsync(string keyVoyage, CancellationToken cancellationToken = default);
    /// <summary>Drops a cost marker so the generator will recompute that operation.</summary>
    void RemoveCostMarker(string keyVoyage, string operation);
    /// <summary>True when an attached, non-deleted dossier sits in the given customs warehouse.</summary>
    Task<bool> HasMeadDossierAsync(string keyVoyage, string? lieuDedouanement, CancellationToken cancellationToken = default);
    void Add(CoutKilometrageVoyage ligne);
    /// <summary>Purges the per-leg kilometrage detail, mirroring <c>delete by (key_voyage, key_trajet)</c>.</summary>
    Task RemoveKilometrageAsync(string keyVoyage, CancellationToken cancellationToken = default);
    void Add(Voyage voyage);
    void Add(EquipementVoyage equipement);
    /// <summary>
    /// Resolves a fleet unit from its plate number.
    /// </summary>
    /// <remarks>
    /// The legacy equipment inserts look <c>t_materiel</c> up by <c>IMMATRICULATION</c> and
    /// <c>type_materiel</c>, not by key — tractors, trailers and boxes are all rows of <c>t_materiel</c>
    /// discriminated by type. A plate the fleet does not know yields null, and the legacy then stores an
    /// empty reference rather than failing.
    /// </remarks>
    Task<Materiel?> FindMaterielParImmatriculationAsync(
        string typeMateriel, string? immatriculation, CancellationToken cancellationToken = default);
    void Add(CoutOperationVoyageCalcule marker);
    void RemoveTrajet(TrajetVoyage trajet);
    void RemoveEquipement(EquipementVoyage equipement);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IDossierRepository
{
    Task<Dossier?> GetAsync(string keyDossier, bool includeColisages, CancellationToken cancellationToken = default);
    Task<PagedResult<Dossier>> SearchAsync(string? search, string? keyClient, string? keyVoyage, string? typeDossier,
        StatutFacturation? statutFacturation, bool? meadOnly, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Dossier>> GetByVoyageAsync(string keyVoyage, CancellationToken cancellationToken = default);
    Task<bool> NumDossierExistsAsync(string numDossier, string? excludingKey, CancellationToken cancellationToken = default);
    /// <summary>Sum of taxable weight for a voyage — the denominator of the pro-rata allocation.</summary>
    Task<decimal> SumPoidsTaxableAsync(string keyVoyage, CancellationToken cancellationToken = default);
    void Add(Dossier dossier);
    void Add(DossierVoyage association);
    /// <summary>
    /// Adds a packing line.
    /// </summary>
    /// <remarks>
    /// Distinct from the dossier's own <c>nb_colis</c> and <c>poids_brut</c>, which are totals: the legacy EDI
    /// import writes both, and the packing rows are what the packing list prints and what a per-package-type
    /// breakdown needs.
    /// </remarks>
    void Add(ColisageDossier colisage);
    /// <summary>A COD term, written by the EDI import when the file carries one.</summary>
    void Add(CodDossier cod);
    /// <summary>A supplier invoice referenced by the dossier; the EDI file carries up to five.</summary>
    void Add(FactureFournisseurDossier facture);
    void Remove(Dossier dossier);
    void Remove(ColisageDossier colisage);
    void Remove(DossierVoyage association);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

// ---------------------------------------------------------------------------------------------
// Voyage service
// ---------------------------------------------------------------------------------------------
public interface IVoyageService
{
    Task<PagedResult<VoyageListItem>> SearchAsync(string? search, CategorieVoyage? categorie, string? keyAgence,
        DateTime? from, DateTime? to, bool? validatedOnly, int page, int pageSize, CancellationToken cancellationToken = default, VoyageSearchStatus? statut = null);
    Task<VoyageDetail> GetAsync(string keyVoyage, CancellationToken cancellationToken = default);
    Task<VoyageDetail> CreateAsync(SaveVoyageRequest request, CancellationToken cancellationToken = default);
    Task<VoyageDetail> UpdateAsync(string keyVoyage, SaveVoyageRequest request, CancellationToken cancellationToken = default);
    Task<VoyageDetail> ValidateAsync(string keyVoyage, DateTime dateVoyage, CancellationToken cancellationToken = default);
    Task<VoyageDetail> CloseAsync(string keyVoyage, DateTime dateCloture, CancellationToken cancellationToken = default);
    Task<VoyageDetail> ReopenAsync(string keyVoyage, CancellationToken cancellationToken = default);
    Task DeleteAsync(string keyVoyage, CancellationToken cancellationToken = default);
    Task<TrajetDto> AddTrajetAsync(string keyVoyage, SaveTrajetRequest request, CancellationToken cancellationToken = default);
    Task RemoveTrajetAsync(string keyVoyage, string keyTrajet, CancellationToken cancellationToken = default);
    Task<EquipementDto> AddEquipementAsync(string keyVoyage, string typeEquipement, string? natureEquipement, CancellationToken cancellationToken = default);
    Task AssignDossierAsync(string keyVoyage, string keyDossier, int? position, CancellationToken cancellationToken = default);
    Task UnassignDossierAsync(string keyVoyage, string keyDossier, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CostOperationState>> GetCostOperationsAsync(string keyVoyage, CancellationToken cancellationToken = default);
    Task MarkCostOperationAsync(string keyVoyage, string operation, CancellationToken cancellationToken = default);
    Task ClearCostOperationAsync(string keyVoyage, string operation, CancellationToken cancellationToken = default);
}

/// <summary>
/// Voyage use cases.
/// </summary>
/// <remarks>
/// The important rules reproduced here are the ones the legacy screens enforced with inline SQL plus a
/// <c>MsgBox</c>: validation stamps <c>date_voyage</c> (which the whole costing module keys off),
/// closing is refused while a dossier is uninvoiced, and assigning or removing a dossier also maintains
/// the <c>t_dossiers_voyage</c> association row and the voyage's dossier count — which is the divisor
/// used by the equal-split costing rule, so letting it drift would silently mis-cost every voyage.
/// </remarks>
public sealed class VoyageService(
    IVoyageRepository voyages,
    IDossierRepository dossiers,
    IKeyGenerator keys,
    IAuditTrail audit,
    ITenantContext tenant) : IVoyageService
{
    private const string TraceCreation = "$CVOY";
    private const string TraceModification = "$MVOY";
    private const string TraceValidation = "$VVOY";
    private const string TraceCloture = "$CLOV";
    private const string TraceSuppression = "$SEV";

    public async Task<PagedResult<VoyageListItem>> SearchAsync(string? search, CategorieVoyage? categorie,
        string? keyAgence, DateTime? from, DateTime? to, bool? validatedOnly, int page, int pageSize,
        CancellationToken cancellationToken = default, VoyageSearchStatus? statut = null)
    {
        GuardPaging(page, pageSize);

        if (statut.HasValue && !Enum.IsDefined(statut.Value))
            throw new BusinessRuleException("voyage.search.status", "Statut de voyage invalide.");
        var result = await voyages.SearchAsync(search, categorie, keyAgence, from, to, validatedOnly, page, pageSize, cancellationToken, statut);

        return new PagedResult<VoyageListItem>(
            result.Items.Select(v => new VoyageListItem(
                v.KeyVoyage, v.NumVoyage, v.TypeVoyage, v.CategorieVoyage, v.NatureVoyage,
                v.PortDepart, v.PortArrivee, v.NavireDepart, v.DateDepartPrevu, v.DateArriveePrevu,
                v.DateVoyage, v.DateCloture, v.KeyStatutVoyage, v.KeyAgenceExploitation,
                v.IntituleAgenceExploitation, v.IsMead, v.Supprime, v.Provenance, v.Destination)).ToArray(),
            result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<VoyageDetail> GetAsync(string keyVoyage, CancellationToken cancellationToken = default) =>
        Map(await RequireAsync(keyVoyage, cancellationToken));

    public async Task<VoyageDetail> CreateAsync(SaveVoyageRequest request, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(request.NumVoyage)
            && await voyages.NumVoyageExistsAsync(request.NumVoyage, null, cancellationToken))
        {
            throw new DuplicateEntityException("Voyage", "NUM_VOYAGE", request.NumVoyage);
        }

        var key = await keys.NextAsync("VOYAGE", cancellationToken);
        var now = DateTime.UtcNow;
        var voyage = new Voyage(key, tenant.SocieteKey, request.TypeVoyage, request.NumVoyage, now);

        Apply(voyage, request, now);
        voyages.Add(voyage);
        await voyages.SaveChangesAsync(cancellationToken);

        await audit.TraceAsync<TraceVoyage>(key, TraceCreation,
            $"Voyage {voyage.NumVoyage}, type={voyage.TypeVoyage}", cancellationToken: cancellationToken);

        return Map(voyage);
    }

    public async Task<VoyageDetail> UpdateAsync(string keyVoyage, SaveVoyageRequest request, CancellationToken cancellationToken = default)
    {
        var voyage = await RequireAsync(keyVoyage, cancellationToken);

        if (voyage.IsCloture)
        {
            throw new BusinessRuleException("voyage.cloture", "Un voyage clôturé ne peut plus être modifié.");
        }

        if (!string.IsNullOrWhiteSpace(request.NumVoyage)
            && await voyages.NumVoyageExistsAsync(request.NumVoyage, keyVoyage, cancellationToken))
        {
            throw new DuplicateEntityException("Voyage", "NUM_VOYAGE", request.NumVoyage);
        }

        Apply(voyage, request, DateTime.UtcNow);
        await voyages.SaveChangesAsync(cancellationToken);

        await audit.TraceAsync<TraceVoyage>(keyVoyage, TraceModification, $"Numéro={voyage.NumVoyage}",
            cancellationToken: cancellationToken);

        return Map(voyage);
    }

    public async Task<VoyageDetail> ValidateAsync(string keyVoyage, DateTime dateVoyage, CancellationToken cancellationToken = default)
    {
        var voyage = await RequireAsync(keyVoyage, cancellationToken);
        voyage.Validate(dateVoyage);

        // The legacy validation silently switches the voyage into the MEAD flow when any attached
        // dossier carries a customs warehouse.
        var attached = await dossiers.GetByVoyageAsync(keyVoyage, cancellationToken);
        if (attached.Any(d => d.IsMead))
        {
            voyage.EnterMeadFlow(Voyage.StatutMeadAttenteEntree, Voyage.LibelleMeadAttenteEntree, dateVoyage);
        }

        await voyages.SaveChangesAsync(cancellationToken);
        await audit.TraceAsync<TraceVoyage>(keyVoyage, TraceValidation,
            $"Validation du voyage au {dateVoyage:dd/MM/yyyy}", cancellationToken: cancellationToken);

        return Map(voyage);
    }

    public async Task<VoyageDetail> CloseAsync(string keyVoyage, DateTime dateCloture, CancellationToken cancellationToken = default)
    {
        var voyage = await RequireAsync(keyVoyage, cancellationToken);

        var (_, uninvoiced) = await voyages.CountDossiersAsync(keyVoyage, cancellationToken);
        voyage.Close(dateCloture, uninvoiced > 0);

        await voyages.SaveChangesAsync(cancellationToken);
        await audit.TraceAsync<TraceVoyage>(keyVoyage, TraceCloture, "Clôture du voyage", cancellationToken: cancellationToken);

        return Map(voyage);
    }

    public async Task<VoyageDetail> ReopenAsync(string keyVoyage, CancellationToken cancellationToken = default)
    {
        var voyage = await RequireAsync(keyVoyage, cancellationToken);
        voyage.Reopen();

        await voyages.SaveChangesAsync(cancellationToken);
        await audit.TraceAsync<TraceVoyage>(keyVoyage, TraceModification, "Réouverture du voyage", cancellationToken: cancellationToken);

        return Map(voyage);
    }

    public async Task DeleteAsync(string keyVoyage, CancellationToken cancellationToken = default)
    {
        var voyage = await RequireAsync(keyVoyage, cancellationToken);

        // Detaching is required first: the legacy code refuses to delete a voyage that still carries
        // dossiers rather than silently orphaning them.
        var (total, _) = await voyages.CountDossiersAsync(keyVoyage, cancellationToken);
        if (total > 0)
        {
            throw new BusinessRuleException(
                "voyage.delete.dossiers",
                $"Suppression impossible : {total} dossier(s) sont encore rattachés à ce voyage.");
        }

        voyage.SoftDelete();
        await voyages.SaveChangesAsync(cancellationToken);

        await audit.TraceAsync<TraceVoyage>(keyVoyage, TraceSuppression, "Suppression du voyage", cancellationToken: cancellationToken);
    }

    public async Task<TrajetDto> AddTrajetAsync(string keyVoyage, SaveTrajetRequest request, CancellationToken cancellationToken = default)
    {
        var voyage = await RequireAsync(keyVoyage, cancellationToken);
        var key = await keys.NextAsync("TRAJET_VOYAGE", cancellationToken);

        var trajet = new TrajetVoyage(key, voyage.KeyVoyage, request.Ordre, request.Provenance, request.Destination);
        trajet.SetDates(request.DateDepart, request.DateArrivee, request.DateDepart, request.DateArrivee);
        trajet.SetMoyens(request.KeyTracteur, request.NumTracteur, request.KeyRemorque, request.NumRemorque,
            null, request.KeyChauffeur1, null, request.KeyChauffeur2, null);
        trajet.SetKilometrage(request.Kilometrage, request.FraisAutoroute, request.NumPlomb, null);

        voyage.AddTrajet(trajet);
        await voyages.SaveChangesAsync(cancellationToken);

        await audit.TraceAsync<TraceVoyage>(keyVoyage, TraceModification, $"Ajout trajet {key}", cancellationToken: cancellationToken);

        return new TrajetDto(trajet.KeyTrajet, trajet.Ordre, trajet.Provenance, trajet.Destination,
            trajet.DateDepart, trajet.DateArrivee, trajet.NumTracteur, trajet.NumRemorque,
            trajet.IntituleChauffeur1, trajet.Kilometrage, trajet.FraisAutoroute, trajet.NumPlomb);
    }

    public async Task RemoveTrajetAsync(string keyVoyage, string keyTrajet, CancellationToken cancellationToken = default)
    {
        var voyage = await RequireAsync(keyVoyage, cancellationToken);
        var trajet = voyage.Trajets.SingleOrDefault(t => t.KeyTrajet == keyTrajet)
            ?? throw new EntityNotFoundException("TrajetVoyage", keyTrajet);

        voyages.RemoveTrajet(trajet);
        await voyages.SaveChangesAsync(cancellationToken);
    }

    public async Task<EquipementDto> AddEquipementAsync(string keyVoyage, string typeEquipement, string? natureEquipement,
        CancellationToken cancellationToken = default)
    {
        var voyage = await RequireAsync(keyVoyage, cancellationToken);
        var key = await keys.NextAsync("parametrage", cancellationToken);

        // Merges into an existing type/nature line rather than inserting a duplicate.
        var equipement = voyage.AddOrIncrementEquipement(key, typeEquipement, natureEquipement);

        await voyages.SaveChangesAsync(cancellationToken);

        return new EquipementDto(equipement.KeyLigneEquipement, equipement.TypeEquipementCode,
            equipement.NatureEquipement, equipement.Qte, []);
    }

    public async Task AssignDossierAsync(string keyVoyage, string keyDossier, int? position, CancellationToken cancellationToken = default)
    {
        var voyage = await RequireAsync(keyVoyage, cancellationToken);
        var dossier = await dossiers.GetAsync(keyDossier, false, cancellationToken)
            ?? throw new EntityNotFoundException("Dossier", keyDossier);

        if (voyage.IsCloture)
        {
            throw new BusinessRuleException("voyage.cloture", "Un dossier ne peut pas être affecté à un voyage clôturé.");
        }

        var previousVoyage = dossier.KeyVoyageInter;
        dossier.AssignVoyage(keyVoyage, position);

        // The association table records every pairing; re-affectation removes the old one.
        if (!string.IsNullOrWhiteSpace(previousVoyage) && previousVoyage != keyVoyage)
        {
            dossiers.Remove(new DossierVoyage(keyDossier, previousVoyage));
        }

        dossiers.Add(new DossierVoyage(keyDossier, keyVoyage));

        await dossiers.SaveChangesAsync(cancellationToken);
        await RefreshVoyageCountersAsync(voyage, cancellationToken);

        await audit.TraceAsync<TraceVoyage>(keyVoyage, "$AFDV", $"Affectation dossier {keyDossier}",
            keyDocument: keyDossier, cancellationToken: cancellationToken);
        await audit.TraceAsync<TraceDossier>(keyDossier, "$AFDV", $"Affectation au voyage {keyVoyage}",
            cancellationToken: cancellationToken);
    }

    public async Task UnassignDossierAsync(string keyVoyage, string keyDossier, CancellationToken cancellationToken = default)
    {
        var voyage = await RequireAsync(keyVoyage, cancellationToken);
        var dossier = await dossiers.GetAsync(keyDossier, false, cancellationToken)
            ?? throw new EntityNotFoundException("Dossier", keyDossier);

        dossier.AssignVoyage(null, null);
        dossiers.Remove(new DossierVoyage(keyDossier, keyVoyage));

        await dossiers.SaveChangesAsync(cancellationToken);
        await RefreshVoyageCountersAsync(voyage, cancellationToken);
    }

    public async Task<IReadOnlyList<CostOperationState>> GetCostOperationsAsync(string keyVoyage, CancellationToken cancellationToken = default)
    {
        await RequireAsync(keyVoyage, cancellationToken);
        var calculated = await voyages.GetCalculatedOperationsAsync(keyVoyage, cancellationToken);

        return CoutOperationVoyageCalcule.StandardOperations
            .Concat([CoutOperationVoyageCalcule.FraisAutoroute, CoutOperationVoyageCalcule.FraisGasoil])
            .Select(op => new CostOperationState(op, calculated.Contains(op)))
            .ToArray();
    }

    public async Task MarkCostOperationAsync(string keyVoyage, string operation, CancellationToken cancellationToken = default)
    {
        var voyage = await RequireAsync(keyVoyage, cancellationToken);

        if (!voyage.IsValide)
        {
            throw new BusinessRuleException(
                "voyage.cost.not_validated",
                "Le coût d'un voyage ne peut être calculé qu'après validation (date_voyage).");
        }

        voyages.Add(new CoutOperationVoyageCalcule(keyVoyage, operation));
        await voyages.SaveChangesAsync(cancellationToken);
    }

    public async Task ClearCostOperationAsync(string keyVoyage, string operation, CancellationToken cancellationToken = default)
    {
        await RequireAsync(keyVoyage, cancellationToken);
        var markers = await voyages.GetCalculatedOperationsAsync(keyVoyage, cancellationToken);

        if (!markers.Contains(operation))
        {
            return;
        }

        // Recomputing means dropping the marker so the generator will pick the operation up again.
        voyages.RemoveCostMarker(keyVoyage, operation);
        await voyages.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Recomputes the voyage's dossier count and totals. The count is the divisor of the equal-split
    /// costing rule, so it must never drift from the actual number of attached dossiers.
    /// </summary>
    private async Task RefreshVoyageCountersAsync(Voyage voyage, CancellationToken cancellationToken)
    {
        var attached = await dossiers.GetByVoyageAsync(voyage.KeyVoyage, cancellationToken);

        voyage.SetCompteurs(
            attached.Count,
            attached.Sum(d => d.PoidsBrut ?? 0m),
            attached.Sum(d => d.NbColis ?? 0m));

        await voyages.SaveChangesAsync(cancellationToken);
    }

    private static void Apply(Voyage voyage, SaveVoyageRequest r, DateTime now)
    {
        voyage.UpdateEntete(r.NumVoyage, r.TypeVoyage, r.NatureVoyage, r.CategorieVoyage,
            r.ServiceVoyage, r.Provenance, r.Destination, r.Commentaire);
        voyage.SetAgences(r.KeyAgenceSaisie, r.IntituleAgenceSaisie, r.KeyAgenceExploitation, r.IntituleAgenceExploitation);
        voyage.SetItineraire(r.DateDepartPrevu, r.DateArriveePrevu, r.DateDepart, r.DateArrivee,
            r.PortDepart, r.PortArrivee, r.NavireDepart, r.NavireArrivee, voyage.DateDepartTrans, voyage.DateArriveeTrans, voyage.PortTransbordement);
        voyage.SetPersonnel(r.KeyExploitant, r.KeyExploitant == voyage.KeyExploitant ? voyage.IntituleExploitant : null,
            r.KeyChauffeur1, r.KeyChauffeur1 == voyage.KeyChauffeur1 ? voyage.IntituleChauffeur1 : null,
            r.KeyChauffeur2, r.KeyChauffeur2 == voyage.KeyChauffeur2 ? voyage.IntituleChauffeur2 : null);
        voyage.SetDouane(r.NumAcquit, r.NumAcquit == voyage.NumAcquit ? voyage.DateAcquit : null,
            r.NumManifeste, r.NumManifeste == voyage.NumManifeste ? voyage.DateManifeste : null,
            r.NumDs, r.NumDs == voyage.NumDs ? voyage.DateDs : null, r.NumCnt);
        voyage.SetMoyens(r.NumRemorque, r.NumTracteur, r.NumTc, r.NumCaisse, voyage.NumTriptyque, voyage.DateTriptyque, voyage.Tare, voyage.CapaciteRemorque);
        voyage.SetMagasinMead(r.MagasinMead, r.MagasinMead == voyage.MagasinMead ? voyage.EntreeMead : null);
    }

    private async Task<Voyage> RequireAsync(string keyVoyage, CancellationToken cancellationToken) =>
        await voyages.GetAsync(keyVoyage, true, cancellationToken)
        ?? throw new EntityNotFoundException("Voyage", keyVoyage);

    /// <summary>
    /// Delegates to <see cref="Paging.Valider"/> so there is one rule, not a copy per service.
    /// </summary>
    private static void GuardPaging(int page, int pageSize) => Paging.Valider(page, pageSize);

    private static SaveVoyageRequest Editable(Voyage v) => new()
    {
        NumVoyage = v.NumVoyage,
        TypeVoyage = v.TypeVoyage,
        NatureVoyage = v.NatureVoyage,
        CategorieVoyage = v.CategorieVoyage,
        ServiceVoyage = v.ServiceVoyage,
        Provenance = v.Provenance,
        Destination = v.Destination,
        Commentaire = v.Commentaire,
        KeyAgenceSaisie = v.KeyAgenceSaisie,
        IntituleAgenceSaisie = v.IntituleAgenceSaisie,
        KeyAgenceExploitation = v.KeyAgenceExploitation,
        IntituleAgenceExploitation = v.IntituleAgenceExploitation,
        DateDepartPrevu = v.DateDepartPrevu,
        DateArriveePrevu = v.DateArriveePrevu,
        DateDepart = v.DateDepart,
        DateArrivee = v.DateArrivee,
        PortDepart = v.PortDepart,
        PortArrivee = v.PortArrivee,
        NavireDepart = v.NavireDepart,
        NavireArrivee = v.NavireArrivee,
        KeyExploitant = v.KeyExploitant,
        KeyChauffeur1 = v.KeyChauffeur1,
        KeyChauffeur2 = v.KeyChauffeur2,
        NumAcquit = v.NumAcquit,
        NumManifeste = v.NumManifeste,
        NumDs = v.NumDs,
        NumCnt = v.NumCnt,
        NumBooking = v.NumBooking,
        NumRemorque = v.NumRemorque,
        NumTracteur = v.NumTracteur,
        NumTc = v.NumTc,
        NumCaisse = v.NumCaisse,
        MagasinMead = v.MagasinMead
    };

    private static VoyageDetail Map(Voyage v) => new(
        v.KeyVoyage, v.SocieteKey, v.NumVoyage, v.TypeVoyage, v.NatureVoyage, v.CategorieVoyage, v.ServiceVoyage,
        v.KeyStatutVoyage, v.DateStatutVoyage, v.DateCreation, v.DateVoyage, v.DateCloture, v.IsValide, v.IsCloture,
        v.KeyAgenceSaisie, v.KeyAgenceExploitation, v.IntituleAgenceExploitation, v.DateDepartPrevu,
        v.DateArriveePrevu, v.DateDepart, v.DateArrivee, v.PortDepart, v.PortArrivee, v.NavireDepart,
        v.NavireArrivee, v.Provenance, v.Destination, v.KeyExploitant, v.KeyChauffeur1, v.KeyChauffeur2,
        v.NumAcquit, v.NumManifeste, v.NumDs, v.NumCnt, v.NumBooking, v.NumRemorque, v.NumTracteur, v.NumTc,
        v.NumCaisse, v.KeyStatutVoyageMead, v.LibelleStatutVoyageMead, v.MagasinMead, v.IsMead,
        v.NbDossVoyageSaisi, v.TotalPoidsBrutSaisi, v.TotalNbColisSaisi, v.CostDivisor, v.StatutReception,
        v.Commentaire, v.Attachement, v.Supprime,
        v.Trajets.OrderBy(t => t.Ordre).Select(t => new TrajetDto(t.KeyTrajet, t.Ordre, t.Provenance,
            t.Destination, t.DateDepart, t.DateArrivee, t.NumTracteur, t.NumRemorque, t.IntituleChauffeur1,
            t.Kilometrage, t.FraisAutoroute, t.NumPlomb)).ToArray(),
        v.Equipements.Select(x => new EquipementDto(x.KeyLigneEquipement, x.TypeEquipementCode,
            x.NatureEquipement, x.Qte,
            x.Details.Select(d => new DetailEquipementDto(d.KeyEquipement, d.NumEquipement, d.NumPlomb,
                d.Tare, d.DateRestitution, d.NbColis, d.PoidsBrut, d.Volume)).ToArray())).ToArray(), Editable(v));
}

// ---------------------------------------------------------------------------------------------
// Dossier service
// ---------------------------------------------------------------------------------------------
public interface IDossierService
{
    Task<PagedResult<DossierListItem>> SearchAsync(string? search, string? keyClient, string? keyVoyage,
        string? typeDossier, StatutFacturation? statutFacturation, bool? meadOnly, int page, int pageSize,
        CancellationToken cancellationToken = default);
    Task<DossierDetail> GetAsync(string keyDossier, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DossierListItem>> ListByVoyageAsync(string keyVoyage, CancellationToken cancellationToken = default);
    Task<DossierDetail> CreateAsync(SaveDossierRequest request, CancellationToken cancellationToken = default);
    Task<DossierDetail> UpdateAsync(string keyDossier, SaveDossierRequest request, CancellationToken cancellationToken = default);
    Task<DossierDetail> DepotageAsync(string keyDossier, DepotageRequest request, CancellationToken cancellationToken = default);
    Task<DossierDetail> CancelDepotageAsync(string keyDossier, CancellationToken cancellationToken = default);
    Task<DossierDetail> SetStatutFacturationAsync(string keyDossier, StatutFacturation statut, CancellationToken cancellationToken = default);
    Task DeleteAsync(string keyDossier, CancellationToken cancellationToken = default);
    Task<ColisageDto> AddColisageAsync(string keyDossier, SaveColisageRequest request, CancellationToken cancellationToken = default);
    Task RemoveColisageAsync(string keyDossier, int keyLigneColisage, CancellationToken cancellationToken = default);
    /// <summary>
    /// Splits a sales amount across the dossiers of a voyage pro rata to taxable weight — the rule the
    /// legacy <c>Repartition_Facture_vente</c> procedure applies.
    /// </summary>
    Task<IReadOnlyList<(string KeyDossier, decimal MontantMad, decimal MontantDevise)>> SplitAmountByPoidsTaxableAsync(
        string keyVoyage, decimal montantMad, decimal montantDevise, CancellationToken cancellationToken = default);
}

public sealed class DossierService(
    IDossierRepository dossiers,
    IVoyageRepository voyages,
    IKeyGenerator keys,
    IAuditTrail audit,
    ITenantContext tenant,
    ICallerRolesAccessor roles) : IDossierService
{
    private const string TraceCreation = "$CDOS";
    private const string TraceModification = "$DDOS";
    private const string TraceSuppression = "$SDOS";
    private const string TraceDepotage = "$DEPOT";
    private const string TraceFacturation = "$FADOSS";

    public async Task<PagedResult<DossierListItem>> SearchAsync(string? search, string? keyClient, string? keyVoyage,
        string? typeDossier, StatutFacturation? statutFacturation, bool? meadOnly, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        GuardPaging(page, pageSize);

        var result = await dossiers.SearchAsync(search, keyClient, keyVoyage, typeDossier, statutFacturation,
            meadOnly, page, pageSize, cancellationToken);

        return new PagedResult<DossierListItem>(result.Items.Select(ToListItem).ToArray(),
            result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<DossierDetail> GetAsync(string keyDossier, CancellationToken cancellationToken = default) =>
        Map(await RequireAsync(keyDossier, cancellationToken));

    public async Task<IReadOnlyList<DossierListItem>> ListByVoyageAsync(string keyVoyage, CancellationToken cancellationToken = default) =>
        (await dossiers.GetByVoyageAsync(keyVoyage, cancellationToken)).Select(ToListItem).ToArray();

    public async Task<DossierDetail> CreateAsync(SaveDossierRequest request, CancellationToken cancellationToken = default)
    {
        // Dossier-type scoping: t_personnel_type_dossier records which types an employee may handle. Checked
        // before anything is written, so a refusal leaves no partial state behind.
        if (!roles.Roles.AutoriseTypeDossier(request.TypeDossier))
        {
            throw new ForbiddenException(
                "dossier.type_dossier",
                $"Vous n'êtes pas habilité à créer un dossier de type « {request.TypeDossier} ».");
        }

        if (await dossiers.NumDossierExistsAsync(request.NumDossier, null, cancellationToken))
        {
            throw new DuplicateEntityException("Dossier", "NUM_DOSSIER", request.NumDossier);
        }

        var key = await keys.NextAsync("DOSSIER", cancellationToken);
        var dossier = new Dossier(key, tenant.SocieteKey, request.NumDossier, request.TypeDossier, DateTime.UtcNow);

        Apply(dossier, request);
        dossiers.Add(dossier);
        await dossiers.SaveChangesAsync(cancellationToken);

        await audit.TraceAsync<TraceDossier>(key, TraceCreation,
            $"Dossier {dossier.NumDossier}, type={dossier.TypeDossier}", cancellationToken: cancellationToken);

        return Map(dossier);
    }

    public async Task<DossierDetail> UpdateAsync(string keyDossier, SaveDossierRequest request, CancellationToken cancellationToken = default)
    {
        var dossier = await RequireAsync(keyDossier, cancellationToken);

        if (await dossiers.NumDossierExistsAsync(request.NumDossier, keyDossier, cancellationToken))
        {
            throw new DuplicateEntityException("Dossier", "NUM_DOSSIER", request.NumDossier);
        }

        var wasMead = dossier.IsMead;
        Apply(dossier, request);

        await dossiers.SaveChangesAsync(cancellationToken);

        // Entering the MEAD flow must also be reflected on the parent voyage.
        if (!wasMead && dossier.IsMead && !string.IsNullOrWhiteSpace(dossier.KeyVoyageInter))
        {
            var voyage = await voyages.GetAsync(dossier.KeyVoyageInter!, true, cancellationToken);
            voyage?.EnterMeadFlow(Voyage.StatutMeadAttenteEntree, Voyage.LibelleMeadAttenteEntree, DateTime.UtcNow);
            await voyages.SaveChangesAsync(cancellationToken);
        }

        await audit.TraceAsync<TraceDossier>(keyDossier, TraceModification, $"Numéro={dossier.NumDossier}",
            cancellationToken: cancellationToken);

        return Map(dossier);
    }

    public async Task<DossierDetail> DepotageAsync(string keyDossier, DepotageRequest request, CancellationToken cancellationToken = default)
    {
        var dossier = await RequireAsync(keyDossier, cancellationToken);

        // The MEAD precondition lives on the entity, so it is enforced for every caller; nothing to
        // duplicate here.
        dossier.Depotage(request.DateDepotage, request.LieuDepotage, request.ConstatDepotage,
            request.ColisDepote, request.RefDepotage);

        await dossiers.SaveChangesAsync(cancellationToken);
        await audit.TraceAsync<TraceDossier>(keyDossier, TraceDepotage,
            $"Dépotage au {request.DateDepotage:dd/MM/yyyy}, colis={request.ColisDepote}, lieu={request.LieuDepotage}",
            valeur: (double)(request.ColisDepote ?? 0m), cancellationToken: cancellationToken);

        return Map(dossier);
    }

    public async Task<DossierDetail> CancelDepotageAsync(string keyDossier, CancellationToken cancellationToken = default)
    {
        var dossier = await RequireAsync(keyDossier, cancellationToken);
        dossier.CancelDepotage();

        await dossiers.SaveChangesAsync(cancellationToken);
        await audit.TraceAsync<TraceDossier>(keyDossier, "$SDPC", "Annulation du dépotage", cancellationToken: cancellationToken);

        return Map(dossier);
    }

    public async Task<DossierDetail> SetStatutFacturationAsync(string keyDossier, StatutFacturation statut,
        CancellationToken cancellationToken = default)
    {
        var dossier = await RequireAsync(keyDossier, cancellationToken);
        dossier.SetStatutFacturation(statut);

        await dossiers.SaveChangesAsync(cancellationToken);
        await audit.TraceAsync<TraceDossier>(keyDossier, TraceFacturation, $"Statut facturation = {statut}",
            cancellationToken: cancellationToken);

        return Map(dossier);
    }

    public async Task DeleteAsync(string keyDossier, CancellationToken cancellationToken = default)
    {
        var dossier = await RequireAsync(keyDossier, cancellationToken);

        if (dossier.StatutFacturation == StatutFacturation.Facture)
        {
            throw new BusinessRuleException("dossier.delete.invoiced", "Un dossier facturé ne peut pas être supprimé.");
        }

        // Detach from the voyage so the parent's dossier count stays truthful.
        var voyageKey = dossier.KeyVoyageInter;
        dossier.AssignVoyage(null, null);
        dossier.SoftDelete();

        if (!string.IsNullOrWhiteSpace(voyageKey))
        {
            dossiers.Remove(new DossierVoyage(keyDossier, voyageKey!));
        }

        await dossiers.SaveChangesAsync(cancellationToken);
        await audit.TraceAsync<TraceDossier>(keyDossier, TraceSuppression, $"Suppression du dossier {dossier.NumDossier}",
            cancellationToken: cancellationToken);
    }

    public async Task<ColisageDto> AddColisageAsync(string keyDossier, SaveColisageRequest request, CancellationToken cancellationToken = default)
    {
        var dossier = await RequireAsync(keyDossier, cancellationToken);

        var colisage = new ColisageDossier(dossier.KeyDossier, request.TypeColis, request.NbColis, request.NatureMarchandise);
        colisage.Update(request.NbColis, request.NatureMarchandise, request.Marque, request.CodeNgp);
        colisage.SetMesures(request.PoidsBrut, request.Volume, null);

        dossier.AddColisage(colisage);
        await dossiers.SaveChangesAsync(cancellationToken);

        return new ColisageDto(colisage.KeyLigneColisage, colisage.TypeColis, colisage.NbColis,
            colisage.NatureMarchandise, colisage.PoidsBrut, colisage.Volume, colisage.Marque, colisage.CodeNgp);
    }

    public async Task RemoveColisageAsync(string keyDossier, int keyLigneColisage, CancellationToken cancellationToken = default)
    {
        var dossier = await RequireAsync(keyDossier, cancellationToken);
        var colisage = dossier.Colisages.SingleOrDefault(c => c.KeyLigneColisage == keyLigneColisage)
            ?? throw new EntityNotFoundException("ColisageDossier", keyLigneColisage.ToString());

        dossiers.Remove(colisage);
        await dossiers.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<(string KeyDossier, decimal MontantMad, decimal MontantDevise)>> SplitAmountByPoidsTaxableAsync(
        string keyVoyage, decimal montantMad, decimal montantDevise, CancellationToken cancellationToken = default)
    {
        var attached = await dossiers.GetByVoyageAsync(keyVoyage, cancellationToken);

        // Delegates to the domain calculator so the pro-rata rule is unit-tested in one place.
        return Domain.Entities.Invoicing.AllocationCalculator.SplitByPoidsTaxable(
            montantMad, montantDevise,
            attached.Select(d => (d.KeyDossier, d.PoidsTaxable ?? 0m)));
    }

    private static void Apply(Dossier dossier, SaveDossierRequest r)
    {
        dossier.UpdateEntete(r.NumDossier, r.TypeDossier, r.KeyClient, r.IntituleClient, r.NatureOperation,
            r.ServiceDossier, r.Position, r.CommentaireDossier);
        dossier.SetAgences(r.KeyAgenceExploitation, r.IntituleAgenceExploitation, dossier.KeyAgenceCommerciale, dossier.IntituleAgenceCommerciale, dossier.KeyAgenceSaisie, dossier.IntituleAgenceSaisie);
        dossier.SetParties(r.KeyExpediteur, r.IntituleExpediteur, r.KeyDestinataire, r.IntituleDestinataire,
            r.KeyPayeur, r.IntitulePayeur, r.KeyAgent, r.IntituleAgent, dossier.RefAgent);
        dossier.SetMarchandise(r.DescriptionMarchandise, r.MarchandiseGerbable, r.MarchandiseDangereuse,
            r.ClasseMarchandiseDangereuse, r.MarchandiseFragile, r.Assurance, dossier.ValeurMarchandise, dossier.DeviseValeurMarchandise, dossier.TauxChangeValMarch,
            r.NbColis, r.TypeColis, dossier.Suspendu, r.Marque);
        dossier.SetMesures(r.PoidsBrut, r.Volume, dossier.Mpl, r.PoidsTaxable, r.VolumeTaxable, dossier.EquivalenceVolumePoids, dossier.EquivalenceMplPoids, dossier.EquivalenceMplVolume);
        dossier.SetIncoterm(r.Incoterm);
        dossier.SetMagasinDouane(r.MagasinDouane);
        dossier.SetCustoms(r.HsCode, r.CodeRegime, r.CodeBureau, dossier.NumDum, dossier.DateDum, dossier.ValeurDeclareeDouane, dossier.LivraisonPar);
        dossier.SetTransport(r.TemperatureDirigee, r.TemperatureMin, r.TemperatureMax, r.MontantFret, r.DeviseFret);
    }

    private async Task<Dossier> RequireAsync(string keyDossier, CancellationToken cancellationToken) =>
        await dossiers.GetAsync(keyDossier, true, cancellationToken)
        ?? throw new EntityNotFoundException("Dossier", keyDossier);

    /// <summary>
    /// Delegates to <see cref="Paging.Valider"/> so there is one rule, not a copy per service.
    /// </summary>
    private static void GuardPaging(int page, int pageSize) => Paging.Valider(page, pageSize);

    private static DossierListItem ToListItem(Dossier d) => new(
        d.KeyDossier, d.NumDossier, d.TypeDossier, d.KeyClient, d.IntituleClient, d.KeyVoyageInter,
        d.DateCreation, d.PoidsBrut, d.PoidsTaxable, d.NbColis, d.KeyStatutDossier, d.LibelleStatutDossier,
        d.MagasinDouane, d.StatutFacturation, d.StatutCloture, d.Supprime);

    private static SaveDossierRequest Editable(Dossier d) => new()
    {
        NumDossier = d.NumDossier ?? string.Empty,
        TypeDossier = d.TypeDossier ?? string.Empty,
        KeyClient = d.KeyClient,
        IntituleClient = d.IntituleClient,
        NatureOperation = d.NatureOperation,
        ServiceDossier = d.ServiceDossier,
        Position = d.Position,
        CommentaireDossier = d.CommentaireDossier,
        KeyAgenceExploitation = d.KeyAgenceExploitation,
        IntituleAgenceExploitation = d.IntituleAgenceExploitation,
        KeyExpediteur = d.KeyExpediteur,
        IntituleExpediteur = d.IntituleExpediteur,
        KeyDestinataire = d.KeyDestinataire,
        IntituleDestinataire = d.IntituleDestinataire,
        KeyPayeur = d.KeyPayeur,
        IntitulePayeur = d.IntitulePayeur,
        KeyAgent = d.KeyAgent,
        IntituleAgent = d.IntituleAgent,
        Incoterm = d.Incoterm,
        DescriptionMarchandise = d.DescriptionMarchandise,
        MarchandiseGerbable = d.MarchandiseGerbable,
        MarchandiseDangereuse = d.MarchandiseDangereuse,
        ClasseMarchandiseDangereuse = d.ClasseMarchandiseDangereuse,
        MarchandiseFragile = d.MarchandiseFragile,
        Assurance = d.Assurance,
        NbColis = d.NbColis,
        TypeColis = d.TypeColis,
        Marque = d.Marque,
        PoidsBrut = d.PoidsBrut,
        Volume = d.Volume,
        PoidsTaxable = d.PoidsTaxable,
        VolumeTaxable = d.VolumeTaxable,
        MagasinDouane = d.MagasinDouane,
        HsCode = d.HsCode,
        CodeRegime = d.CodeRegime,
        CodeBureau = d.CodeBureau,
        MontantFret = d.MontantFret,
        DeviseFret = d.DeviseFret,
        TemperatureDirigee = d.TemperatureDirigee,
        TemperatureMin = d.TemperatureMin,
        TemperatureMax = d.TemperatureMax
    };

    private static DossierDetail Map(Dossier d) => new(
        d.KeyDossier, d.SocieteKey, d.NumDossier, d.TypeDossier, d.DateCreation, d.KeyVoyageInter, d.KeyBooking,
        d.KeyCotation, d.KeyClient, d.IntituleClient, d.KeyPayeur, d.KeyExpediteur, d.IntituleExpediteur,
        d.KeyDestinataire, d.IntituleDestinataire, d.Incoterm, d.DescriptionMarchandise, d.MarchandiseGerbable,
        d.MarchandiseDangereuse, d.ClasseMarchandiseDangereuse, d.MarchandiseFragile, d.Assurance, d.NbColis,
        d.TypeColis, d.PoidsBrut, d.Volume, d.PoidsTaxable, d.VolumeTaxable, d.Position, d.MagasinDouane,
        d.KeyStatutDossier, d.LibelleStatutDossier, d.KeyStatutMead, d.LibelleStatutDossierMead, d.DateDepotage,
        d.LieuDepotage, d.ColisDepote, d.ColisSortie, d.HasStockRemaining, d.IsMead, d.StatutFacturation,
        d.StatutCloture, d.Litige, d.Cod, d.HsCode, d.CodeRegime, d.MontantFret, d.DeviseFret, d.Attachement,
        d.Supprime,
        d.Colisages.Select(c => new ColisageDto(c.KeyLigneColisage, c.TypeColis, c.NbColis, c.NatureMarchandise,
            c.PoidsBrut, c.Volume, c.Marque, c.CodeNgp)).ToArray(),
        Editable(d), d.ConstatDepotage, d.RefDepotage);
}

// ---------------------------------------------------------------------------------------------
// Voyage cost generation
// ---------------------------------------------------------------------------------------------
/// <summary>
/// Caller-supplied rates for the cost run.
/// </summary>
/// <remarks>
/// <b>Why these are passed in rather than read from the database.</b> The company and agency costs come
/// from <c>t_societe</c> / <c>t_agence</c>, which are migrated, so the service reads those itself. The
/// per-day and per-kilometre rates for tractors, trailers, drivers, gasoil and AdBlue live in
/// <c>t_materiel</c> and <c>t_personnel</c>, which are <em>not</em> part of this increment. Rather than
/// substituting zeroes — which would silently cost every voyage at nothing — they are required inputs,
/// so an unmigrated rate source is visible at the call site instead of producing quietly wrong numbers.
/// </remarks>
public sealed record VoyageCostRates
{
    public decimal CoutFraisBancaire { get; init; }
    public IReadOnlyList<VoyageCostLeg> Legs { get; init; } = [];
    public IReadOnlyList<VoyageCostEquipment> Equipements { get; init; } = [];
    public IReadOnlyList<VoyageCostPersonnel> Personnel { get; init; } = [];
}

public sealed record VoyageCostPreview(
    string KeyVoyage, string MoisCalcul, bool Eligible, string? Raison,
    int NbDossiers, decimal TotalMad,
    IReadOnlyList<CoutOperationAllocation> Allocations,
    IReadOnlyList<CoutKilometrageLigne> KilometrageLignes,
    IReadOnlyList<string> OperationsIgnorees);

public interface IVoyageCostService
{
    /// <summary>Computes what the cost run <em>would</em> produce, without persisting anything.</summary>
    Task<VoyageCostPreview> PreviewAsync(string keyVoyage, string moisCalcul, VoyageCostRates rates, CancellationToken cancellationToken = default);
    /// <summary>Runs the cost generation and persists the allocations, markers and kilometre detail.</summary>
    Task<VoyageCostPreview> GenerateAsync(string keyVoyage, string moisCalcul, VoyageCostRates rates, CancellationToken cancellationToken = default);
}

/// <summary>
/// Voyage cost generation — the port of <c>ClassGeneration_Cout_Voyage.Generation_Cout_Voyage</c>.
/// </summary>
/// <remarks>
/// The legacy routine was the only place in the whole application that used a real database
/// transaction (on a second connection). Here the work is a single <c>SaveChanges</c>, which gives the
/// same all-or-nothing guarantee without a hand-rolled transaction.
/// </remarks>
public sealed class VoyageCostService(
    IVoyageRepository voyages,
    IDossierRepository dossiers,
    IFactureVenteRepository factures,
    IReferenceDataRepository referenceData,
    IAuditTrail audit,
    ITenantContext tenant) : IVoyageCostService
{
    private const string TraceCostGenerated = "$GCSV";

    public async Task<VoyageCostPreview> PreviewAsync(
        string keyVoyage, string moisCalcul, VoyageCostRates rates, CancellationToken cancellationToken = default)
    {
        var (inputs, _) = await BuildInputsAsync(keyVoyage, moisCalcul, rates, cancellationToken);
        var result = VoyageCostEngine.Generate(inputs);
        var (eligible, raison) = VoyageCostEngine.IsEligible(inputs);

        return new VoyageCostPreview(keyVoyage, moisCalcul, eligible, raison, inputs.NbDossVoyageSaisi,
            result.TotalMad, result.Allocations, result.KilometrageLignes, result.OperationsIgnorees);
    }

    public async Task<VoyageCostPreview> GenerateAsync(
        string keyVoyage, string moisCalcul, VoyageCostRates rates, CancellationToken cancellationToken = default)
    {
        var (inputs, voyage) = await BuildInputsAsync(keyVoyage, moisCalcul, rates, cancellationToken);

        var (eligible, raison) = VoyageCostEngine.IsEligible(inputs);
        if (!eligible)
        {
            throw new BusinessRuleException("voyage.cost.ineligible", raison!);
        }

        var attached = await dossiers.GetByVoyageAsync(keyVoyage, cancellationToken);
        if (attached.Count == 0)
        {
            throw new BusinessRuleException("voyage.cost.no_dossier",
                "Aucun dossier n'est rattaché à ce voyage : la répartition des coûts est impossible.");
        }

        var result = VoyageCostEngine.Generate(inputs);

        // Regenerated wholesale per run: the legacy purge deletes the operation's rows before
        // re-inserting, which is why the marker exists at all.
        foreach (var allocation in result.Allocations)
        {
            foreach (var dossier in attached)
            {
                // The legacy cost rows set KEY_FACTURE = KEY_LIGNE_DETAILFACT = key_voyage, which is why
                // both are passed the voyage key rather than an invoice key.
                factures.Add(new RepartitionDossier(
                    keyFacture: keyVoyage,
                    keyLigneDetailFact: keyVoyage,
                    keyVoyageInter: keyVoyage,
                    keyDossierInter: dossier.KeyDossier,
                    montantMad: Math.Round(allocation.MontantMad, 3, MidpointRounding.AwayFromZero),
                    montantDevise: Math.Round(allocation.MontantDevise, 3, MidpointRounding.AwayFromZero),
                    devise: inputs.Devise,
                    natureRepartition: NatureRepartition.Achat,
                    typeRepartition: allocation.Operation,
                    moisCout: moisCalcul));
            }

            voyages.Add(new CoutOperationVoyageCalcule(keyVoyage, allocation.Operation));
        }

        // AdBlue and gasoil rows must be dropped for the legs before being rewritten.
        if (result.KilometrageLignes.Count > 0)
        {
            await voyages.RemoveKilometrageAsync(keyVoyage, cancellationToken);

            foreach (var ligne in result.KilometrageLignes)
            {
                voyages.Add(new CoutKilometrageVoyage(ligne));
            }
        }

        await voyages.SaveChangesAsync(cancellationToken);
        await factures.SaveChangesAsync(cancellationToken);

        voyage.MarkPremiereFacture(DateTime.UtcNow);

        await audit.TraceAsync<TraceVoyage>(keyVoyage, TraceCostGenerated,
            $"Génération des coûts pour {moisCalcul} : {string.Join(", ", result.OperationsGenerees)}",
            valeur: (double)result.TotalMad, cancellationToken: cancellationToken);

        return new VoyageCostPreview(keyVoyage, moisCalcul, true, null, attached.Count, result.TotalMad,
            result.Allocations, result.KilometrageLignes, result.OperationsIgnorees);
    }

    private async Task<(VoyageCostInputs Inputs, Voyage Voyage)> BuildInputsAsync(
        string keyVoyage, string moisCalcul, VoyageCostRates rates, CancellationToken cancellationToken)
    {
        var voyage = await voyages.GetAsync(keyVoyage, true, cancellationToken)
            ?? throw new EntityNotFoundException("Voyage", keyVoyage);

        var societe = await referenceData.FindAsync<Domain.Entities.ReferenceData.Societe>(tenant.SocieteKey);

        Domain.Entities.ReferenceData.Agence? agence = null;
        if (!string.IsNullOrWhiteSpace(voyage.KeyAgenceExploitation))
        {
            agence = await referenceData.FindAsync<Domain.Entities.ReferenceData.Agence>(voyage.KeyAgenceExploitation!);
        }

        var calculated = await voyages.GetCalculatedOperationsAsync(keyVoyage, cancellationToken);
        var hasMead = await voyages.HasMeadDossierAsync(keyVoyage, agence?.LieuDedouanement, cancellationToken);

        var inputs = new VoyageCostInputs
        {
            KeyVoyage = keyVoyage,
            MoisCalcul = moisCalcul,
            Devise = societe?.DeviseSoc ?? "MAD",
            DateVoyage = voyage.DateVoyage,
            Supprime = voyage.Supprime,
            KeyAgenceExploitation = voyage.KeyAgenceExploitation,
            LieuDedouanement = agence?.LieuDedouanement,
            NbDossVoyageSaisi = (int)(voyage.NbDossVoyageSaisi ?? 0m),
            CoutSociete = societe?.CoutSociete ?? 0m,
            CoutMead = societe?.CoutMead ?? 0m,
            CoutAssuranceMarchandise = societe?.CoutAssuranceMarchandise ?? 0m,
            CoutAgence = agence?.CoutAgence ?? 0m,
            CoutFraisBancaire = rates.CoutFraisBancaire,
            Legs = rates.Legs,
            Equipements = rates.Equipements,
            Personnel = rates.Personnel,
            HasMeadDossier = hasMead,
            OperationsDejaCalculees = calculated
        };

        return (inputs, voyage);
    }
}
