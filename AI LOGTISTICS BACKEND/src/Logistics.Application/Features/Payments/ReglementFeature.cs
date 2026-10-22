using Logistics.Application.Abstractions;
using Logistics.Domain.Common;
using Logistics.Domain.Entities.Audit;
using Logistics.Domain.Entities.Invoicing;
using Logistics.Domain.Entities.Payments;
using Logistics.Domain.Entities.ReferenceData;
using Logistics.Domain.Enums;

namespace Logistics.Application.Features.Payments;

// ---------------------------------------------------------------------------------------------
// DTOs
// ---------------------------------------------------------------------------------------------
public sealed record ReglementListItem(
    string KeyReglement, string? KeyTiers, TypeReglement TypeReglement, NatureReglement NatureReglement,
    string? ModePaiement, string? Devise, decimal MontantDevise, decimal MontantMad,
    StatutReglement Statut, SensFinancier Sens, DateTime? DateOperation, DateTime? DateEcheance,
    string? Facture, string? ReferencePaiement);

public sealed record AllocationDto(string KeyFacture, decimal Montant, string Devise, SensReglement Sens,
    decimal RasIs, decimal RasTva);

public sealed record ReglementDetail(
    string KeyReglement, string SocieteKey, string? AgenceReglement, string? KeyTiers, TypeReglement TypeReglement,
    NatureReglement NatureReglement, string? ModePaiement, string? ModeChange, string Devise, decimal TauxDevise,
    decimal MontantDevise, decimal MontantMad, string? MontantLettre, decimal MontantImputeAvance,
    decimal ResteAvance, DateTime? DateSaisie, DateTime? DateOperation, DateTime? DateEcheance,
    DateTime? DateRemiseBanque, DateTime? DateValidationBanque, string? KeyTresorie, StatutReglement Statut,
    SensFinancier Sens, string? MotifImpaye, string? Observation, string? Facture,
    decimal? FraisBancaire, decimal? Tva, decimal? ComissionBanque,
    decimal? EcartReglement, bool ValidationLettrage, bool IsLettrableAvance,
    IReadOnlyList<AllocationDto> Allocations)
{
    public string? ReferencePaiement { get; init; }
    public string? KeyRemiseSys { get; init; }
    public string? RefCompta { get; init; }
}

public sealed record CreateReglementRequest
{
    public required TypeReglement TypeReglement { get; init; }
    public string? KeyTiers { get; init; }
    public required string Devise { get; init; }
    public required decimal MontantDevise { get; init; }
    public decimal TauxDevise { get; init; } = 1m;
    public string? ModePaiement { get; init; }
    public string? ModeChange { get; init; }
    public string? AgenceReglement { get; init; }
    public string? KeyTresorie { get; init; }
    public string? ReferencePaiement { get; init; }
    public string? Observation { get; init; }
    public string? Dossier { get; init; }
    public DateTime? DateOperation { get; init; }
    public DateTime? DateEcheance { get; init; }
    public bool IsAvance { get; init; }
    public decimal? FraisBancaire { get; init; }
    public decimal? TvaFraisBancaire { get; init; }
    public decimal? ComissionBanque { get; init; }
}

public sealed record AllocateRequest(string KeyFacture, decimal Montant, SensReglement Sens,
    decimal RasIsGlobal = 0m, decimal NetAPayerGlobal = 0m);

public sealed record AllocateResult(decimal Allocated, decimal MontantMad, decimal Ecart,
    EcartDecision EcartDecision, string? OdKeyFacture);

public interface IReglementService
{
    Task<PagedResult<ReglementListItem>> SearchAsync(
        TypeReglement? type, string? keyTiers, StatutReglement? statut, DateTime? from, DateTime? to,
        int page, int pageSize, CancellationToken cancellationToken = default, string? search = null, string? tiers = null);
    Task<ReglementDetail> GetAsync(string keyReglement, CancellationToken cancellationToken = default);
    Task<ReglementDetail> CreateAsync(CreateReglementRequest request, CancellationToken cancellationToken = default);
    Task<AllocateResult> AllocateAsync(string keyReglement, AllocateRequest request, CancellationToken cancellationToken = default);
    Task<ReglementDetail> DeallocateAsync(string keyReglement, string keyFacture, CancellationToken cancellationToken = default);
    Task<ReglementDetail> RemitAsync(string keyReglement, string keyTresorie, string? refCompta, DateTime dateRemise, CancellationToken cancellationToken = default);
    Task<ReglementDetail> MarkImpayeAsync(string keyReglement, string motifImpaye, string? commentaire, CancellationToken cancellationToken = default);
}

/// <summary>
/// Payment use cases: création, lettrage (allocation) and the currency-difference decision.
/// </summary>
/// <remarks>
/// This is where the legacy behaviour was hardest to follow, and the parts that are easy to get wrong
/// are isolated deliberately:
/// <list type="bullet">
///   <item>the exchange rate is forced to 1 for the company's own currency <em>before</em> MAD is computed;</item>
///   <item>allocation amounts are stored positive, with the direction carried by <c>sens</c>;</item>
///   <item>a bank fee produces both a header snapshot and a real <c>T_FRAIS_BANCAIRES</c> row;</item>
///   <item>the conversion difference is classified against the two company thresholds and, when small
///   enough, produces an automatic <c>type_facture='O'</c> counter-invoice keyed on the payment.</item>
/// </list>
/// </remarks>
public sealed class ReglementService(
    IReglementRepository reglements,
    IFactureVenteRepository factures,
    IReferenceDataRepository referenceData,
    IKeyGenerator keys,
    IAuditTrail audit,
    ITenantContext tenant) : IReglementService
{
    private const string TraceAjout = "$AREG";
    private const string TraceLettrage = "$ARGL";
    private const string TraceImpaye = "$VIREG";
    private const string TraceRemise = "$EODV";

    /// <summary>Legacy defaults for the automatic / manual "opération diverse" thresholds.</summary>
    private const decimal DefaultOdAutomatique = 1m;
    private const decimal DefaultOdManuel = 100m;

    public async Task<PagedResult<ReglementListItem>> SearchAsync(
        TypeReglement? type, string? keyTiers, StatutReglement? statut, DateTime? from, DateTime? to,
        int page, int pageSize, CancellationToken cancellationToken = default, string? search = null, string? tiers = null)
    {
        // Delegates to the shared rule instead of repeating the bound; this copy was inline, which is why a
        // search for the duplicated helper missed it.
        Paging.Valider(page, pageSize);

        var result = await reglements.SearchAsync(type, keyTiers, statut, from, to, page, pageSize, cancellationToken, search, tiers);

        return new PagedResult<ReglementListItem>(
            result.Items.Select(r => new ReglementListItem(
                r.KeyReglement, r.KeyTiers, r.TypeReglement, r.NatureReglement, r.ModePaiement, r.Devise,
                r.MontantDevise, r.MontantMad, r.Statut, r.SensFinancier, r.DateOperation, r.DateEcheance,
                r.Facture, r.ReferencePaiement)).ToArray(),
            result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<ReglementDetail> GetAsync(string keyReglement, CancellationToken cancellationToken = default) =>
        Map(await RequireAsync(keyReglement, cancellationToken));

    public async Task<ReglementDetail> CreateAsync(CreateReglementRequest request, CancellationToken cancellationToken = default)
    {
        var societe = await referenceData.FindAsync<Societe>(tenant.SocieteKey)
            ?? throw new EntityNotFoundException("Societe", tenant.SocieteKey);

        var devise = await referenceData.FindAsync<Devise>(request.Devise.ToUpperInvariant())
            ?? throw new EntityNotFoundException("Devise", request.Devise);

        var mode = request.ModePaiement is null
            ? null
            : await referenceData.FindAsync<ModePaiement>(request.ModePaiement);

        var key = await keys.NextAsync("REGLEMENT", cancellationToken);
        var now = DateTime.UtcNow;

        var reglement = new EnteteReglement(key, tenant.SocieteKey, request.TypeReglement, request.KeyTiers,
            devise.CodeDevise, request.DateOperation ?? now);

        if (request.IsAvance)
        {
            reglement.MarkAsAvance();
        }

        reglement.SetAgence(request.AgenceReglement ?? tenant.AgenceKey);
        reglement.SetModeChange(request.ModeChange);
        reglement.SetModePaiement(request.ModePaiement);
        reglement.SetDates(now, request.DateOperation ?? now, request.DateEcheance);
        reglement.SetMontants(request.MontantDevise, request.TauxDevise, societe.DeviseSoc, devise.NbDecimal);
        reglement.ApplyDefaultStatut(null, mode);

        reglement.SetReferencePaiement(request.ReferencePaiement);
        reglement.SetObservation(request.Observation);
        reglement.SetTresorie(request.KeyTresorie);
        reglement.SetDossier(request.Dossier);

        // Bank fees are stored twice on purpose: a snapshot on the header AND a real fee row used by
        // the VAT declaration and the accounting integration.
        var fraisHt = request.FraisBancaire.GetValueOrDefault();
        var fraisTva = request.TvaFraisBancaire.GetValueOrDefault();
        if (fraisHt != 0m || fraisTva != 0m || request.ComissionBanque is not null)
        {
            reglement.SetFraisBancaires(request.FraisBancaire, request.TvaFraisBancaire, request.ComissionBanque);
        }

        reglements.Add(reglement);

        if (fraisHt != 0m || fraisTva != 0m)
        {
            var fraisKey = await keys.NextAsync("FRAIS_BANCAIRE", cancellationToken);
            var frais = new FraisBancaire(
                fraisKey, tenant.SocieteKey, FraisBancaire.NatureFraisBancaires,
                societe.DeviseSoc, fraisHt, fraisTva, tauxChange: 1m);

            frais.LinkReglement(reglement.KeyReglement);
            frais.Update(fraisHt, fraisTva, request.KeyTresorie, request.DateOperation ?? now);
            reglements.Add(frais);
        }

        await reglements.SaveChangesAsync(cancellationToken);
        await audit.TraceAsync<TraceReglement>(
            reglement.KeyReglement, TraceAjout,
            $"Montant={reglement.MontantDevise} {reglement.Devise}, Tiers={reglement.KeyTiers}",
            cancellationToken: cancellationToken);

        return Map(reglement);
    }

    public async Task<AllocateResult> AllocateAsync(
        string keyReglement, AllocateRequest request, CancellationToken cancellationToken = default)
    {
        var reglement = await RequireAsync(keyReglement, cancellationToken);
        if (reglement.Allocations.Any(a => a.KeyFacture == request.KeyFacture))
            throw new BusinessRuleException("reglement.allocation.exists", "Cette facture est déjà affectée à ce règlement.");
        var facture = await factures.GetAsync(request.KeyFacture, false, cancellationToken)
            ?? throw new EntityNotFoundException("FactureVente", request.KeyFacture);

        if (facture.FactRegle)
        {
            throw new BusinessRuleException("facture.already_settled", $"La facture '{request.KeyFacture}' est déjà réglée.");
        }

        if (!string.Equals(reglement.Devise, facture.DeviseReg, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(facture.DeviseReg, facture.DeviseFacture, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException(
                "reglement.devise",
                $"La devise du règlement ({reglement.Devise}) est incompatible avec celle de la facture ({facture.DeviseReg}).");
        }

        if (reglement.NatureReglement == NatureReglement.Avance && request.Montant > reglement.ResteAvance)
        {
            throw new BusinessRuleException(
                "avance.depassement",
                $"Le montant affecté ({request.Montant}) dépasse le reste de l'avance ({reglement.ResteAvance}).");
        }

        // Allocation amounts are stored positive; direction is carried by `sens`. RAS is prorated
        // afterwards, once the allocation exists and its share of the settlement is known.
        var allocation = reglement.Allocate(request.KeyFacture, request.Montant, request.Sens);

        if (request.RasIsGlobal > 0 && request.NetAPayerGlobal > 0)
        {
            allocation.ProrateRas(request.RasIsGlobal, request.NetAPayerGlobal);
        }

        // Invoice side effect: the purchase side is decremented, the sales side incremented.
        facture.RegisterPayment(request.Montant, negateForPurchase: request.Sens == SensReglement.Achat, DateTime.UtcNow);

        // Conversion difference is only meaningful for a foreign-currency settlement.
        string? odKey = null;
        var ecart = 0m;
        var decision = EcartDecision.Automatique;

        if (!string.Equals(reglement.Devise, facture.DeviseFacture, StringComparison.OrdinalIgnoreCase))
        {
            reglement.ComputeEcarts();
            ecart = reglement.EcartReglement ?? 0m;
            decision = reglement.ClassifyEcart(DefaultOdAutomatique, DefaultOdManuel);

            if (decision == EcartDecision.Refuse)
            {
                throw new BusinessRuleException(
                    "ecart.refuse",
                    $"L'écart de règlement ({ecart}) dépasse le seuil manuel autorisé et doit être analysé.");
            }

            if (decision == EcartDecision.Automatique && ecart != 0m)
            {
                odKey = reglement.KeyReglement;
                var od = EnteteFactureVente.CreateEcartOd(
                    odKey, tenant.SocieteKey, facture.KeyClient, facture.DeviseFacture, ecart, DateTime.UtcNow);
                factures.Add(od);
            }
        }

        await reglements.SaveChangesAsync(cancellationToken);
        await factures.SaveChangesAsync(cancellationToken);

        await audit.TraceAsync<TraceReglement>(
            reglement.KeyReglement, TraceLettrage,
            $"Affectation facture={request.KeyFacture}, montant={request.Montant}",
            valeur: (double)request.Montant, cancellationToken: cancellationToken);

        return new AllocateResult(request.Montant, reglement.MontantMad, ecart, decision, odKey);
    }

    public async Task<ReglementDetail> DeallocateAsync(
        string keyReglement, string keyFacture, CancellationToken cancellationToken = default)
    {
        var reglement = await RequireAsync(keyReglement, cancellationToken);
        var allocation = reglement.Allocations.FirstOrDefault(a => a.KeyFacture == keyFacture)
            ?? throw new EntityNotFoundException("DetailReglement", $"{keyReglement}:{keyFacture}");

        reglement.Deallocate(keyFacture);

        var facture = await factures.GetAsync(keyFacture, false, cancellationToken);
        facture?.ReversePayment(allocation.Montant, negateForPurchase: allocation.Sens == SensReglement.Achat);

        await reglements.SaveChangesAsync(cancellationToken);
        await factures.SaveChangesAsync(cancellationToken);

        return Map(reglement);
    }

    public async Task<ReglementDetail> RemitAsync(
        string keyReglement, string keyTresorie, string? refCompta, DateTime dateRemise,
        CancellationToken cancellationToken = default)
    {
        var reglement = await RequireAsync(keyReglement, cancellationToken);

        var remiseKey = await keys.NextAsync("REMISE", cancellationToken);
        reglement.SetTresorerieRemise(keyTresorie, dateRemise, remiseKey, refCompta);

        await reglements.SaveChangesAsync(cancellationToken);
        await audit.TraceAsync<TraceReglement>(
            keyReglement, TraceRemise,
            $"Remise en banque, trésorerie={keyTresorie}, remise={remiseKey}", cancellationToken: cancellationToken);

        return Map(reglement);
    }

    public async Task<ReglementDetail> MarkImpayeAsync(
        string keyReglement, string motifImpaye, string? commentaire, CancellationToken cancellationToken = default)
    {
        var reglement = await RequireAsync(keyReglement, cancellationToken);

        reglement.MarkImpaye(motifImpaye, commentaire);

        // The legacy screen propagates the VAT-declaration status to the linked bank fees.
        reglement.SetStatutDeclarationTva(StatutDeclarationTva.AttenteDeclaration, null);

        await reglements.SaveChangesAsync(cancellationToken);
        await audit.TraceAsync<TraceReglement>(
            keyReglement, TraceImpaye, $"Motif={motifImpaye} {commentaire}", cancellationToken: cancellationToken);

        return Map(reglement);
    }

    private async Task<EnteteReglement> RequireAsync(string keyReglement, CancellationToken cancellationToken) =>
        await reglements.GetAsync(keyReglement, true, cancellationToken)
        ?? throw new EntityNotFoundException("EnteteReglement", keyReglement);

    private static ReglementDetail Map(EnteteReglement r) => new(
        r.KeyReglement, r.SocieteKey, r.AgenceReglement, r.KeyTiers, r.TypeReglement, r.NatureReglement,
        r.ModePaiement, r.ModeChange, r.Devise, r.TauxDevise, r.MontantDevise, r.MontantMad, r.MontantLettre,
        r.MontantImputeAvance, r.ResteAvance, r.DateSaisie, r.DateOperation, r.DateEcheance, r.DateRemiseBanque,
        r.DateValidationBanque, r.KeyTresorie, r.Statut, r.SensFinancier, r.MotifImpaye, r.Observation, r.Facture,
        r.FraisBancaire, r.Tva, r.ComissionBanque, r.EcartReglement, r.ValidationLettrage, r.IsLettrableAvance,
        r.Allocations.Select(a => new AllocationDto(a.KeyFacture, a.Montant, a.Devise, a.Sens, a.RasIs, a.RasTva)).ToArray())
    { ReferencePaiement = r.ReferencePaiement, KeyRemiseSys = r.KeyRemiseSys, RefCompta = r.RefCompta };
}
