using Logistics.Application.Abstractions;
using Logistics.Domain.Common;
using Logistics.Domain.Entities.Audit;
using Logistics.Domain.Entities.Invoicing;
using Logistics.Domain.Entities.ReferenceData;

namespace Logistics.Application.Features.Invoicing;

// ---------------------------------------------------------------------------------------------
// DTOs
// ---------------------------------------------------------------------------------------------
public sealed record FactureListItem(string KeyFacture, string? NumFacture, TypeFacture TypeFacture,
    string? KeyClient, string? IntituleClient, DateTime? DateFacture, DateTime? DateEcheance,
    string DeviseFacture, decimal TtcDevise, decimal NetAPayer, decimal MtRegle, bool FactRegle, string? StatutFacture);

public sealed record FactureLineDto(string KeyLigne, string? CodeRubrique, string? LibelleRubrique,
    NatureLigne NatureLigne, decimal MtTotalDevise, decimal TauxRemise, decimal MtDevise, decimal TauxChange,
    decimal MtHtMad, decimal TauxTva, string? CodeTva, bool Imprimable);

public sealed record FactureTvaDto(string CodeTva, decimal Ht, decimal Tva, decimal MtImpute);

public sealed record FactureDetail(string KeyFacture, string SocieteKey, string? NumFacture, TypeFacture TypeFacture,
    string? KeyClient, string? IntituleClient, string? KeyDossier, string? KeyVoyageInt, string? NatureService,
    DateTime? DateFacture, DateTime? DateEcheance, string? MoisFacture, string DeviseFacture, decimal TauxDeviseFacture,
    decimal DeboursMad, decimal AssigneMad, decimal HtMad, decimal TvaMad, decimal TaxeRegionale, decimal TotalHtMad,
    decimal TtcMad, decimal TtcDevise, decimal RasIs, decimal RasTva, decimal NetAPayer, decimal MtRegle,
    decimal EncoursMad, bool FactRegle, string? StatutFacture, string? CommentaireFacture,
    IReadOnlyList<FactureLineDto> Lignes, IReadOnlyList<FactureTvaDto> Tva);

public sealed record CreateFactureLineRequest(string? CodeRubrique, string? SocRubrique, string? LibelleRubrique,
    NatureLigne NatureLigne, decimal MtTotalDevise, decimal TauxRemise, decimal TauxChange, decimal TauxTva, string? CodeTva);

public sealed record CreateFactureRequest
{
    public required string KeyClient { get; init; }
    public string? IntituleClient { get; init; }
    public string? KeyDossier { get; init; }
    public string? KeyVoyageInt { get; init; }
    public string? NatureService { get; init; }
    public string? TypeOperation { get; init; }
    public string? AgFact { get; init; }
    public string? Langue { get; init; }
    public required string DeviseFacture { get; init; }
    public decimal TauxDeviseFacture { get; init; } = 1m;
    public DateTime? DateFacture { get; init; }
    public DateTime? DateEcheance { get; init; }
    public string? CommentaireFacture { get; init; }
    public string? NumFacture { get; init; }
    public required IReadOnlyList<CreateFactureLineRequest> Lignes { get; init; }
}

public interface IFactureVenteService
{
    Task<PagedResult<FactureListItem>> SearchAsync(string? keyClient, TypeFacture? type, string? statut,
        bool? factRegle, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken cancellationToken = default, string? search = null);
    Task<FactureDetail> GetAsync(string keyFacture, CancellationToken cancellationToken = default);
    Task<FactureDetail> CreateAsync(CreateFactureRequest request, CancellationToken cancellationToken = default);
    Task<FactureDetail> RecalculateAsync(string keyFacture, CancellationToken cancellationToken = default);
    Task<FactureDetail> CreateAvoirTotalAsync(string keyFacture, bool avoirTotalFactRegle, CancellationToken cancellationToken = default);
    Task<FactureDetail> ApplyExonerationAsync(string keyFacture, string? commentaire, CancellationToken cancellationToken = default);
}

/// <summary>
/// Sales invoicing use cases. The header is always derived from its lines — the API never accepts a
/// caller-supplied total, which removes the class of bug where the legacy screens left the header
/// buckets out of step with the detail.
/// </summary>
public sealed class FactureVenteService(
    IFactureVenteRepository factures,
    IReferenceDataRepository referenceData,
    IKeyGenerator keys,
    IAuditTrail audit,
    ITenantContext tenant) : IFactureVenteService
{
    private const string TraceCreation = "$CFVENT";
    private const string TraceAvoir = "$CAVENT";
    private const string TraceExoneration = "$FADOSS";

    public async Task<PagedResult<FactureListItem>> SearchAsync(string? keyClient, TypeFacture? type, string? statut,
        bool? factRegle, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken cancellationToken = default, string? search = null)
    {
        // Delegates to the shared rule instead of repeating the bound; this copy was inline, which is why a
        // search for the duplicated helper missed it.
        Paging.Valider(page, pageSize);

        var result = await factures.SearchAsync(keyClient, type, statut, factRegle, from, to, page, pageSize, cancellationToken, search);

        return new PagedResult<FactureListItem>(
            result.Items.Select(f => new FactureListItem(f.KeyFacture, f.NumFacture, f.TypeFacture, f.KeyClient,
                f.IntituleClient, f.DateFacture, f.DateEcheance, f.DeviseFacture, f.TtcDevise, f.NetAPayer,
                f.MtRegle, f.FactRegle, f.StatutFacture)).ToArray(),
            result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<FactureDetail> GetAsync(string keyFacture, CancellationToken cancellationToken = default) =>
        Map(await RequireAsync(keyFacture, cancellationToken));

    public async Task<FactureDetail> CreateAsync(CreateFactureRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Lignes.Count == 0)
        {
            throw new BusinessRuleException("facture.lignes", "Une facture doit comporter au moins une ligne.");
        }

        var devise = await referenceData.FindAsync<Devise>(request.DeviseFacture.ToUpperInvariant())
            ?? throw new EntityNotFoundException("Devise", request.DeviseFacture);

        var societe = await referenceData.FindAsync<Societe>(tenant.SocieteKey)
            ?? throw new EntityNotFoundException("Societe", tenant.SocieteKey);

        if (!string.IsNullOrWhiteSpace(request.NumFacture)
            && await factures.NumFactureExistsAsync(request.NumFacture, cancellationToken))
        {
            throw new DuplicateEntityException("FactureVente", "NUM_FACTURE", request.NumFacture);
        }

        var key = await keys.NextAsync("FACTURE", cancellationToken);
        var facture = new EnteteFactureVente(key, tenant.SocieteKey, request.NumFacture, TypeFacture.Facture,
            request.KeyClient, request.IntituleClient, devise.CodeDevise, request.DateFacture ?? DateTime.UtcNow);

        facture.SetEntete(request.AgFact ?? tenant.AgenceKey, request.Langue ?? "FR", request.TypeOperation,
            request.NatureService, request.KeyDossier, request.KeyVoyageInt, request.DateEcheance,
            request.CommentaireFacture, magasinDouane: null);

        foreach (var line in request.Lignes)
        {
            var lineKey = await keys.NextAsync("DETAIL_FACTURE_VENTE", cancellationToken);
            var entity = new DetailFactureVente(lineKey, key, line.LibelleRubrique, line.NatureLigne,
                line.MtTotalDevise, line.TauxRemise, line.TauxChange, line.TauxTva);

            facture.AddLigne(entity);
        }

        // The VAT recap is always one row per rate, including zero rows, so the printed table lists
        // every configured rate — matching the legacy behaviour.
        facture.SetTvaRecap(BuildTvaRecap(facture, devise.CodeDevise, tenant.SocieteKey));

        var tauxRasIs = (decimal)(societe.TauxRasIs ?? 0d);
        var tauxRasTva = (decimal)(societe.TauxRasTva ?? 0d);
        facture.Recalculate(devise, request.TauxDeviseFacture <= 0 ? 1m : request.TauxDeviseFacture, tauxRasIs, tauxRasTva);

        factures.Add(facture);
        await factures.SaveChangesAsync(cancellationToken);

        await audit.TraceAsync<TraceFactureVente>(key, TraceCreation,
            $"Facture {facture.NumFacture}, client={request.KeyClient}, TTC={facture.TtcDevise}",
            valeur: (double)facture.TtcMad, cancellationToken: cancellationToken);

        return Map(facture);
    }

    public async Task<FactureDetail> RecalculateAsync(string keyFacture, CancellationToken cancellationToken = default)
    {
        var facture = await RequireAsync(keyFacture, cancellationToken);
        var devise = await referenceData.FindAsync<Devise>(facture.DeviseFacture)
            ?? throw new EntityNotFoundException("Devise", facture.DeviseFacture);
        var societe = await referenceData.FindAsync<Societe>(tenant.SocieteKey)
            ?? throw new EntityNotFoundException("Societe", tenant.SocieteKey);

        facture.SetTvaRecap(BuildTvaRecap(facture, devise.CodeDevise, tenant.SocieteKey));
        facture.Recalculate(devise, facture.TauxDeviseFacture,
            (decimal)(societe.TauxRasIs ?? 0d), (decimal)(societe.TauxRasTva ?? 0d));

        await factures.SaveChangesAsync(cancellationToken);
        return Map(facture);
    }

    public async Task<FactureDetail> CreateAvoirTotalAsync(
        string keyFacture, bool avoirTotalFactRegle, CancellationToken cancellationToken = default)
    {
        var source = await RequireAsync(keyFacture, cancellationToken);

        if (source.TypeFacture != TypeFacture.Facture)
        {
            throw new BusinessRuleException("avoir.source", "Un avoir total ne peut être généré que depuis une facture.");
        }

        var avoirKey = await keys.NextAsync("AVOIR", cancellationToken);
        var numAvoir = await keys.NextAsync("NUM_AVOIR", cancellationToken);

        var avoir = source.CreateAvoirTotal(avoirKey, numAvoir, avoirTotalFactRegle);

        factures.Add(avoir);
        await factures.SaveChangesAsync(cancellationToken);

        await audit.TraceAsync<TraceFactureVente>(avoirKey, TraceAvoir,
            $"Avoir total de la facture {source.NumFacture}", valeur: (double)avoir.TtcMad,
            cancellationToken: cancellationToken);

        return Map(avoir);
    }

    public async Task<FactureDetail> ApplyExonerationAsync(
        string keyFacture, string? commentaire, CancellationToken cancellationToken = default)
    {
        var facture = await RequireAsync(keyFacture, cancellationToken);

        facture.ApplyExoneration(commentaire);

        // The legacy code discards the exemption when no line was actually affected.
        if (!facture.Lignes.Any(l => l.IsExonerationEffective) && !string.IsNullOrWhiteSpace(commentaire))
        {
            throw new BusinessRuleException("exoneration.ineffective",
                "Aucune ligne de la facture n'est concernée par cette exonération.");
        }

        await factures.SaveChangesAsync(cancellationToken);
        await audit.TraceAsync<TraceFactureVente>(keyFacture, TraceExoneration,
            $"Exonération : {commentaire}", cancellationToken: cancellationToken);

        return Map(facture);
    }

    /// <summary>
    /// Builds the VAT recap by grouping the lines on their VAT code. Zero rows are added for every
    /// configured sales rate that has no amount, which is what the legacy printed VAT table expects.
    /// </summary>
    private static IReadOnlyList<TvaFactureVente> BuildTvaRecap(EnteteFactureVente facture, string devise, string societeKey)
    {
        var grouped = facture.Lignes
            .GroupBy(l => l.CodeTva ?? "0%")
            .Select(g => new TvaFactureVente(
                facture.KeyFacture,
                g.Key,
                societeKey,
                g.Sum(l => l.MtHtMad),
                g.Sum(l => Math.Round(l.MtHtMad * l.TauxTva, 2, MidpointRounding.AwayFromZero))))
            .ToList();

        return grouped;
    }

    private async Task<EnteteFactureVente> RequireAsync(string keyFacture, CancellationToken cancellationToken) =>
        await factures.GetAsync(keyFacture, true, cancellationToken)
        ?? throw new EntityNotFoundException("FactureVente", keyFacture);

    private static FactureDetail Map(EnteteFactureVente f) => new(
        f.KeyFacture, f.SocieteKey, f.NumFacture, f.TypeFacture, f.KeyClient, f.IntituleClient, f.KeyDossier,
        f.KeyVoyageInt, f.NatureService, f.DateFacture, f.DateEcheance, f.MoisFacture, f.DeviseFacture,
        f.TauxDeviseFacture, f.DeboursMad, f.AssigneMad, f.HtMad, f.TvaMad, f.TaxeRegionale, f.TotalHtMad,
        f.TtcMad, f.TtcDevise, f.RasIs, f.RasTva, f.NetAPayer, f.MtRegle, f.EncoursMad, f.FactRegle,
        f.StatutFacture, f.CommentaireFacture,
        f.Lignes.Select(l => new FactureLineDto(l.KeyLigne, l.CodeRubrique, l.LibelleRubrique, l.NatureLigne,
            l.MtTotalDevise, l.TauxRemise, l.MtDevise, l.TauxChange, l.MtHtMad, l.TauxTva, l.CodeTva, l.Imprimable)).ToArray(),
        f.TvaDetail.Select(t => new FactureTvaDto(t.CodeTva, t.Ht, t.Tva, t.MtImpute)).ToArray());
}
