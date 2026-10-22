using Logistics.Application.Abstractions;
using Logistics.Application.Features.Resources;
using Logistics.Domain.Entities.Resources;
using Microsoft.AspNetCore.Mvc;

namespace Logistics.Api.Controllers;

/// <summary>
/// Resources — personnel and matériel (the port of <c>ClassPersonnel.vb</c>,
/// <c>frm_Codification_Matériaux.vb</c>, <c>frm_Sortie_du_materiel.vb</c> and the repair screens).
/// </summary>
/// <remarks>
/// <para>
/// Tractors, trailers and vehicles are all rows of <c>t_materiel</c>, discriminated by
/// <c>type_materiel</c>; there is no separate table per kind. That is why the matériel endpoints take a
/// type rather than being split per vehicle kind.
/// </para>
/// <para>
/// Two lifecycle differences from the rest of the schema matter here. <c>t_personnel</c> has
/// <b>no delete flag</b> — a departure is <c>date_sortie</c> plus a revoked login, so
/// <c>POST /personnel/{key}/sortie</c> is the removal path and it clears the credentials. And
/// <c>t_materiel</c> has <b>two</b> retirement mechanisms (the <c>supprimer</c> flag and disposal via
/// <c>statut='S'</c>); disposal here is a business status, so a disposed vehicle stays visible and can
/// still be read.
/// </para>
/// </remarks>
[ApiController]
[Route("api/v1/resources")]
[Tags("Resources")]
public sealed class ResourcesController(IResourceService resources) : ControllerBase
{
    // ── Personnel ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Searches personnel. Departed staff are excluded unless <c>includeSortis</c> is set.
    /// </summary>
    /// <param name="keyFonction">
    /// Filter by function. Drivers are <c>$CHAUFFEUR</c> — the literal the costing engine keys on.
    /// </param>
    [HttpGet("personnel")]
    [ProducesResponseType<PagedResult<PersonnelDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<PersonnelDto>>> SearchPersonnel(
        [FromQuery] PersonnelSearchRequest request, CancellationToken cancellationToken) =>
        Ok(await resources.SearchPersonnelAsync(request, cancellationToken));

    [HttpGet("personnel/{keyPersonnel}")]
    [ProducesResponseType<PersonnelDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PersonnelDto>> GetPersonnel(string keyPersonnel, CancellationToken cancellationToken) =>
        Ok(await resources.GetPersonnelAsync(keyPersonnel, cancellationToken));

    [HttpPost("personnel")]
    [ProducesResponseType<PersonnelDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelDto>> CreatePersonnel(CreatePersonnelRequest request, CancellationToken cancellationToken)
    {
        var created = await resources.CreatePersonnelAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetPersonnel), new { keyPersonnel = created.KeyPersonnel }, created);
    }

    [HttpPut("personnel/{keyPersonnel}")]
    [ProducesResponseType<PersonnelDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PersonnelDto>> UpdatePersonnel(
        string keyPersonnel, UpdatePersonnelRequest request, CancellationToken cancellationToken) =>
        Ok(await resources.UpdatePersonnelAsync(keyPersonnel, request, cancellationToken));

    /// <summary>
    /// Records a departure — this table's only removal path.
    /// </summary>
    /// <remarks>
    /// Sets <c>date_sortie</c> and the reason, blocks the account and clears the login and password.
    /// There is no delete flag on <c>t_personnel</c>, so nothing else would disable a departed
    /// employee's access.
    /// </remarks>
    [HttpPost("personnel/{keyPersonnel}/sortie")]
    [ProducesResponseType<PersonnelDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelDto>> EnregistrerSortie(
        string keyPersonnel, SortiePersonnelRequest request, CancellationToken cancellationToken) =>
        Ok(await resources.EnregistrerSortieAsync(keyPersonnel, request, cancellationToken));

    /// <summary>
    /// Sets the <b>monthly</b> cost of an employee.
    /// </summary>
    /// <remarks>
    /// Monthly, not daily — the schema has no per-day cost column. The voyage cost engine apportions this
    /// across the period's trajets; naming it "cost" without the unit is how the per-day assumption crept
    /// in before.
    /// </remarks>
    [HttpPut("personnel/{keyPersonnel}/cout")]
    [ProducesResponseType<PersonnelDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelDto>> SetCoutPersonnel(
        string keyPersonnel, CoutMaterielRequest request, CancellationToken cancellationToken) =>
        Ok(await resources.SetCoutPersonnelAsync(keyPersonnel, request, cancellationToken));

    // ── Matériel ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>Searches matériel. Disposed and soft-deleted vehicles are excluded unless asked for.</summary>
    /// <param name="typeMateriel">TRACTEUR, REMORQUE, … — the discriminator, not a separate table.</param>
    [HttpGet("materiel")]
    [ProducesResponseType<PagedResult<MaterielDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<MaterielDto>>> SearchMateriel(
        [FromQuery] MaterielSearchRequest request, CancellationToken cancellationToken) =>
        Ok(await resources.SearchMaterielAsync(request, cancellationToken));

    [HttpGet("materiel/{keyMateriel}")]
    [ProducesResponseType<MaterielDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MaterielDto>> GetMateriel(string keyMateriel, CancellationToken cancellationToken) =>
        Ok(await resources.GetMaterielAsync(keyMateriel, cancellationToken));

    [HttpPost("materiel")]
    [ProducesResponseType<MaterielDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<MaterielDto>> CreateMateriel(CreateMaterielRequest request, CancellationToken cancellationToken)
    {
        var created = await resources.CreateMaterielAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetMateriel), new { keyMateriel = created.KeyMateriel }, created);
    }

    [HttpPut("materiel/{keyMateriel}")]
    [ProducesResponseType<MaterielDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MaterielDto>> UpdateMateriel(
        string keyMateriel, UpdateMaterielRequest request, CancellationToken cancellationToken) =>
        Ok(await resources.UpdateMaterielAsync(keyMateriel, request, cancellationToken));

    /// <summary>
    /// Disposes of a vehicle: status <c>Sorti</c> with its date, reason, sale value and odometer.
    /// </summary>
    /// <remarks>
    /// Not a deletion — the vehicle stays readable as history.
    /// </remarks>
    [HttpPost("materiel/{keyMateriel}/sortie")]
    [ProducesResponseType<MaterielDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<MaterielDto>> SortirDuParc(
        string keyMateriel, SortieMaterielRequest request, CancellationToken cancellationToken) =>
        Ok(await resources.SortirDuParcAsync(keyMateriel, request, cancellationToken));

    /// <summary>Marks the vehicle as under repair (<c>statut='R'</c>).</summary>
    [HttpPost("materiel/{keyMateriel}/reparation/entree")]
    [ProducesResponseType<MaterielDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<MaterielDto>> EntrerReparation(string keyMateriel, CancellationToken cancellationToken) =>
        Ok(await resources.EntrerReparationAsync(keyMateriel, cancellationToken));

    /// <summary>Returns a repaired vehicle to service (<c>statut='D'</c>).</summary>
    [HttpPost("materiel/{keyMateriel}/reparation/sortie")]
    [ProducesResponseType<MaterielDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<MaterielDto>> SortirReparation(string keyMateriel, CancellationToken cancellationToken) =>
        Ok(await resources.SortirReparationAsync(keyMateriel, cancellationToken));

    /// <summary>
    /// Sets the <b>monthly</b> cost of a vehicle.
    /// </summary>
    /// <remarks>
    /// Only types with <c>t_type_materiel.generation_cout = 1</c> take part in voyage costing, so setting
    /// a cost on an unflagged type has no effect on generated costs.
    /// </remarks>
    [HttpPut("materiel/{keyMateriel}/cout")]
    [ProducesResponseType<MaterielDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<MaterielDto>> SetCoutMateriel(
        string keyMateriel, CoutMaterielRequest request, CancellationToken cancellationToken) =>
        Ok(await resources.SetCoutMaterielAsync(keyMateriel, request, cancellationToken));
}
