using Logistics.Application.Abstractions;
using Logistics.Domain.Common;
using Logistics.Domain.Entities.Resources;
namespace Logistics.Application.Features.Resources;

public sealed record CiterneDto(
    string KeyCiterne, string? IntituleCiterne, string? Agence, string TypeCiterne,
    decimal Stock, decimal Pmp, decimal ValeurStock, decimal? IndexCompteur, bool EstConfreres);

public sealed record SortieGasoilDto(
    string KeySortie, string KeyCiterne, DateTime DateSortie, string TypeCiterne, string? TypeSortie,
    decimal QteSortie, decimal? PrixUnitaire, decimal? MontantSortie,
    string? KeyVoyage, string? KeyMateriel, string? KeyChauffeur, string? Matricule, decimal? Compteur,
    bool Plein, string? Observation, bool Supprime);

public sealed record JaugeGasoilDto(
    string KeyJauge, string KeyCiterne, DateTime DateJauge,
    decimal? QteStock, decimal? QteStockTheorique, decimal Ecart);

public sealed record CreateCiterneRequest(
    string? IntituleCiterne, TypeCiterne TypeCiterne, string? Agence = null, decimal? IndexCompteur = null);

public sealed record ReceptionCiterneRequest(decimal Quantite, decimal Montant, decimal? IndexCompteur = null);

public sealed record SortieCiterneRequest(
    decimal Quantite,
    DateTime DateSortie,
    string? KeyVoyage = null,
    string? KeyMateriel = null,
    string? KeyChauffeur = null,
    string? Matricule = null,
    decimal? Compteur = null,
    bool Plein = false,
    string? Observation = null,
    /// <summary>
    /// Which issue variant this is; see <see cref="SortieGasoil.TypeSortieInterne"/> and friends. Defaults to
    /// an ordinary internal issue.
    /// </summary>
    string? TypeSortie = null,
    /// <summary>
    /// The other party, for the transporteur and confrère variants. The confrère variant requires it: the
    /// partner's consignment balance is keyed by it.
    /// </summary>
    /// <remarks>
    /// Null for an internal issue, which is fuel drawn for the company's own vehicles and involves no third
    /// party.
    /// </remarks>
    string? KeyTransporteur = null);

public sealed record JaugeCiterneRequest(decimal QuantiteReelle, DateTime DateJauge, decimal? IndexCompteur = null);

public interface ICarburantRepository
{
    Task<Citerne?> FindCiterneAsync(string keyCiterne, CancellationToken cancellationToken = default);
    Task<PagedResult<Citerne>> SearchCiternesAsync(string? recherche, TypeCiterne? type, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<SortieGasoil?> FindSortieAsync(string keySortie, CancellationToken cancellationToken = default);
    Task<PagedResult<SortieGasoil>> SearchSortiesAsync(string? keyCiterne, DateTime? du, DateTime? au, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<ApprovisionnementCiterne?> FindApprovisionnementAsync(string keyApprovisionnement, CancellationToken cancellationToken = default);
    Task<PagedResult<ApprovisionnementCiterne>> SearchApprovisionnementsAsync(string? keyFournisseur, DateTime? du, DateTime? au, int page, int pageSize, CancellationToken cancellationToken = default);

    void Add(Citerne citerne);
    void Add(SortieGasoil sortie);
    void Add(JaugeGasoil jauge);
    void Add(ApprovisionnementCiterne approvisionnement);
    /// <summary>The consolidated journal row paired with an operational document.</summary>
    void Add(ConsommationGasoil consommation);
    /// <summary>Finds the journal row by the key it shares with its operational document.</summary>
    Task<ConsommationGasoil?> FindConsommationAsync(string keyConsommation, CancellationToken cancellationToken = default);
    Task<BonGasoil?> FindBonAsync(string keyBon, CancellationToken cancellationToken = default);
    Task<PagedResult<BonGasoil>> SearchBonsAsync(string? keyFournisseur, DateTime? du, DateTime? au, int page, int pageSize, CancellationToken cancellationToken = default);
    void Add(BonGasoil bon);
    /// <summary>Finds a consignment balance by partner, keyed (key_confrere, key_soc).</summary>
    Task<StockGasoilConfrere?> FindStockConfrereAsync(string keyConfrere, CancellationToken cancellationToken = default);
    void Add(StockGasoilConfrere stock);
    /// <summary>
    /// Movements of one vehicle and product that have not yet been counted in a measurement.
    /// </summary>
    /// <remarks>
    /// Tracked, because the caller marks the consumed ones in the same save.
    /// </remarks>
    Task<IReadOnlyList<ConsommationGasoil>> GetNonCalculeesAsync(
        string keyMateriel, string typeCiterne, CancellationToken cancellationToken = default);
    void Add(CoutKilometrageGasoil ratio);
    /// <summary>The measurements a vehicle already has.</summary>
    Task<IReadOnlyList<CoutKilometrageGasoil>> GetRatiosAsync(
        string keyMateriel, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>One tank line of a delivery.</summary>
public sealed record LigneApprovisionnementRequest(string KeyCiterne, decimal Quantite, int? IndexCompteur = null);

/// <summary>
/// A delivery note.
/// </summary>
/// <remarks>
/// <see cref="Montant"/> is the BL's total and the module's only cost figure: there is no unit price, and the
/// per-tank amounts are allocated from it pro rata by quantity.
/// </remarks>
public sealed record CreateApprovisionnementRequest(
    string KeyFournisseur,
    DateTime DateApprovisionnement,
    decimal Montant,
    string? NumBl = null,
    IReadOnlyList<LigneApprovisionnementRequest>? Lignes = null);

public sealed record LigneApprovisionnementDto(
    string KeyCiterne, int? IndexCompteur, decimal QteCiterne, decimal MontantAlloue);

public sealed record ApprovisionnementDto(
    string KeyApprovisionnement,
    string SocieteKey,
    string KeyFournisseur,
    DateTime DateApprovisionnement,
    string? NumBl,
    decimal QteTotale,
    decimal MontantApprovisionnement,
    bool Attachement,
    IReadOnlyList<LigneApprovisionnementDto> Lignes,
    /// <summary>Always false — see <see cref="ApprovisionnementCiterne.EstAnnulable"/>.</summary>
    bool Annulable);

/// <summary>
/// A fuel voucher from a supplier.
/// </summary>
/// <remarks>
/// A voucher is <b>purchase</b> fuel, not an issue: it names a supplier and <b>no tank</b>, and the legacy
/// writes no tank update for it (<c>frm_Saisie_Bon_Gasoil.vb:249-254</c>). The tank is filled later, when a
/// replenishment records where the fuel actually went.
/// </remarks>
public sealed record CreateBonGasoilRequest(
    /// <summary>
    /// Supplier. Null for the confrère variant, which has no supplier — the fellow company is the
    /// <see cref="KeyTransporteur"/> instead.
    /// </summary>
    string? KeyFournisseur,
    DateTime DateBon,
    decimal Quantite,
    string? NumBon = null,
    string? Devise = null,
    decimal? Montant = null,
    TypeCiterne? TypeCiterne = null,
    string? KeyVoyage = null,
    string? KeyMateriel = null,
    string? KeyChauffeur = null,
    string? KeyTransporteur = null,
    string? Matricule = null,
    decimal? Compteur = null,
    bool Plein = false,
    string? Observation = null,
    /// <summary>
    /// Which voucher variant this is; see <see cref="BonGasoil.TypeBonGasoil"/> and friends. Defaults to an
    /// ordinary supplier voucher.
    /// </summary>
    /// <remarks>
    /// The legacy writes the same literal to the voucher's <c>type_bon</c> and the journal's
    /// <c>type_sortie</c>, so one value drives both.
    /// </remarks>
    string? TypeBon = null);

public sealed record BonGasoilDto(    string KeyBon,
    string SocieteKey,
    string? NumBon,
    DateTime DateBon,
    DateTime? DateSaisie,
    string? KeyFournisseur,
    string? KeyVoyage,
    string? KeyMateriel,
    string? KeyChauffeur,
    string? KeyTransporteur,
    decimal? Compteur,
    decimal QteBon,
    string? DeviseBon,
    decimal? MontantBon,
    string? TypeCiterne,
    bool Plein,
    string? TypeBon,
    string? Observation);

/// <summary>A cost-per-kilometre measurement over one full-tank window.</summary>
public sealed record CoutKilometrageGasoilDto(
    string KeyLigne,
    string KeyTracteur,
    DateTime DateDebut,
    DateTime DateFin,
    int? TotalKm,
    decimal? TotalLitre,
    decimal? TotalMontant,
    decimal? CoutKm,
    /// <summary>Litres per 100 km.</summary>
    decimal? RatioConsommation);

public interface ICarburantService
{
    Task<PagedResult<CiterneDto>> SearchCiternesAsync(string? recherche, TypeCiterne? type, int page = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<CiterneDto> GetCiterneAsync(string keyCiterne, CancellationToken cancellationToken = default);
    Task<CiterneDto> CreateCiterneAsync(CreateCiterneRequest request, CancellationToken cancellationToken = default);

    /// <summary>Receives a delivery and re-weights the tank's average price.</summary>
    Task<CiterneDto> ReceptionnerAsync(string keyCiterne, ReceptionCiterneRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues fuel, valuing it at the tank's current average price and reducing the stock.
    /// </summary>
    Task<SortieGasoilDto> SortirAsync(string keyCiterne, SortieCiterneRequest request, CancellationToken cancellationToken = default);

    /// <summary>Cancels an issue: soft-deletes it and restores the stock.</summary>
    Task<SortieGasoilDto> AnnulerSortieAsync(string keySortie, CancellationToken cancellationToken = default);

    /// <summary>Records a physical dip and returns the gap against the theoretical stock.</summary>
    Task<JaugeGasoilDto> JaugerAsync(string keyCiterne, JaugeCiterneRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a tank delivery and applies its cost to each tank's average price.
    /// </summary>
    Task<ApprovisionnementDto> ApprovisionnerAsync(CreateApprovisionnementRequest request, CancellationToken cancellationToken = default);

    Task<ApprovisionnementDto> GetApprovisionnementAsync(string keyApprovisionnement, CancellationToken cancellationToken = default);

    Task<PagedResult<ApprovisionnementDto>> SearchApprovisionnementsAsync(string? keyFournisseur, DateTime? du, DateTime? au, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a supplier fuel voucher and its journal row.
    /// </summary>
    /// <remarks>
    /// Both rows carry the <b>same key</b>, as the legacy does — one <c>IncrementationKeyApp("GASOIL")</c>
    /// written as <c>key_bon</c> and <c>key_consommation</c>.
    /// </remarks>
    Task<BonGasoilDto> CreerBonAsync(CreateBonGasoilRequest request, CancellationToken cancellationToken = default);

    Task<BonGasoilDto> GetBonAsync(string keyBon, CancellationToken cancellationToken = default);

    Task<PagedResult<BonGasoilDto>> SearchBonsAsync(string? keyFournisseur, DateTime? du, DateTime? au, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Measures a vehicle's cost per kilometre over its next full-tank window.
    /// </summary>
    /// <returns>The measurement, or null when no window can be closed yet.</returns>
    /// <remarks>
    /// Null is the normal answer while a vehicle has fewer than two full tanks recorded — it is still running
    /// on the fuel it took on, and nothing can be measured. See <see cref="RatioCoutKm"/> for the window rule.
    /// </remarks>
    Task<CoutKilometrageGasoilDto?> CalculerRatioAsync(string keyMateriel, string? produit = null, CancellationToken cancellationToken = default);

    /// <summary>The measurements a vehicle already has, most recent first.</summary>
    Task<IReadOnlyList<CoutKilometrageGasoilDto>> GetRatiosAsync(string keyMateriel, CancellationToken cancellationToken = default);

    Task<PagedResult<SortieGasoilDto>> SearchSortiesAsync(string? keyCiterne, DateTime? du, DateTime? au, int page = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}

/// <summary>
/// Fuel tank and issue use cases — the port of <c>frm_Saisie_Reception_Gasoil.vb</c>,
/// <c>frm_Saisie_Sortie_Gasoil.vb</c>, <c>frm_saisie_jauge_gasoil.vb</c> and the fuel integration screens.
/// </summary>
/// <remarks>
/// The stock move and the movement row are written in one unit of work. The legacy screens issued the
/// stock update and the insert as separate statements, so a failure between them left the tank and the
/// ledger disagreeing — the exact discrepancy a dip then has to absorb.
/// </remarks>
public sealed class CarburantService(
    ICarburantRepository carburant,
    IKeyGenerator keys,
    IAuditTrail audit,
    ITenantContext tenant) : ICarburantService
{
    public async Task<PagedResult<CiterneDto>> SearchCiternesAsync(
        string? recherche, TypeCiterne? type, int page = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        Guard(page);

        var resultat = await carburant.SearchCiternesAsync(recherche, type, page, Math.Clamp(pageSize, 1, 200), cancellationToken);

        return new PagedResult<CiterneDto>(
            resultat.Items.Select(Map).ToArray(), resultat.Page, resultat.PageSize, resultat.TotalCount);
    }

    public async Task<CiterneDto> GetCiterneAsync(string keyCiterne, CancellationToken cancellationToken = default) =>
        Map(await RequireCiterneAsync(keyCiterne, cancellationToken));

    public async Task<CiterneDto> CreateCiterneAsync(CreateCiterneRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.IntituleCiterne))
        {
            throw new BusinessRuleException("citerne.intitule", "L'intitulé de la citerne est obligatoire.");
        }

        var key = await keys.NextAsync(SequenceGasoil.Operation, cancellationToken);
        var citerne = new Citerne(key, tenant.SocieteKey, request.IntituleCiterne, request.TypeCiterne);
        citerne.SetEntete(request.IntituleCiterne, request.Agence, request.IndexCompteur);

        carburant.Add(citerne);
        await carburant.SaveChangesAsync(cancellationToken);
        await audit.JournalAsync("Citerne", "Création", $"{key} — {request.IntituleCiterne}", cancellationToken);

        return Map(citerne);
    }

    /// <summary>
    /// Receives a delivery into the tank.
    /// </summary>
    /// <remarks>
    /// The tank's average price is re-weighted by the delivered quantity and cost, so a small expensive
    /// delivery barely moves it. The tank's stock is never edited directly here — the entity owns both
    /// figures so they cannot diverge.
    /// </remarks>
    public async Task<CiterneDto> ReceptionnerAsync(string keyCiterne, ReceptionCiterneRequest request, CancellationToken cancellationToken = default)
    {
        var citerne = await RequireCiterneAsync(keyCiterne, cancellationToken);

        var pmpAvant = citerne.Pmp ?? 0m;
        citerne.Receptionner(request.Quantite, request.Montant);

        if (request.IndexCompteur.HasValue)
        {
            citerne.SetEntete(citerne.IntituleCiterne, citerne.Agence, request.IndexCompteur);
        }

        await carburant.SaveChangesAsync(cancellationToken);
        await audit.JournalAsync("Citerne", "Réception",
            $"{keyCiterne} : +{request.Quantite} L pour {request.Montant} (PMP {pmpAvant} → {citerne.Pmp})",
            cancellationToken);

        return Map(citerne);
    }

    /// <summary>
    /// Issues fuel from a tank to a vehicle.
    /// </summary>
    /// <remarks>
    /// The issue is valued at the tank's <b>current</b> average price and that price is stored on the
    /// movement. Reading it back later from the tank would restate every past issue each time a delivery
    /// arrived, so the snapshot is what makes historical fuel cost stable.
    /// </remarks>
    public async Task<SortieGasoilDto> SortirAsync(string keyCiterne, SortieCiterneRequest request, CancellationToken cancellationToken = default)
    {
        var citerne = await RequireCiterneAsync(keyCiterne, cancellationToken);

        if (citerne.EstConfreres)
        {
            // The sentinel row is not real stock; issuing from it corrupts the totals.
            throw new BusinessRuleException(
                "citerne.confreres", "La citerne « CITERNES CONFRERES » n'est pas un stock réel.");
        }

        var sortie = new SortieGasoil(
            await keys.NextAsync(SequenceGasoil.Operation, cancellationToken),
            tenant.SocieteKey, keyCiterne, citerne.TypeCiterne, request.DateSortie, request.Quantite);

        sortie.SetAffectation(request.KeyVoyage, request.KeyMateriel, request.KeyChauffeur,
            request.Matricule, request.Compteur, request.Plein);

        var typeSortie = string.IsNullOrWhiteSpace(request.TypeSortie)
            ? SortieGasoil.TypeSortieInterne
            : request.TypeSortie.Trim();

        // The confrère variant records which partner the fuel went to, because that is what its consignment
        // balance is keyed by.
        var estConfrere = string.Equals(typeSortie, SortieGasoil.TypeSortieConfrere, StringComparison.OrdinalIgnoreCase);

        if (estConfrere && string.IsNullOrWhiteSpace(request.KeyTransporteur))
        {
            throw new BusinessRuleException(
                "sortie.confrere.transporteur",
                "Une sortie confrère doit désigner le confrère concerné.");
        }

        sortie.SetType(typeSortie, request.Observation, request.KeyTransporteur);

        // The stock guard lives on the entity, so the move and the valuation cannot disagree.
        var prixUnitaire = citerne.Sortir(request.Quantite);
        sortie.AppliquerPrix(prixUnitaire, DateTime.UtcNow);

        // The journal row, under the SAME key as the document above — the legacy issues one
        // IncrementationKeyApp("GASOIL") and writes it as both key_sortie and key_consommation
        // (frm_Saisie_Sortie_Gasoil.vb:319-323), which is what lets the pair be rejoined later.
        var consommation = new ConsommationGasoil(
            sortie.Id, tenant.SocieteKey, typeSortie,
            request.DateSortie, keyCiterne, request.Quantite, 0m, prixUnitaire,
            sortie.TypeCiterne.ToString());

        consommation.SetAffectation(request.KeyVoyage, request.KeyMateriel, request.KeyChauffeur,
            request.Matricule, request.Compteur, request.Plein);
        // Derives the amount from the snapshot price, so the ledger cannot disagree with the document.
        consommation.SetPrixUnitaire(prixUnitaire);

        carburant.Add(sortie);
        carburant.Add(consommation);

        // A confrère issue is fuel taken out of a real tank and given to a partner, so it increases what they
        // owe us — the mirror of a confrère voucher, which increases what we owe them. Note the tank itself is
        // decremented above like any other issue: the fuel physically left the company's stock.
        if (estConfrere)
        {
            var stock = await carburant.FindStockConfrereAsync(request.KeyTransporteur!, cancellationToken);

            if (stock is null)
            {
                stock = new StockGasoilConfrere(request.KeyTransporteur!, tenant.SocieteKey);
                stock.AjouterSortie(request.Quantite);
                carburant.Add(stock);
            }
            else
            {
                stock.AjouterSortie(request.Quantite);
            }
        }

        await carburant.SaveChangesAsync(cancellationToken);

        await audit.JournalAsync("SortieGasoil", "Sortie",
            $"{keyCiterne} : -{request.Quantite} L à {prixUnitaire}/L", cancellationToken);

        return Map(sortie);
    }

    /// <summary>
    /// Cancels an issue and restores the tank.
    /// </summary>
    /// <remarks>
    /// The movement is soft-deleted rather than removed, because the audit trail of what was issued has to
    /// survive the correction.
    /// </remarks>
    public async Task<SortieGasoilDto> AnnulerSortieAsync(string keySortie, CancellationToken cancellationToken = default)
    {
        var sortie = await carburant.FindSortieAsync(keySortie, cancellationToken)
            ?? throw new EntityNotFoundException("SortieGasoil", keySortie);

        if (sortie.Supprime)
        {
            return Map(sortie);
        }

        var citerne = await RequireCiterneAsync(sortie.KeyCiterne, cancellationToken);

        sortie.SupprimerLogiquement();
        citerne.AnnulerSortie(sortie.QteSortie);

        // The journal row follows its document. The legacy soft-deletes both rather than removing either, so
        // a cancelled movement still appears in the history of what was issued.
        var consommation = await carburant.FindConsommationAsync(sortie.Id, cancellationToken);
        consommation?.SupprimerLogiquement();

        // A cancelled confrère issue gives the consignment movement back too. Leaving it would keep counting
        // fuel as handed to the partner when the document says it was not, and their statement would stop
        // reconciling.
        if (string.Equals(sortie.TypeSortie, SortieGasoil.TypeSortieConfrere, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(sortie.KeyTransporteur))
        {
            var stock = await carburant.FindStockConfrereAsync(sortie.KeyTransporteur!, cancellationToken);
            stock?.RetirerSortie(sortie.QteSortie);
        }

        await carburant.SaveChangesAsync(cancellationToken);
        await audit.JournalAsync("SortieGasoil", "Annulation",
            $"{keySortie} : +{sortie.QteSortie} L restitués à {sortie.KeyCiterne}", cancellationToken);

        return Map(sortie);
    }

    /// <summary>
    /// Records a physical dip against the theoretical stock.
    /// </summary>
    /// <remarks>
    /// The theoretical figure is captured onto the dip row, because the point of a dip is to compare
    /// reality against what the system believed <em>at that moment</em>. The stock is deliberately left
    /// untouched — the legacy dip observed, it did not correct; that is what the correction screen is for.
    /// </remarks>
    public async Task<JaugeGasoilDto> JaugerAsync(string keyCiterne, JaugeCiterneRequest request, CancellationToken cancellationToken = default)
    {
        var citerne = await RequireCiterneAsync(keyCiterne, cancellationToken);

        var ecart = citerne.Jauger(request.QuantiteReelle);

        var jauge = new JaugeGasoil(
            await keys.NextAsync(SequenceGasoil.Operation, cancellationToken),
            tenant.SocieteKey, keyCiterne, request.DateJauge, request.QuantiteReelle, citerne.Stock);

        jauge.SetIndexCompteur(request.IndexCompteur);

        carburant.Add(jauge);
        await carburant.SaveChangesAsync(cancellationToken);

        await audit.JournalAsync("JaugeGasoil", "Jauge",
            $"{keyCiterne} : écart {ecart} L", cancellationToken);

        return new JaugeGasoilDto(jauge.KeyJauge, keyCiterne, request.DateJauge,
            jauge.QteStock, jauge.QteStockTheorique, jauge.Ecart);
    }

    public async Task<PagedResult<SortieGasoilDto>> SearchSortiesAsync(
        string? keyCiterne, DateTime? du, DateTime? au, int page = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        Guard(page);

        var resultat = await carburant.SearchSortiesAsync(keyCiterne, du, au, page, Math.Clamp(pageSize, 1, 200), cancellationToken);

        return new PagedResult<SortieGasoilDto>(
            resultat.Items.Select(Map).ToArray(), resultat.Page, resultat.PageSize, resultat.TotalCount);
    }

    private static void Guard(int page)
    {
        if (page < 1)
        {
            throw new ArgumentException("La page doit être supérieure ou égale à 1.", nameof(page));
        }
    }

    private async Task<Citerne> RequireCiterneAsync(string keyCiterne, CancellationToken cancellationToken) =>
        await carburant.FindCiterneAsync(keyCiterne, cancellationToken)
        ?? throw new EntityNotFoundException("Citerne", keyCiterne);

    /// <summary>
    /// Records a tank delivery and applies its cost to each tank's average price.
    /// </summary>
    /// <remarks>
    /// Each tank is re-weighted by its <b>own allocated share</b> of the delivery note rather than by a unit
    /// price — which is the whole reason the allocation exists. The allocation and every tank update happen
    /// before a single save, so a delivery cannot be recorded without its cost being applied, or applied
    /// twice.
    /// </remarks>
    public async Task<ApprovisionnementDto> ApprovisionnerAsync(
        CreateApprovisionnementRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Lignes is null || request.Lignes.Count == 0)
        {
            throw new BusinessRuleException(
                "approvisionnement.lignes", "Un bon d'approvisionnement doit comporter au moins une citerne.");
        }

        var key = await keys.NextAsync(SequenceGasoil.Operation, cancellationToken);

        var appro = new ApprovisionnementCiterne(
            key, tenant.SocieteKey, request.KeyFournisseur, request.DateApprovisionnement,
            request.NumBl, request.Montant);

        foreach (var ligne in request.Lignes)
        {
            appro.AjouterLigne(ligne.KeyCiterne, ligne.IndexCompteur, ligne.Quantite);
        }

        foreach (var (ligne, montant) in appro.RepartirMontant())
        {
            var citerne = await RequireCiterneAsync(ligne.KeyCiterne, cancellationToken);
            citerne.Receptionner(ligne.QteCiterne, montant);

            // The physical counter reading is mirrored onto the tank in the same save, as the legacy does.
            if (ligne.IndexCompteur.HasValue)
            {
                citerne.SetEntete(citerne.IntituleCiterne, citerne.Agence, ligne.IndexCompteur);
            }
        }

        carburant.Add(appro);
        await carburant.SaveChangesAsync(cancellationToken);

        await audit.JournalAsync("Approvisionnement", "Création",
            $"{key} : {appro.QteTotale} L pour {appro.MontantApprovisionnement}", cancellationToken);

        return Map(appro);
    }

    public async Task<ApprovisionnementDto> GetApprovisionnementAsync(
        string keyApprovisionnement, CancellationToken cancellationToken = default)
    {
        var appro = await carburant.FindApprovisionnementAsync(keyApprovisionnement, cancellationToken)
            ?? throw new EntityNotFoundException("ApprovisionnementCiterne", keyApprovisionnement);

        return Map(appro);
    }

    public async Task<PagedResult<ApprovisionnementDto>> SearchApprovisionnementsAsync(
        string? keyFournisseur, DateTime? du, DateTime? au, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        Paging.Valider(page, pageSize);

        var result = await carburant.SearchApprovisionnementsAsync(
            keyFournisseur, du, au, page, pageSize, cancellationToken);

        return new PagedResult<ApprovisionnementDto>(
            result.Items.Select(Map).ToArray(), result.Page, result.PageSize, result.TotalCount);
    }

    /// <summary>
    /// Projects a delivery, re-deriving its line allocations.
    /// </summary>
    /// <remarks>
    /// The allocation is recomputed rather than read because it is never stored — there is no line amount
    /// column in the schema. An empty document is tolerated here rather than throwing, because a read should
    /// report what exists even if a legacy row predates the rule.
    /// </remarks>
    /// <summary>
    /// Records a supplier fuel voucher and its journal row.
    /// </summary>
    /// <remarks>
    /// A voucher is <b>purchase</b> fuel, so unlike an issue it touches <b>no tank</b> — the legacy writes no
    /// stock update for it. What it does write is the pair: one key as <c>key_bon</c> and <c>key_consommation</c>,
    /// so the voucher and its journal row can be rejoined.
    /// </remarks>
    public async Task<BonGasoilDto> CreerBonAsync(
        CreateBonGasoilRequest request, CancellationToken cancellationToken = default)
    {
        var key = await keys.NextAsync(SequenceGasoil.Operation, cancellationToken);

        // One literal drives both rows, as the legacy writes the same string to type_bon and type_sortie.
        var typeBon = string.IsNullOrWhiteSpace(request.TypeBon) ? BonGasoil.TypeBonGasoil : request.TypeBon.Trim();

        var bon = new BonGasoil(key, tenant.SocieteKey, request.NumBon, request.DateBon, request.Quantite);
        bon.SetFournisseur(request.KeyFournisseur, request.Devise, request.Montant);
        bon.SetAffectation(request.KeyVoyage, request.KeyMateriel, request.KeyChauffeur,
            request.KeyTransporteur, request.Matricule, request.Compteur, request.Plein);
        bon.SetType(request.TypeCiterne, typeBon, request.Observation, DateTime.UtcNow);

        // The unit price is derived for the journal's own column, but the AMOUNT is taken as given rather
        // than recomputed from it: a voucher's total is what the supplier billed, and re-deriving it from a
        // rounded unit price would move the figure away from the document.
        var prixUnitaire = request.Montant is > 0 && request.Quantite > 0
            ? request.Montant.Value / request.Quantite
            : 0m;

        var consommation = new ConsommationGasoil(
            key, tenant.SocieteKey, typeBon, request.DateBon,
            // No tank: fuel bought, not fuel drawn.
            keyCiterne: null,
            request.Quantite, request.Montant ?? 0m, prixUnitaire,
            request.TypeCiterne?.ToString() ?? ConsommationGasoil.TypeCiterneGasoil);

        consommation.SetAffectation(request.KeyVoyage, request.KeyMateriel, request.KeyChauffeur,
            request.Matricule, request.Compteur, request.Plein);
        // The voucher is the only writer that fills the journal's purchase columns.
        consommation.SetAchat(request.KeyFournisseur, request.Devise, request.NumBon);

        carburant.Add(bon);
        carburant.Add(consommation);

        // The confrère variant is the only one that moves the consignment balance: a voucher is fuel the
        // partner delivered to us, so it increases what we owe them.
        if (string.Equals(typeBon, BonGasoil.TypeBonConfrere, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(request.KeyTransporteur))
        {
            var stock = await carburant.FindStockConfrereAsync(request.KeyTransporteur!, cancellationToken);

            if (stock is null)
            {
                stock = new StockGasoilConfrere(request.KeyTransporteur!, tenant.SocieteKey);
                stock.AjouterEntree(request.Quantite);
                carburant.Add(stock);
            }
            else
            {
                // Already tracked, so the change is persisted by the save below.
                stock.AjouterEntree(request.Quantite);
            }
        }

        await carburant.SaveChangesAsync(cancellationToken);

        await audit.JournalAsync("BonGasoil", "Création",
            $"{key} : {request.Quantite} L fournisseur {request.KeyFournisseur}", cancellationToken);

        return Map(bon);
    }

    public async Task<BonGasoilDto> GetBonAsync(string keyBon, CancellationToken cancellationToken = default)
    {
        var bon = await carburant.FindBonAsync(keyBon, cancellationToken)
            ?? throw new EntityNotFoundException("BonGasoil", keyBon);

        return Map(bon);
    }

    public async Task<PagedResult<BonGasoilDto>> SearchBonsAsync(
        string? keyFournisseur, DateTime? du, DateTime? au, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        Paging.Valider(page, pageSize);

        var result = await carburant.SearchBonsAsync(keyFournisseur, du, au, page, pageSize, cancellationToken);

        return new PagedResult<BonGasoilDto>(
            result.Items.Select(Map).ToArray(), result.Page, result.PageSize, result.TotalCount);
    }

    /// <summary>
    /// Measures a vehicle's cost per kilometre over its next full-tank window.
    /// </summary>
    /// <remarks>
    /// The window rule — and in particular why the closing fill-up is summed but deliberately left unmarked —
    /// is documented on <see cref="RatioCoutKm"/>.
    /// <para>
    /// <b>Currency is not converted.</b> The legacy multiplies each amount by a budget rate from
    /// <c>dbo.recup_taux_budget</c>, a function over a rate table this backend does not model, so amounts are
    /// summed as stored. A window containing movements in more than one currency is therefore refused rather
    /// than silently added up, which would produce a cost per kilometre that looks authoritative and is wrong.
    /// </para>
    /// </remarks>
    public async Task<CoutKilometrageGasoilDto?> CalculerRatioAsync(
        string keyMateriel, string? produit = null, CancellationToken cancellationToken = default)
    {
        var typeCiterne = string.IsNullOrWhiteSpace(produit) ? ConsommationGasoil.TypeCiterneGasoil : produit.Trim();

        if (!Enum.TryParse<ProduitGasoil>(typeCiterne, ignoreCase: true, out var produitGasoil))
        {
            throw new BusinessRuleException("ratio.produit", $"Produit inconnu : « {typeCiterne} ».");
        }

        var lignes = await carburant.GetNonCalculeesAsync(keyMateriel, typeCiterne, cancellationToken);

        // A window needs two full tanks. With fewer, the vehicle is still running on the fuel it took on and
        // nothing can be measured — the legacy reports it as uncosted rather than guessing.
        var pleins = lignes.Where(l => l.Plein && l.CompteurConsommation.HasValue).ToList();

        if (pleins.Count < 2)
        {
            return null;
        }

        var ouverture = pleins[0];
        var fermeture = pleins[1];
        var debut = ouverture.DateConsommation;
        var fin = fermeture.DateConsommation;

        // Summed over EVERY movement in the window, not only the full tanks: the intermediate top-ups are fuel
        // this window consumed, and excluding them would understate both the litres and the cost.
        var dansLaSomme = lignes.Where(l => RatioCoutKm.DansLaSomme(l.DateConsommation, debut, fin)).ToList();

        var litres = dansLaSomme.Sum(l => l.QteConsommation);
        var montant = dansLaSomme.Sum(l => l.MontantConsommation);

        var devises = dansLaSomme
            .Select(l => l.DeviseConsommation)
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (devises.Count > 1)
        {
            throw new BusinessRuleException(
                "ratio.devises.multiples",
                $"La fenêtre mélange plusieurs devises ({string.Join(", ", devises)}). "
                + "La conversion par taux budgétaire n'est pas implémentée : le coût au kilomètre serait faux.");
        }

        var distance = RatioCoutKm.Distance(
            ouverture.CompteurConsommation!.Value, fermeture.CompteurConsommation!.Value, litres, montant);

        if (distance is null)
        {
            return null;
        }

        var ratio = new CoutKilometrageGasoil(
            await keys.NextAsync(SequenceGasoil.Operation, cancellationToken),
            keyMateriel, debut, fin, produitGasoil);

        ratio.SetTotaux(distance, litres, montant);
        carburant.Add(ratio);

        // The closing fill-up is NOT marked, so it opens the next window and the measurement chain continues.
        foreach (var ligne in lignes.Where(l => RatioCoutKm.DansLeMarquage(l.DateConsommation, debut, fin)))
        {
            ligne.MarquerRatioCalcule();
        }

        await carburant.SaveChangesAsync(cancellationToken);

        await audit.JournalAsync("RatioCoutKm", "Calcul",
            $"{keyMateriel} : {distance} km, {ratio.CoutKm}/km, {ratio.RatioConsommation} L/100km",
            cancellationToken);

        return Map(ratio);
    }

    public async Task<IReadOnlyList<CoutKilometrageGasoilDto>> GetRatiosAsync(
        string keyMateriel, CancellationToken cancellationToken = default)
    {
        var ratios = await carburant.GetRatiosAsync(keyMateriel, cancellationToken);

        return ratios.Select(Map).ToArray();
    }

    private static CoutKilometrageGasoilDto Map(CoutKilometrageGasoil r) => new(
        r.KeyLigne, r.KeyTracteur, r.DateDebut, r.DateFin,
        r.TotalKm, r.TotalLitre, r.TotalMontant, r.CoutKm, r.RatioConsommation);

    private static BonGasoilDto Map(BonGasoil b) => new(        b.KeyBon, b.SocieteKey, b.NumBon, b.DateBon, b.DateSaisie, b.KeyFournisseur,
        b.KeyVoyage, b.KeyMateriel, b.KeyChauffeur, b.KeyTransporteur, b.Compteur,
        b.QteBon, b.DeviseBon, b.MontantBon, b.TypeCiterne?.ToString(), b.Plein, b.TypeBon, b.Observation);

    private static ApprovisionnementDto Map(ApprovisionnementCiterne a)
    {
        var lignes = a.Lignes.Count == 0
            ? []
            : a.RepartirMontant()
                .Select(p => new LigneApprovisionnementDto(
                    p.Ligne.KeyCiterne, p.Ligne.IndexCompteur, p.Ligne.QteCiterne, p.MontantAlloue))
                .ToArray();

        return new ApprovisionnementDto(
            a.Id, a.SocieteKey, a.KeyFournisseur, a.DateApprovisionnement, a.NumBl,
            a.QteTotale, a.MontantApprovisionnement, a.Attachement, lignes,
            ApprovisionnementCiterne.EstAnnulable);
    }

    private static CiterneDto Map(Citerne c) => new(
        c.KeyCiterne, c.IntituleCiterne, c.Agence, c.TypeCiterne.ToString(),
        c.Stock, c.Pmp ?? 0m, c.ValeurStock, c.IndexCompteur, c.EstConfreres);

    private static SortieGasoilDto Map(SortieGasoil s) => new(
        s.KeySortie, s.KeyCiterne, s.DateSortie, s.TypeCiterne.ToString(), s.TypeSortie,
        s.QteSortie, s.PrixUnitaire, s.MontantSortie,
        s.KeyVoyage, s.KeyMateriel, s.KeyChauffeur, s.Matricule, s.Compteur,
        s.Plein, s.Observation, s.Supprime);
}
