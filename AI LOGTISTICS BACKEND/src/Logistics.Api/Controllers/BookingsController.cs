using Logistics.Application.Abstractions;
using Logistics.Application.Features.Bookings;
using Microsoft.AspNetCore.Mvc;

namespace Logistics.Api.Controllers;

/// <summary>
/// Bookings — the migration of <c>frm_Creation_Booking.vb</c>, <c>frm_Liste_Booking.vb</c>,
/// <c>frm_Affectation_Booking_Voyage.vb</c> and the dépotage screens.
/// </summary>
/// <remarks>
/// <para>
/// A booking is the pre-dossier reservation raised before a dossier or voyage exists. It carries a
/// forecast measure block (<c>*_prevision</c>) captured up front and an actual block filled in at
/// dépotage; <b>the trailer capacity check uses the forecast volume</b>, which is why the capacity
/// endpoint reports on that and not on the actual one.
/// </para>
/// <para>
/// Two behaviours here are stricter than the desktop application, deliberately. Attaching a booking to
/// a voyage whose trailer is already full is refused (the legacy screen merely displayed the free
/// capacity and left the comparison to the user), and the same applies to a booking attached beyond
/// its validity date. Both are documented in the README.
/// </para>
/// </remarks>
[ApiController]
[Route("api/v1/bookings")]
[Tags("Bookings")]
public sealed class BookingsController(IBookingService bookings) : ControllerBase
{
    /// <summary>
    /// Searches bookings. Soft-deleted ones are excluded unless <c>includeSupprimes</c> is set.
    /// </summary>
    /// <param name="nonAffectes">Only bookings attached to no voyage at all.</param>
    [HttpGet]
    [ProducesResponseType<PagedResult<BookingSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<BookingSummaryDto>>> Search(
        [FromQuery] BookingSearchRequest request, CancellationToken cancellationToken) =>
        Ok(await bookings.SearchAsync(request, cancellationToken));

    /// <summary>Full booking, including its equipment and packing lines.</summary>
    [HttpGet("{keyBooking}")]
    [ProducesResponseType<BookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDto>> Get(string keyBooking, CancellationToken cancellationToken) =>
        Ok(await bookings.GetAsync(keyBooking, cancellationToken));

    /// <summary>Creates a booking.</summary>
    /// <remarks>
    /// The client is <b>derived</b>, not supplied: <c>t_type_dossier.client_dossier</c> names which
    /// party (EXPEDITEUR, DESTINATAIRE or PAYEUR) becomes <c>key_client</c>. The booking number is
    /// unique across the whole installation, not per company.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType<BookingDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BookingDto>> Create(CreateBookingRequest request, CancellationToken cancellationToken)
    {
        var created = await bookings.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { keyBooking = created.KeyBooking }, created);
    }

    /// <summary>Updates the editable header fields.</summary>
    /// <remarks>
    /// The booking type is intentionally not editable: it carries the direction and drives the
    /// client-attribution rule, so changing it would silently re-point <c>key_client</c>.
    /// </remarks>
    [HttpPut("{keyBooking}")]
    [ProducesResponseType<BookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingDto>> Update(
        string keyBooking, UpdateBookingRequest request, CancellationToken cancellationToken) =>
        Ok(await bookings.UpdateAsync(keyBooking, request, cancellationToken));

    /// <summary>Re-runs the business validation and reports why it fails as a 422.</summary>
    [HttpPost("{keyBooking}/valider")]
    [ProducesResponseType<BookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BookingDto>> Validate(string keyBooking, CancellationToken cancellationToken) =>
        Ok(await bookings.ValidateAsync(keyBooking, cancellationToken));

    /// <summary>Duplicates a booking, re-keying its equipment and packing lines.</summary>
    /// <remarks>
    /// The copy is forced to <c>$BOKC</c> and the source receives a <c>$DBOK</c> trace, which is how the
    /// legacy list marks a booking as having been duplicated.
    /// </remarks>
    [HttpPost("{keyBooking}/dupliquer")]
    [ProducesResponseType<BookingDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingDto>> Duplicate(
        string keyBooking, DuplicateBookingRequest request, CancellationToken cancellationToken)
    {
        var copy = await bookings.DuplicateAsync(keyBooking, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { keyBooking = copy.KeyBooking }, copy);
    }

    /// <summary>Cancels the booking (<c>$BOKAN</c>).</summary>
    /// <remarks>
    /// Refused while the booking is attached to a voyage — the legacy listing excluded cancelled
    /// bookings from almost everything, so cancelling an attached one would strand the trailer.
    /// </remarks>
    [HttpPost("{keyBooking}/annuler")]
    [ProducesResponseType<BookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BookingDto>> Annuler(
        string keyBooking, MotifRequest request, CancellationToken cancellationToken) =>
        Ok(await bookings.AnnulerAsync(keyBooking, request, cancellationToken));

    /// <summary>
    /// Soft-deletes the booking (<c>supprimer=1</c> + <c>$SBOK</c>).
    /// </summary>
    /// <remarks>
    /// The legacy "supprimer" never issued a physical <c>DELETE</c>; every listing filters
    /// <c>supprimer=0</c>, so nothing is removed here either.
    /// </remarks>
    [HttpDelete("{keyBooking}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(string keyBooking, CancellationToken cancellationToken)
    {
        await bookings.DeleteAsync(keyBooking, cancellationToken);
        return NoContent();
    }

    /// <summary>Records dépotage and moves the MEAD status to "En Stock".</summary>
    [HttpPost("{keyBooking}/depotage")]
    [ProducesResponseType<BookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BookingDto>> Depotage(
        string keyBooking, DepotageBookingRequest request, CancellationToken cancellationToken) =>
        Ok(await bookings.DepotageAsync(keyBooking, request, cancellationToken));

    /// <summary>Reverses a dépotage, clearing the depotage fields and the MEAD status.</summary>
    [HttpDelete("{keyBooking}/depotage")]
    [ProducesResponseType<BookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BookingDto>> AnnulerDepotage(string keyBooking, CancellationToken cancellationToken) =>
        Ok(await bookings.AnnulerDepotageAsync(keyBooking, cancellationToken));

    /// <summary>Links the booking to a dossier and converts it to <c>$BOCV</c>.</summary>
    [HttpPost("{keyBooking}/dossier")]
    [ProducesResponseType<BookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BookingDto>> LierDossier(
        string keyBooking, [FromQuery] string keyDossier, CancellationToken cancellationToken) =>
        Ok(await bookings.LierDossierAsync(keyBooking, keyDossier, cancellationToken));

    /// <summary>Detaches the dossier and returns the booking to <c>$ATRM</c>.</summary>
    [HttpDelete("{keyBooking}/dossier")]
    [ProducesResponseType<BookingDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<BookingDto>> DelierDossier(string keyBooking, CancellationToken cancellationToken) =>
        Ok(await bookings.DelierDossierAsync(keyBooking, cancellationToken));

    /// <summary>
    /// Attaches the booking to a voyage.
    /// </summary>
    /// <remarks>
    /// A booking belongs to <b>one</b> voyage at a time: the legacy picker excluded every booking
    /// already present in <c>t_detail_voyage_booking</c> regardless of which voyage held it. The link
    /// is recorded differently per voyage category — enlèvement sets <c>fin_enlevement</c>, prévisionnel
    /// stores <c>key_voyage_previsionnel</c>.
    /// </remarks>
    [HttpPost("{keyBooking}/voyage")]
    [ProducesResponseType<BookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BookingDto>> AffecterVoyage(
        string keyBooking, AffecterVoyageRequest request, CancellationToken cancellationToken) =>
        Ok(await bookings.AffecterVoyageAsync(keyBooking, request, cancellationToken));

    /// <summary>Detaches the booking from a voyage.</summary>
    [HttpDelete("{keyBooking}/voyage/{keyVoyage}")]
    [ProducesResponseType<BookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BookingDto>> DesaffecterVoyage(
        string keyBooking, string keyVoyage, CancellationToken cancellationToken) =>
        Ok(await bookings.DesaffecterVoyageAsync(keyBooking, keyVoyage, cancellationToken));

    /// <summary>
    /// Trailer capacity for a voyage: demand, remaining volume and occupancy rate.
    /// </summary>
    /// <remarks>
    /// Computed from <c>volume_prevision</c> — the forecast volume — matching the legacy
    /// <c>capacite_remorque - sum(volume_prevision)</c> calculation.
    /// </remarks>
    [HttpGet("capacite/{keyVoyage}")]
    [ProducesResponseType<BookingCapacityDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingCapacityDto>> GetCapacite(string keyVoyage, CancellationToken cancellationToken) =>
        Ok(await bookings.GetCapaciteAsync(keyVoyage, cancellationToken));

    /// <summary>Replaces the equipment lines wholesale.</summary>
    /// <remarks>
    /// A booking whose type is "Complet" must keep at least one line, which is why the resulting
    /// booking is validated before being returned.
    /// </remarks>
    [HttpPut("{keyBooking}/equipements")]
    [ProducesResponseType<BookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BookingDto>> SetEquipements(
        string keyBooking, IReadOnlyList<EquipementBookingInput> equipements, CancellationToken cancellationToken) =>
        Ok(await bookings.SetEquipementsAsync(keyBooking, equipements, cancellationToken));

    /// <summary>
    /// Replaces the packing lines wholesale, mirroring the legacy delete-then-reinsert.
    /// </summary>
    /// <remarks>
    /// The actual measure block is reset to zero, as the legacy code did, because it is derived from
    /// the packing detail.
    /// </remarks>
    [HttpPut("{keyBooking}/colisages")]
    [ProducesResponseType<BookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BookingDto>> SetColisages(
        string keyBooking, IReadOnlyList<ColisageBookingInput> colisages, CancellationToken cancellationToken) =>
        Ok(await bookings.SetColisagesAsync(keyBooking, colisages, cancellationToken));
}
