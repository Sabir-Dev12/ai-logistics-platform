using Logistics.Application.Abstractions;
using Logistics.Application.Features.Resources;
using Logistics.Domain.Entities.Resources;
using Microsoft.AspNetCore.Mvc;

namespace Logistics.Api.Controllers;

/// <summary>
/// Carburant / gasoil — the port of <c>frm_Codification_Citerne_Gesoil.vb</c>,
/// <c>frm_Saisie_Reception_Gasoil.vb</c>, <c>frm_Saisie_Sortie_Gasoil.vb</c> and
/// <c>frm_saisie_jauge_gasoil.vb</c>.
/// </summary>
/// <remarks>
/// <para>
/// Every issue is valued at the tank's <b>weighted-average price</b> (<c>pmp</c>), which is the price the
/// fuel actually cost. <c>t_prix_gasoil</c> holds a reference market price and is deliberately not used
/// for valuation — confusing the two misprices all fuel consumption.
/// </para>
/// <para>
/// The average is re-weighted on every reception by stock, so a small expensive delivery barely moves it.
/// Each issue snapshots the price it was valued at, so a later delivery cannot restate history.
/// </para>
/// </remarks>
[ApiController]
[Route("api/v1/carburant")]
[Tags("Resources")]
public sealed class CarburantController(ICarburantService carburant) : ControllerBase
{
    /// <summary>Lists tanks, optionally filtered by fuel type.</summary>
    [HttpGet("citernes")]
    [ProducesResponseType<PagedResult<CiterneDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<CiterneDto>>> SearchCiternes(
        [FromQuery] string? recherche, [FromQuery] TypeCiterne? type,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default) =>
        Ok(await carburant.SearchCiternesAsync(recherche, type, page, pageSize, cancellationToken));

    /// <summary>A tank with its current stock, average price and stock value.</summary>
    [HttpGet("citernes/{keyCiterne}")]
    [ProducesResponseType<CiterneDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CiterneDto>> GetCiterne(string keyCiterne, CancellationToken cancellationToken) =>
        Ok(await carburant.GetCiterneAsync(keyCiterne, cancellationToken));

    /// <summary>Creates a tank.</summary>
    /// <remarks>
    /// Tanks are segregated by product — the sortie screens hard-filter on the type, so issuing AdBlue from
    /// a Gasoil tank is a data error rather than a rounding difference.
    /// </remarks>
    [HttpPost("citernes")]
    [ProducesResponseType<CiterneDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CiterneDto>> CreateCiterne(CreateCiterneRequest request, CancellationToken cancellationToken)
    {
        var created = await carburant.CreateCiterneAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetCiterne), new { keyCiterne = created.KeyCiterne }, created);
    }

    /// <summary>
    /// Receives a delivery, re-weighting the tank's average price and raising its stock.
    /// </summary>
    /// <remarks>
    /// <c>pmp = ((stock × pmp) + montant) / (stock + quantite)</c>. Receiving into an empty tank therefore
    /// sets the price to the delivered unit price.
    /// </remarks>
    [HttpPost("citernes/{keyCiterne}/reception")]
    [ProducesResponseType<CiterneDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CiterneDto>> Receptionner(
        string keyCiterne, ReceptionCiterneRequest request, CancellationToken cancellationToken) =>
        Ok(await carburant.ReceptionnerAsync(keyCiterne, request, cancellationToken));

    /// <summary>
    /// Records a fuel-tank delivery note and applies its cost to each tank.
    /// </summary>
    /// <remarks>
    /// The delivery note carries the BL's <b>total</b> amount and nothing else cost-wise — there is no unit
    /// price anywhere in the schema. Each tank's share is allocated pro rata to the quantity it received, and
    /// that share drives its average price:
    /// <c>pmp = ((stock × pmp) + share) / (stock + quantite)</c>.
    /// <para>
    /// <b>There is no endpoint to cancel a delivery.</b> That is not an omission: the legacy application has
    /// no delete path either, and reversal is impossible from this schema because the tank's previous
    /// quantity and average price are not retained. The response's <c>annulable</c> flag is always false so a
    /// caller finds the answer rather than assuming it was forgotten.
    /// </para>
    /// </remarks>
    [HttpPost("approvisionnements")]
    [ProducesResponseType<ApprovisionnementDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApprovisionnementDto>> Approvisionner(
        CreateApprovisionnementRequest request, CancellationToken cancellationToken)
    {
        var appro = await carburant.ApprovisionnerAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetApprovisionnement), new { keyApprovisionnement = appro.KeyApprovisionnement }, appro);
    }

    /// <summary>One delivery note, with its per-tank allocations re-derived.</summary>
    [HttpGet("approvisionnements/{keyApprovisionnement}")]
    [ProducesResponseType<ApprovisionnementDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApprovisionnementDto>> GetApprovisionnement(
        string keyApprovisionnement, CancellationToken cancellationToken) =>
        Ok(await carburant.GetApprovisionnementAsync(keyApprovisionnement, cancellationToken));

    /// <summary>Delivery notes for the current company.</summary>
    [HttpGet("approvisionnements")]
    [ProducesResponseType<PagedResult<ApprovisionnementDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ApprovisionnementDto>>> SearchApprovisionnements(
        [FromQuery] string? keyFournisseur, [FromQuery] DateTime? du, [FromQuery] DateTime? au,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default) =>
        Ok(await carburant.SearchApprovisionnementsAsync(keyFournisseur, du, au, page, pageSize, cancellationToken));

    /// <summary>
    /// Records a supplier fuel voucher and writes its journal row.
    /// </summary>
    /// <remarks>
    /// A voucher is <b>purchase</b> fuel, so unlike an issue it names a supplier and <b>no tank</b> and
    /// changes no stock — the fuel enters a tank later, when a replenishment records where it went. What the
    /// voucher does write, as the legacy does, is the pair: one key as <c>key_bon</c> and
    /// <c>key_consommation</c>, so the document and its consolidated journal row can be rejoined.
    /// <para>
    /// The journal's unit price is derived from the amount and quantity, but the <b>amount is taken as
    /// given</b> rather than recomputed: it is what the supplier billed, and re-deriving it from a rounded
    /// unit price would move it away from the document.
    /// </para>
    /// </remarks>
    [HttpPost("bons")]
    [ProducesResponseType<BonGasoilDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BonGasoilDto>> CreerBon(
        CreateBonGasoilRequest request, CancellationToken cancellationToken)
    {
        var bon = await carburant.CreerBonAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetBon), new { keyBon = bon.KeyBon }, bon);
    }

    /// <summary>One fuel voucher.</summary>
    [HttpGet("bons/{keyBon}")]
    [ProducesResponseType<BonGasoilDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BonGasoilDto>> GetBon(string keyBon, CancellationToken cancellationToken) =>
        Ok(await carburant.GetBonAsync(keyBon, cancellationToken));

    /// <summary>Fuel vouchers for the current company.</summary>
    [HttpGet("bons")]
    [ProducesResponseType<PagedResult<BonGasoilDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<BonGasoilDto>>> SearchBons(
        [FromQuery] string? keyFournisseur, [FromQuery] DateTime? du, [FromQuery] DateTime? au,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default) =>
        Ok(await carburant.SearchBonsAsync(keyFournisseur, du, au, page, pageSize, cancellationToken));

    /// <summary>
    /// Measures a vehicle's cost per kilometre over its next full-tank window.
    /// </summary>
    /// <remarks>
    /// The full-tank-to-full-tank method: between two full tanks you know the litres put in and what they
    /// cost, and the odometer gives the distance. Returns <b>204</b> when no window can be closed yet — which
    /// is the normal answer while a vehicle has fewer than two full tanks recorded, not an error.
    /// <para>
    /// The closing fill-up's fuel counts in this window but the row is left unmarked, so it opens the next
    /// one. Windows therefore chain; marking it would end the chain after a single measurement.
    /// </para>
    /// </remarks>
    [HttpPost("ratios/{keyMateriel}")]
    [ProducesResponseType<CoutKilometrageGasoilDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CoutKilometrageGasoilDto>> CalculerRatio(
        string keyMateriel, [FromQuery] string? produit, CancellationToken cancellationToken)
    {
        var ratio = await carburant.CalculerRatioAsync(keyMateriel, produit, cancellationToken);

        return ratio is null ? NoContent() : Ok(ratio);
    }

    /// <summary>The measurements a vehicle already has, most recent first.</summary>
    [HttpGet("ratios/{keyMateriel}")]
    [ProducesResponseType<IReadOnlyList<CoutKilometrageGasoilDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CoutKilometrageGasoilDto>>> GetRatios(
        string keyMateriel, CancellationToken cancellationToken) =>
        Ok(await carburant.GetRatiosAsync(keyMateriel, cancellationToken));

    /// <summary>
    /// Issues fuel from a tank, valuing it at the current average price.
    /// </summary>
    /// <remarks>
    /// Refused with 422 when the tank holds too little — a tank cannot hold a negative volume. The issue
    /// stores the price it was valued at, so later deliveries cannot restate it.
    /// </remarks>
    [HttpPost("citernes/{keyCiterne}/sorties")]
    [ProducesResponseType<SortieGasoilDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SortieGasoilDto>> Sortir(
        string keyCiterne, SortieCiterneRequest request, CancellationToken cancellationToken)
    {
        var sortie = await carburant.SortirAsync(keyCiterne, request, cancellationToken);
        return CreatedAtAction(nameof(GetCiterne), new { keyCiterne }, sortie);
    }

    /// <summary>
    /// Cancels an issue: soft-deletes the movement and restores the tank.
    /// </summary>
    /// <remarks>
    /// The movement is not removed — the audit trail of what was issued has to survive the correction.
    /// </remarks>
    [HttpDelete("sorties/{keySortie}")]
    [ProducesResponseType<SortieGasoilDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SortieGasoilDto>> AnnulerSortie(string keySortie, CancellationToken cancellationToken) =>
        Ok(await carburant.AnnulerSortieAsync(keySortie, cancellationToken));

    /// <summary>Lists fuel issues, filtered by tank and date range.</summary>
    [HttpGet("sorties")]
    [ProducesResponseType<PagedResult<SortieGasoilDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<SortieGasoilDto>>> SearchSorties(
        [FromQuery] string? keyCiterne, [FromQuery] DateTime? du, [FromQuery] DateTime? au,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default) =>
        Ok(await carburant.SearchSortiesAsync(keyCiterne, du, au, page, pageSize, cancellationToken));

    /// <summary>
    /// Records a physical dip and returns the gap against the theoretical stock.
    /// </summary>
    /// <remarks>
    /// A positive <c>ecart</c> means the tank holds less than the system believed. The theoretical figure
    /// is snapshotted onto the dip, and the stock itself is <b>not</b> changed — a dip observes; the stock
    /// correction screens adjust.
    /// </remarks>
    [HttpPost("citernes/{keyCiterne}/jauge")]
    [ProducesResponseType<JaugeGasoilDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<JaugeGasoilDto>> Jauger(
        string keyCiterne, JaugeCiterneRequest request, CancellationToken cancellationToken)
    {
        var jauge = await carburant.JaugerAsync(keyCiterne, request, cancellationToken);
        return CreatedAtAction(nameof(GetCiterne), new { keyCiterne }, jauge);
    }
}
