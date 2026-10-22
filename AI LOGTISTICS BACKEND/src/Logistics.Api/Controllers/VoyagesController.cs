using Logistics.Application.Abstractions;
using Logistics.Application.Features.Shipments;
using Logistics.Application.Features.Resources;
using Logistics.Domain.Entities.Shipments;
using Microsoft.AspNetCore.Mvc;

namespace Logistics.Api.Controllers;

/// <summary>
/// Voyages (trips) — the migration of <c>frm_Creation_Voyage.vb</c> and <c>frm_Liste_Voyages.vb</c>.
/// </summary>
/// <remarks>
/// The lifecycle is explicit rather than flag-based, because the whole costing module keys off
/// <c>date_voyage</c>: a voyage is <em>created</em>, then <em>validated</em> (which stamps that date),
/// then optionally <em>closed</em>. Closing is refused while an attached dossier is still uninvoiced.
/// </remarks>
[ApiController]
[Route("api/v1/voyages")]
[Tags("Voyages")]
public sealed class VoyagesController(
    IVoyageService voyages,
    IVoyageCostService costs,
    IVoyageCostRateProvider rates) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<VoyageListItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<VoyageListItem>>> Search(
        [FromQuery] string? search,
        [FromQuery] CategorieVoyage? categorie,
        [FromQuery] string? keyAgence,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] bool? validesSeulement,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default,
        [FromQuery] VoyageSearchStatus? statut = null) =>
        Ok(await voyages.SearchAsync(search, categorie, keyAgence, from, to, validesSeulement, page, pageSize, cancellationToken, statut));

    [HttpGet("{keyVoyage}")]
    [ProducesResponseType<VoyageDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VoyageDetail>> Get(string keyVoyage, CancellationToken cancellationToken) =>
        Ok(await voyages.GetAsync(keyVoyage, cancellationToken));

    [HttpPost]
    [ProducesResponseType<VoyageDetail>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<VoyageDetail>> Create(SaveVoyageRequest request, CancellationToken cancellationToken)
    {
        var created = await voyages.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { keyVoyage = created.KeyVoyage }, created);
    }

    [HttpPut("{keyVoyage}")]
    [ProducesResponseType<VoyageDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<VoyageDetail>> Update(
        string keyVoyage, SaveVoyageRequest request, CancellationToken cancellationToken) =>
        Ok(await voyages.UpdateAsync(keyVoyage, request, cancellationToken));

    /// <summary>
    /// Validates the voyage, stamping <c>date_voyage</c>. This is the gate the costing engine waits for;
    /// attempting to cost an unvalidated voyage returns 422.
    /// </summary>
    [HttpPost("{keyVoyage}/validate")]
    [ProducesResponseType<VoyageDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<VoyageDetail>> Validate(
        string keyVoyage, [FromQuery] DateTime? dateVoyage, CancellationToken cancellationToken) =>
        Ok(await voyages.ValidateAsync(keyVoyage, dateVoyage ?? DateTime.UtcNow, cancellationToken));

    /// <summary>Closes the voyage. Returns 422 while an attached dossier is still uninvoiced.</summary>
    [HttpPost("{keyVoyage}/close")]
    [ProducesResponseType<VoyageDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<VoyageDetail>> Close(
        string keyVoyage, [FromQuery] DateTime? dateCloture, CancellationToken cancellationToken) =>
        Ok(await voyages.CloseAsync(keyVoyage, dateCloture ?? DateTime.UtcNow, cancellationToken));

    [HttpPost("{keyVoyage}/reopen")]
    [ProducesResponseType<VoyageDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<VoyageDetail>> Reopen(string keyVoyage, CancellationToken cancellationToken) =>
        Ok(await voyages.ReopenAsync(keyVoyage, cancellationToken));

    [HttpDelete("{keyVoyage}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(string keyVoyage, CancellationToken cancellationToken)
    {
        await voyages.DeleteAsync(keyVoyage, cancellationToken);
        return NoContent();
    }

    [HttpPost("{keyVoyage}/trajets")]
    [ProducesResponseType<TrajetDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<TrajetDto>> AddTrajet(
        string keyVoyage, SaveTrajetRequest request, CancellationToken cancellationToken)
    {
        var created = await voyages.AddTrajetAsync(keyVoyage, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { keyVoyage }, created);
    }

    [HttpDelete("{keyVoyage}/trajets/{keyTrajet}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveTrajet(string keyVoyage, string keyTrajet, CancellationToken cancellationToken)
    {
        await voyages.RemoveTrajetAsync(keyVoyage, keyTrajet, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Adds equipment. Repeated calls for the same type/nature merge into a quantity, reproducing the
    /// legacy <c>qte = qte + 1</c> behaviour.
    /// </summary>
    [HttpPost("{keyVoyage}/equipements")]
    [ProducesResponseType<EquipementDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<EquipementDto>> AddEquipement(
        string keyVoyage,
        [FromQuery] string typeEquipement,
        [FromQuery] string? natureEquipement,
        CancellationToken cancellationToken)
    {
        var created = await voyages.AddEquipementAsync(keyVoyage, typeEquipement, natureEquipement, cancellationToken);
        return CreatedAtAction(nameof(Get), new { keyVoyage }, created);
    }

    /// <summary>Attaches a dossier. Also refreshes the voyage dossier count used as the cost divisor.</summary>
    [HttpPost("{keyVoyage}/dossiers/{keyDossier}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AssignDossier(
        string keyVoyage, string keyDossier, [FromQuery] int? position, CancellationToken cancellationToken)
    {
        await voyages.AssignDossierAsync(keyVoyage, keyDossier, position, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{keyVoyage}/dossiers/{keyDossier}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UnassignDossier(
        string keyVoyage, string keyDossier, CancellationToken cancellationToken)
    {
        await voyages.UnassignDossierAsync(keyVoyage, keyDossier, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Reports which cost operations have already been computed for the voyage.
    /// </summary>
    /// <remarks>
    /// Backed by <c>t_cout_operation_voyage_calcule</c>, which stores only <c>(key_voyage, operation)</c>
    /// and no amounts — the money lives in <c>T_REPARTITION_DOSSIER</c>. This is the idempotency guard
    /// that stops the generator double-costing a voyage.
    /// </remarks>
    [HttpGet("{keyVoyage}/operations-cout")]
    [ProducesResponseType<IReadOnlyList<CostOperationState>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CostOperationState>>> GetCostOperations(
        string keyVoyage, CancellationToken cancellationToken) =>
        Ok(await voyages.GetCostOperationsAsync(keyVoyage, cancellationToken));

    /// <summary>Marks a cost operation as computed. Refused (422) before the voyage is validated.</summary>
    [HttpPost("{keyVoyage}/operations-cout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> MarkCostOperation(
        string keyVoyage, [FromQuery] string operation, CancellationToken cancellationToken)
    {
        await voyages.MarkCostOperationAsync(keyVoyage, operation, cancellationToken);
        return NoContent();
    }

    /// <summary>Drops a cost marker so the operation will be recomputed.</summary>
    [HttpDelete("{keyVoyage}/operations-cout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ClearCostOperation(
        string keyVoyage, [FromQuery] string operation, CancellationToken cancellationToken)
    {
        await voyages.ClearCostOperationAsync(keyVoyage, operation, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Computes what the cost run <em>would</em> produce, without persisting anything.
    /// </summary>
    /// <remarks>
    /// Useful for reconciliation: the legacy screen could only run the generation and look at the
    /// result afterwards. Rates that live in not-yet-migrated masters are supplied in the body, so an
    /// unmigrated rate source is visible rather than silently costing the voyage at zero.
    /// </remarks>
    [HttpPost("{keyVoyage}/cout/apercu")]
    [ProducesResponseType<VoyageCostPreview>(StatusCodes.Status200OK)]
    public async Task<ActionResult<VoyageCostPreview>> PreviewCost(
        string keyVoyage, VoyageCostRequest request, CancellationToken cancellationToken) =>
        Ok(await costs.PreviewAsync(keyVoyage, request.MoisCalcul, request.ToRates(), cancellationToken));

    /// <summary>
    /// Runs the voyage cost generation: spreads each eligible operation equally over the voyage's
    /// dossiers into <c>T_REPARTITION_DOSSIER</c>, writes the per-leg kilometre detail and records the
    /// "already calculated" markers.
    /// </summary>
    /// <remarks>
    /// Refused with 422 when the voyage is not eligible (deleted, unvalidated, or dated outside the
    /// calculation month) or when it carries no dossier to allocate to.
    /// </remarks>
    /// <summary>
    /// Runs the voyage cost generation.
    /// </summary>
    /// <remarks>
    /// Rates are supplied in the body. Use <c>GET /{keyVoyage}/cout/taux</c> first to read what the
    /// database would resolve, then pass those values here (or override them) — that ordering keeps the
    /// apportionment explicit rather than implicit.
    /// </remarks>
    [HttpPost("{keyVoyage}/cout/generer")]
    [ProducesResponseType<VoyageCostPreview>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<VoyageCostPreview>> GenerateCost(
        string keyVoyage, VoyageCostRequest request, CancellationToken cancellationToken) =>
        Ok(await costs.GenerateAsync(keyVoyage, request.MoisCalcul, request.ToRates(), cancellationToken));

    /// <summary>
    /// Resolves the cost rates for a voyage <b>from the database</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the read-only counterpart to supplying rates by hand on the preview/generate calls, and it
    /// encodes a correction established during schema extraction: the masters hold a <b>monthly</b> cost
    /// (<c>t_materiel.cout</c>, <c>t_personnel.cout</c>) which is apportioned across the period's trajets.
    /// There is no per-day rate anywhere in the schema — only fuel is stored per unit of distance
    /// (<c>t_cout_kilometrage_gasoil.cout_km</c>, keyed by tractor + date window + product).
    /// </para>
    /// <para>
    /// The divisor actually applied is returned so the apportionment assumption is auditable, and
    /// <c>avertissements</c> lists every rate that could not be resolved — a missing rate is reported
    /// rather than defaulted to zero, because zero would cost the voyage at nothing and look like success.
    /// </para>
    /// <para>
    /// Only equipment types flagged <c>generation_cout</c> are costed.
    /// </para>
    /// </remarks>
    /// <param name="nbTrajetsDiviseur">Overrides the divisor applied to the monthly costs.</param>
    [HttpGet("{keyVoyage}/cout/taux")]
    [ProducesResponseType<VoyageCostRatesResolus>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VoyageCostRatesResolus>> ResolveCostRates(
        string keyVoyage, [FromQuery] int? nbTrajetsDiviseur, CancellationToken cancellationToken) =>
        Ok(await rates.ResolveAsync(keyVoyage, nbTrajetsDiviseur, cancellationToken));
}

/// <summary>
/// Cost-run input.
/// </summary>
/// <remarks>
/// Company and agency costs are read from <c>t_societe</c> / <c>t_agence</c> by the service. The
/// material and personnel rates are supplied here because <c>t_materiel</c> and <c>t_personnel</c> are
/// not part of this increment; see <c>VoyageCostRates</c>.
/// </remarks>
public sealed record VoyageCostRequest(
    string MoisCalcul,
    decimal CoutFraisBancaire = 0m,
    IReadOnlyList<VoyageCostLeg>? Legs = null,
    IReadOnlyList<VoyageCostEquipment>? Equipements = null,
    IReadOnlyList<VoyageCostPersonnel>? Personnel = null)
{
    public VoyageCostRates ToRates() => new()
    {
        CoutFraisBancaire = CoutFraisBancaire,
        Legs = Legs ?? [],
        Equipements = Equipements ?? [],
        Personnel = Personnel ?? []
    };
}

/// <summary>
/// Dossiers (shipment files) — the migration of <c>frm_Creation_Dossier.vb</c> and
/// <c>frm_Liste_Dossiers.vb</c>.
/// </summary>
[ApiController]
[Route("api/v1/dossiers")]
[Tags("Dossiers")]
public sealed class DossiersController(IDossierService dossiers) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<DossierListItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<DossierListItem>>> Search(
        [FromQuery] string? search,
        [FromQuery] string? keyClient,
        [FromQuery] string? keyVoyage,
        [FromQuery] string? typeDossier,
        [FromQuery] StatutFacturation? statutFacturation,
        [FromQuery] bool? meadSeulement,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await dossiers.SearchAsync(search, keyClient, keyVoyage, typeDossier, statutFacturation, meadSeulement,
            page, pageSize, cancellationToken));

    [HttpGet("{keyDossier}")]
    [ProducesResponseType<DossierDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DossierDetail>> Get(string keyDossier, CancellationToken cancellationToken) =>
        Ok(await dossiers.GetAsync(keyDossier, cancellationToken));

    [HttpGet("by-voyage/{keyVoyage}")]
    [ProducesResponseType<IReadOnlyList<DossierListItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DossierListItem>>> ListByVoyage(
        string keyVoyage, CancellationToken cancellationToken) =>
        Ok(await dossiers.ListByVoyageAsync(keyVoyage, cancellationToken));

    [HttpPost]
    [ProducesResponseType<DossierDetail>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DossierDetail>> Create(SaveDossierRequest request, CancellationToken cancellationToken)
    {
        var created = await dossiers.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { keyDossier = created.KeyDossier }, created);
    }

    [HttpPut("{keyDossier}")]
    [ProducesResponseType<DossierDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DossierDetail>> Update(
        string keyDossier, SaveDossierRequest request, CancellationToken cancellationToken) =>
        Ok(await dossiers.UpdateAsync(keyDossier, request, cancellationToken));

    /// <summary>Records unpacking. Only valid for a MEAD dossier (customs warehouse set).</summary>
    [HttpPost("{keyDossier}/depotage")]
    [ProducesResponseType<DossierDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DossierDetail>> Depotage(
        string keyDossier, DepotageRequest request, CancellationToken cancellationToken) =>
        Ok(await dossiers.DepotageAsync(keyDossier, request, cancellationToken));

    [HttpPost("{keyDossier}/depotage/annulation")]
    [ProducesResponseType<DossierDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DossierDetail>> CancelDepotage(string keyDossier, CancellationToken cancellationToken) =>
        Ok(await dossiers.CancelDepotageAsync(keyDossier, cancellationToken));

    [HttpPost("{keyDossier}/statut-facturation")]
    [ProducesResponseType<DossierDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DossierDetail>> SetStatutFacturation(
        string keyDossier, [FromQuery] StatutFacturation statut, CancellationToken cancellationToken) =>
        Ok(await dossiers.SetStatutFacturationAsync(keyDossier, statut, cancellationToken));

    /// <summary>Soft-deletes the dossier and detaches it from its voyage.</summary>
    [HttpDelete("{keyDossier}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(string keyDossier, CancellationToken cancellationToken)
    {
        await dossiers.DeleteAsync(keyDossier, cancellationToken);
        return NoContent();
    }

    [HttpPost("{keyDossier}/colisages")]
    [ProducesResponseType<ColisageDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ColisageDto>> AddColisage(
        string keyDossier, SaveColisageRequest request, CancellationToken cancellationToken)
    {
        var created = await dossiers.AddColisageAsync(keyDossier, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { keyDossier }, created);
    }

    [HttpDelete("{keyDossier}/colisages/{keyLigneColisage:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveColisage(
        string keyDossier, int keyLigneColisage, CancellationToken cancellationToken)
    {
        await dossiers.RemoveColisageAsync(keyDossier, keyLigneColisage, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Splits an amount across the voyage's dossiers pro rata to <c>poids_taxable</c>.
    /// </summary>
    /// <remarks>
    /// This is the rule the legacy <c>Repartition_Facture_vente</c> procedure applies when a sales line
    /// carries a voyage but no dossier. The competing internal-costing rule splits <em>equally</em> by
    /// <c>nb_doss_voyage_saisi</c> instead; both are implemented in <c>AllocationCalculator</c> and
    /// unit-tested, because picking the wrong one silently mis-allocates every cost.
    /// </remarks>
    [HttpGet("by-voyage/{keyVoyage}/repartition")]
    [ProducesResponseType<IReadOnlyList<RepartitionSplitResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RepartitionSplitResult>>> SplitByPoidsTaxable(
        string keyVoyage,
        [FromQuery] decimal montantMad,
        [FromQuery] decimal montantDevise,
        CancellationToken cancellationToken)
    {
        var split = await dossiers.SplitAmountByPoidsTaxableAsync(keyVoyage, montantMad, montantDevise, cancellationToken);

        return Ok(split.Select(s => new RepartitionSplitResult(s.KeyDossier, s.MontantMad, s.MontantDevise)).ToArray());
    }
}

public sealed record RepartitionSplitResult(string KeyDossier, decimal MontantMad, decimal MontantDevise);
