using Logistics.Application.Abstractions;
using Logistics.Application.Features.Quotations;
using Logistics.Domain.Entities.Quotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Logistics.Api.Controllers;

/// <summary>
/// Purchase tariff scales ("barèmes achat") — the migration of
/// <c>frm_Codification_Tarif_Achat.vb</c>, <c>frm_Liste_Tarif_Achat.vb</c> and
/// <c>frm_Duplication_Tarif_Achat.vb</c>.
/// </summary>
/// <remarks>
/// <para>
/// A scale is a grid: columns are value <em>tranches</em> (with an optional formula) and each cell is a
/// purchase amount for one department. Pricing a weight and a destination means finding the tranche that
/// contains the weight and reading its cell.
/// </para>
/// <para>
/// The lifecycle is one-way. A scale is created <c>Non Validé</c> and becomes <c>Validé</c> through
/// validation; <b>only a validated scale may be modified or duplicated</b>. Nothing returns it to draft,
/// so a price already quoted to a customer cannot be edited underneath it. Deletion never removes rows:
/// every line is archived into the <c>hist</c> tables and the live status becomes <c>Supprimé</c>.
/// </para>
/// </remarks>
[ApiController]
[Route("api/v1/baremes-achat")]
[Tags("Quotations")]
public sealed class BaremesAchatController(IBaremeAchatService baremes) : ControllerBase
{
    /// <summary>Searches scales. <c>modifiables=true</c> returns only those that may be edited.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<BaremeAchatSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<BaremeAchatSummaryDto>>> Search(
        [FromQuery] BaremeAchatSearchRequest request, CancellationToken cancellationToken) =>
        Ok(await baremes.SearchAsync(request, cancellationToken));

    /// <summary>Full scale with its columns, cells and prestations.</summary>
    [HttpGet("{keyBareme}")]
    [ProducesResponseType<BaremeAchatDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BaremeAchatDto>> Get(string keyBareme, CancellationToken cancellationToken) =>
        Ok(await baremes.GetAsync(keyBareme, cancellationToken));

    /// <summary>Creates a scale in the <c>Non Validé</c> state.</summary>
    [HttpPost]
    [ProducesResponseType<BaremeAchatDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BaremeAchatDto>> Create(CreateBaremeAchatRequest request, CancellationToken cancellationToken)
    {
        var created = await baremes.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { keyBareme = created.KeyBareme }, created);
    }

    /// <summary>Updates a validated scale. Refused with 422 while it is not validated.</summary>
    [HttpPut("{keyBareme}")]
    [ProducesResponseType<BaremeAchatDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BaremeAchatDto>> Update(
        string keyBareme, CreateBaremeAchatRequest request, CancellationToken cancellationToken) =>
        Ok(await baremes.UpdateAsync(keyBareme, request, cancellationToken));

    /// <summary>
    /// Validates the scale (<c>Non Validé → Validé</c>).
    /// </summary>
    /// <remarks>
    /// Requires at least one column: without a tranche there is nothing to resolve and every lookup would
    /// silently return no price.
    /// </remarks>
    [HttpPost("{keyBareme}/valider")]
    [ProducesResponseType<BaremeAchatDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BaremeAchatDto>> Valider(string keyBareme, CancellationToken cancellationToken) =>
        Ok(await baremes.ValiderAsync(keyBareme, cancellationToken));

    /// <summary>
    /// Duplicates a validated scale. The source is archived first and the copy starts unvalidated.
    /// </summary>
    [HttpPost("{keyBareme}/dupliquer")]
    [ProducesResponseType<BaremeAchatDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BaremeAchatDto>> Duplicate(
        string keyBareme, DuplicateBaremeAchatRequest request, CancellationToken cancellationToken)
    {
        var copy = await baremes.DuplicateAsync(keyBareme, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { keyBareme = copy.KeyBareme }, copy);
    }

    /// <summary>
    /// Deletes the scale by archiving every line and marking it <c>Supprimé</c>.
    /// </summary>
    /// <remarks>
    /// No row is physically removed — the live tables would otherwise lose the price history that
    /// <c>t_hist_*</c> exists to preserve.
    /// <para>
    /// Requires the parameterisation role: a tariff scale is pricing configuration, the surface the legacy
    /// <c>admin</c> flag gated.
    /// </para>
    /// </remarks>
    [HttpDelete("{keyBareme}")]
    [Authorize(Policy = Authentication.ApiAuthentication.PolitiqueAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string keyBareme, CancellationToken cancellationToken)
    {
        await baremes.DeleteAsync(keyBareme, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Replaces the columns, dropping the cells of any column that is removed.
    /// </summary>
    [HttpPut("{keyBareme}/colonnes")]
    [ProducesResponseType<BaremeAchatDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BaremeAchatDto>> SetColonnes(
        string keyBareme, IReadOnlyList<ColonneBaremeAchatInput> colonnes, CancellationToken cancellationToken) =>
        Ok(await baremes.SetColonnesAsync(keyBareme, colonnes, cancellationToken));

    /// <summary>
    /// Replaces every cell of the grid. A cell naming a column that does not exist is refused.
    /// </summary>
    [HttpPut("{keyBareme}/details")]
    [ProducesResponseType<BaremeAchatDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BaremeAchatDto>> SetDetails(
        string keyBareme, IReadOnlyList<DetailBaremeAchatInput> details, CancellationToken cancellationToken) =>
        Ok(await baremes.SetDetailsAsync(keyBareme, details, cancellationToken));

    /// <summary>Replaces the purchase-prestation lines attached to the scale.</summary>
    [HttpPut("{keyBareme}/prestations")]
    [ProducesResponseType<BaremeAchatDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BaremeAchatDto>> SetPrestations(
        string keyBareme, IReadOnlyList<PrestationBaremeAchatInput> prestations, CancellationToken cancellationToken) =>
        Ok(await baremes.SetPrestationsAsync(keyBareme, prestations, cancellationToken));

    /// <summary>
    /// Prices a value for a department against the scale's tranches.
    /// </summary>
    /// <remarks>
    /// Returns <c>trouve=false</c> rather than a 404 when no tranche matches — "no price for that weight
    /// and destination" is a normal commercial answer, not a failed request. Tranches are half-open on
    /// the upper bound so adjacent ranges such as 0–100 and 100–500 do not both match 100.
    /// </remarks>
    [HttpGet("{keyBareme}/tarif")]
    [ProducesResponseType<TarifBaremeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TarifBaremeDto>> Resoudre(
        string keyBareme, [FromQuery] string departement, [FromQuery] decimal valeur, CancellationToken cancellationToken) =>
        Ok(await baremes.ResoudreAsync(keyBareme, departement, valeur, cancellationToken));
}
