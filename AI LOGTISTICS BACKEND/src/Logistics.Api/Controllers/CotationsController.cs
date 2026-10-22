using Logistics.Application.Abstractions;
using Logistics.Application.Features.Quotations;
using Logistics.Domain.Entities.Quotations;
using Microsoft.AspNetCore.Mvc;

namespace Logistics.Api.Controllers;

/// <summary>
/// Quotations (cotations) — the migration of <c>frm_Saisie_Cotation.vb</c>,
/// <c>frm_Liste_Cotation.vb</c> and <c>frm_Cloture_Cotation.vb</c>.
/// </summary>
/// <remarks>
/// <para>
/// The quotation lifecycle is <c>Saisie → Envoyée → Gagnée|Perdue</c>, with <c>Annulée</c>,
/// <c>Contre Offre</c> and <c>Supprimée</c> as side states. Unlike every other module the status is a
/// free-text French literal, not a <c>t_statut</c> code, so the literals carry their accents and spaces
/// verbatim.
/// </para>
/// <para>
/// A quotation is the origin of a booking or dossier: only a <c>Gagnée</c> quotation still inside its
/// validity window is convertible, which the <c>convertibles</c> search filter exposes directly.
/// </para>
/// </remarks>
[ApiController]
[Route("api/v1/cotations")]
[Tags("Quotations")]
public sealed class CotationsController(IQuotationService cotations) : ControllerBase
{
    /// <summary>
    /// Searches quotations. Soft-deleted ones are excluded unless <c>includeSupprimes</c> is set.
    /// </summary>
    /// <param name="convertibles">
    /// Only won quotations still within validity — the set a booking or dossier may be created from.
    /// </param>
    [HttpGet]
    [ProducesResponseType<PagedResult<CotationSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<CotationSummaryDto>>> Search(
        [FromQuery] QuotationSearchRequest request, CancellationToken cancellationToken) =>
        Ok(await cotations.SearchAsync(request, cancellationToken));

    /// <summary>Full quotation with its lines, rates, documents and computed totals.</summary>
    [HttpGet("{keyCotation}")]
    [ProducesResponseType<CotationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CotationDto>> Get(string keyCotation, CancellationToken cancellationToken) =>
        Ok(await cotations.GetAsync(keyCotation, cancellationToken));

    /// <summary>Creates a quotation.</summary>
    /// <remarks>
    /// The company's own currency is inserted at rate 1 automatically, and the standard conditions and
    /// franchises are copied from <c>t_franchise_cotation</c> for the dossier type unless
    /// <c>appliquerConditions</c> is false.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType<CotationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CotationDto>> Create(CreateCotationRequest request, CancellationToken cancellationToken)
    {
        var created = await cotations.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { keyCotation = created.KeyCotation }, created);
    }

    /// <summary>Updates the editable header. Refused once the quotation has left Saisie/Envoyée.</summary>
    [HttpPut("{keyCotation}")]
    [ProducesResponseType<CotationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CotationDto>> Update(
        string keyCotation, UpdateCotationRequest request, CancellationToken cancellationToken) =>
        Ok(await cotations.UpdateAsync(keyCotation, request, cancellationToken));

    /// <summary>
    /// Validates and sends the quotation (<c>Saisie → Envoyée</c>).
    /// </summary>
    /// <remarks>Requires at least one price line; refused if the quotation is no longer open.</remarks>
    [HttpPost("{keyCotation}/envoyer")]
    [ProducesResponseType<CotationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CotationDto>> Envoyer(string keyCotation, CancellationToken cancellationToken) =>
        Ok(await cotations.EnvoyerAsync(keyCotation, cancellationToken));

    /// <summary>Closes the quotation as won or lost, recording the competitor details.</summary>
    /// <remarks>A lost quotation requires a reason (<c>motifPerdue</c>).</remarks>
    [HttpPost("{keyCotation}/cloturer")]
    [ProducesResponseType<CotationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CotationDto>> Cloturer(
        string keyCotation, CloturerCotationRequest request, CancellationToken cancellationToken) =>
        Ok(await cotations.CloturerAsync(keyCotation, request, cancellationToken));

    /// <summary>Cancels the quotation. Refused once it is won, lost, cancelled or a counter-offer.</summary>
    [HttpPost("{keyCotation}/annuler")]
    [ProducesResponseType<CotationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CotationDto>> Annuler(
        string keyCotation, MotifCotationRequest request, CancellationToken cancellationToken) =>
        Ok(await cotations.AnnulerAsync(keyCotation, request, cancellationToken));

    /// <summary>
    /// Soft-deletes the quotation (<c>supp=1</c>, status <c>Supprimée</c>).
    /// </summary>
    /// <remarks>
    /// Refused while the quotation carries attached documents or <c>t_frais_annexe_cotation</c> rows —
    /// the two legacy guards.
    /// </remarks>
    [HttpDelete("{keyCotation}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(string keyCotation, CancellationToken cancellationToken)
    {
        await cotations.DeleteAsync(keyCotation, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Duplicates the quotation, or issues a <b>counter-offer</b> from it.
    /// </summary>
    /// <remarks>
    /// With <c>contreOffre=true</c> the source is moved to <c>Contre Offre</c> and the copy links back
    /// through <c>cotation_contre_offre</c>. Either way the packing, price, exchange-rate and document
    /// lines are copied; purchase lines are <b>not</b>, matching the legacy duplication.
    /// </remarks>
    [HttpPost("{keyCotation}/dupliquer")]
    [ProducesResponseType<CotationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CotationDto>> Duplicate(
        string keyCotation, DuplicateCotationRequest request, CancellationToken cancellationToken)
    {
        var copy = await cotations.DuplicateAsync(keyCotation, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { keyCotation = copy.KeyCotation }, copy);
    }

    /// <summary>Replaces the packing lines wholesale.</summary>
    [HttpPut("{keyCotation}/colisages")]
    [ProducesResponseType<CotationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CotationDto>> SetColisages(
        string keyCotation, IReadOnlyList<ColisageCotationInput> colisages, CancellationToken cancellationToken) =>
        Ok(await cotations.SetColisagesAsync(keyCotation, colisages, cancellationToken));

    /// <summary>Replaces the price lines wholesale.</summary>
    /// <remarks>
    /// A <c>Pourcentage</c> line is a share of the freight base and cannot be priced in isolation, so its
    /// amount depends on the <c>Montant</c> lines on the same quotation.
    /// </remarks>
    [HttpPut("{keyCotation}/prix")]
    [ProducesResponseType<CotationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CotationDto>> SetPrix(
        string keyCotation, IReadOnlyList<PrixCotationInput> prix, CancellationToken cancellationToken) =>
        Ok(await cotations.SetPrixAsync(keyCotation, prix, cancellationToken));

    /// <summary>Replaces the purchase lines, recomputing MAD equivalents from the current rates.</summary>
    [HttpPut("{keyCotation}/achats")]
    [ProducesResponseType<CotationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CotationDto>> SetAchats(
        string keyCotation, IReadOnlyList<AchatCotationInput> achats, CancellationToken cancellationToken) =>
        Ok(await cotations.SetAchatsAsync(keyCotation, achats, cancellationToken));

    /// <summary>
    /// Replaces the exchange-rate table.
    /// </summary>
    /// <remarks>
    /// The table is deleted and rebuilt on every save, which is the legacy behaviour — the sale and
    /// purchase rate grids share it and overwrite each other. The company currency is always
    /// re-inserted at rate 1.
    /// </remarks>
    [HttpPut("{keyCotation}/taux-change")]
    [ProducesResponseType<CotationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CotationDto>> SetTauxChange(
        string keyCotation, IReadOnlyList<TauxChangeInput> taux, CancellationToken cancellationToken) =>
        Ok(await cotations.SetTauxChangeAsync(keyCotation, taux, cancellationToken));

    /// <summary>Attaches a document and raises the <c>attachement</c> flag.</summary>
    [HttpPost("{keyCotation}/documents")]
    [ProducesResponseType<CotationDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CotationDto>> AddDocument(
        string keyCotation, DocumentCotationInput document, CancellationToken cancellationToken) =>
        Ok(await cotations.AddDocumentAsync(keyCotation, document, cancellationToken));

    /// <summary>
    /// Removes a document, lowering <c>attachement</c> once the last one is gone.
    /// </summary>
    /// <remarks>
    /// The flag is derived from the document set, never set by hand, so it cannot go stale.
    /// </remarks>
    [HttpDelete("{keyCotation}/documents/{keyDocument}")]
    [ProducesResponseType<CotationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CotationDto>> RemoveDocument(
        string keyCotation, string keyDocument, CancellationToken cancellationToken) =>
        Ok(await cotations.RemoveDocumentAsync(keyCotation, keyDocument, cancellationToken));

    /// <summary>Sets or clears the grouping key that links several quotations together.</summary>
    [HttpPut("{keyCotation}/regroupement")]
    [ProducesResponseType<CotationDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CotationDto>> SetRegroupement(
        string keyCotation, [FromQuery] string? keyRegroupement, CancellationToken cancellationToken) =>
        Ok(await cotations.SetRegroupementAsync(keyCotation, keyRegroupement, cancellationToken));

    /// <summary>
    /// Totals across the price and purchase lines, with the resulting margin.
    /// </summary>
    /// <remarks>
    /// <c>devisesSansTaux</c> lists currencies present on the quotation that have no exchange rate.
    /// They are reported rather than converted at 1, because treating EUR as MAD would understate the
    /// total by an order of magnitude.
    /// </remarks>
    [HttpGet("{keyCotation}/totaux")]
    [ProducesResponseType<CotationTotauxDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CotationTotauxDto>> GetTotaux(string keyCotation, CancellationToken cancellationToken) =>
        Ok(await cotations.GetTotauxAsync(keyCotation, cancellationToken));
}
