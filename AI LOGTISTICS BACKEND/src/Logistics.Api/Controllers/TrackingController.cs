using Logistics.Application.Features.Tracking;
using Microsoft.AspNetCore.Mvc;

namespace Logistics.Api.Controllers;

/// <summary>
/// Tracking — the migration of <c>frm_Tableau_Bord.vb</c> and the <c>frm_Trace_*</c> viewers.
/// </summary>
/// <remarks>
/// <para>
/// Every business table in the legacy schema is audited through a per-entity <c>t_trace_*</c> table
/// rather than through <c>date_creation</c> / <c>utilisateur</c> columns, and "who created this and
/// when" is recovered by querying that table for the creation status code (<c>$ACLT</c> for a client,
/// <c>$CVOY</c> for a voyage, <c>$CVENT</c> for an invoice…). This controller exposes that data as a
/// cross-entity feed with the <c>$</c>-codes already resolved to labels, which the desktop application
/// could not do — it could only show one entity type at a time, in raw codes.
/// </para>
/// <para>
/// Note the two clocks on every event: <c>date_trace_user</c> is the business date the user asserted,
/// while <c>date_trace_system</c> is when the row was physically written. They differ often enough that
/// conflating them produces a misleading timeline, so both are returned.
/// </para>
/// </remarks>
[ApiController]
[Route("api/v1/tracking")]
[Tags("Tracking")]
public sealed class TrackingController(ITrackingService tracking) : ControllerBase
{
    /// <summary>
    /// Audit timeline for one entity, most recent first.
    /// </summary>
    /// <param name="entite">client | voyage | dossier | factureVente | reglement | booking | cotation</param>
    /// <param name="cle">The entity key, e.g. a <c>key_dossier</c>.</param>
    [HttpGet("{entite}/{cle}")]
    [ProducesResponseType<IReadOnlyList<TrackingEventDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<TrackingEventDto>>> GetTimeline(
        TrackingEntity entite, string cle, [FromQuery] int take = 100, CancellationToken cancellationToken = default) =>
        Ok(await tracking.GetTimelineAsync(entite, cle, take, cancellationToken));

    /// <summary>
    /// Latest known state of one entity: last event, its label, the actor and a short history.
    /// </summary>
    [HttpGet("{entite}/{cle}/statut")]
    [ProducesResponseType<EntityTrackingStatusDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EntityTrackingStatusDto>> GetStatus(
        TrackingEntity entite, string cle, CancellationToken cancellationToken) =>
        Ok(await tracking.GetStatusAsync(entite, cle, cancellationToken));

    /// <summary>
    /// Cross-entity activity feed, optionally narrowed to some entity kinds and a date window.
    /// </summary>
    /// <remarks>
    /// This is the endpoint a dashboard is built on. Omitting <paramref name="entites"/> covers every
    /// tracked entity kind.
    /// </remarks>
    [HttpGet("activite")]
    [ProducesResponseType<IReadOnlyList<TrackingEventDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TrackingEventDto>>> GetActivity(
        [FromQuery] TrackingEntity[]? entites,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default) =>
        Ok(await tracking.GetActivityAsync(entites, from, to, take, cancellationToken));

    /// <summary>
    /// Status/event code catalogue from <c>t_statut</c>, optionally filtered by scope.
    /// </summary>
    /// <param name="utilisation">'Voyage', 'Dossier', 'Booking', 'Personnel', 'Matériel', …</param>
    [HttpGet("statuts")]
    [ProducesResponseType<IReadOnlyList<StatutDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StatutDto>>> GetStatuts(
        [FromQuery] string? utilisation, CancellationToken cancellationToken) =>
        Ok(await tracking.GetStatutsAsync(utilisation, cancellationToken));

    /// <summary>
    /// Appends an audit event.
    /// </summary>
    /// <remarks>
    /// The <c>$</c>-prefixed status codes are system events; user-entered codes should not start with
    /// <c>$</c>, matching the convention the legacy UI enforced when it hid system codes from the
    /// pickers and refused to delete them.
    /// </remarks>
    [HttpPost("{entite}/{cle}/evenements")]
    [ProducesResponseType<TrackingEventDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TrackingEventDto>> RecordEvent(
        TrackingEntity entite, string cle, RecordTrackingEventRequest request, CancellationToken cancellationToken)
    {
        var created = await tracking.RecordEventAsync(entite, cle, request, cancellationToken);
        return CreatedAtAction(nameof(GetTimeline), new { entite, cle }, created);
    }
}
