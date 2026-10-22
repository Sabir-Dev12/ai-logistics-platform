using Logistics.Application.Abstractions;
using Logistics.Application.Features.Clients;
using Logistics.Application.Features.Payments;
using Logistics.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Logistics.Api.Controllers;

/// <summary>
/// Client (customer) master data — the migration of <c>frm_Codification_Client.vb</c>,
/// <c>frm_Liste_Clients.vb</c> and <c>frm_Ajout_Tiers.vb</c>.
/// </summary>
[ApiController]
[Route("api/v1/clients")]
[Tags("Clients")]
public sealed class ClientsController(IClientService clients) : ControllerBase
{
    /// <summary>Searches clients with paging and optional filters.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<ClientListItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ClientListItem>>> Search(
        [FromQuery] string? search,
        [FromQuery] string? statut,
        [FromQuery] string? classe,
        [FromQuery] bool? actif,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default,
        [FromQuery] bool? valide = null) =>
        Ok(await clients.SearchAsync(search, statut, classe, actif, page, pageSize, cancellationToken, valide));

    [HttpGet("{keyClient}")]
    [ProducesResponseType<ClientDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientDetail>> Get(string keyClient, CancellationToken cancellationToken) =>
        Ok(await clients.GetAsync(keyClient, cancellationToken));

    [HttpPost]
    [ProducesResponseType<ClientDetail>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ClientDetail>> Create(
        SaveClientRequest request, CancellationToken cancellationToken)
    {
        var created = await clients.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { keyClient = created.KeyClient }, created);
    }

    [HttpPut("{keyClient}")]
    [ProducesResponseType<ClientDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientDetail>> Update(
        string keyClient, SaveClientRequest request, CancellationToken cancellationToken) =>
        Ok(await clients.UpdateAsync(keyClient, request, cancellationToken));

    /// <summary>
    /// Validates the client fiche. Reproduces the legacy "validation" action that gates the Moroccan
    /// legal-identifier rules and the ledger-account requirement.
    /// </summary>
    [HttpPost("{keyClient}/validate")]
    [ProducesResponseType<ClientDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ClientDetail>> Validate(string keyClient, CancellationToken cancellationToken) =>
        Ok(await clients.ValidateAsync(keyClient, cancellationToken));

    /// <summary>
    /// Soft-deletes the client. Fails with 422 when other tables still reference it, mirroring the
    /// legacy guards rather than cascading.
    /// </summary>
    [HttpDelete("{keyClient}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(string keyClient, CancellationToken cancellationToken)
    {
        await clients.DeleteAsync(keyClient, cancellationToken);
        return NoContent();
    }

    [HttpPost("{keyClient}/adresses")]
    [ProducesResponseType<AdresseClientDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AdresseClientDto>> AddAdresse(
        string keyClient, SaveAdresseRequest request, CancellationToken cancellationToken)
    {
        var created = await clients.AddAdresseAsync(keyClient, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { keyClient }, created);
    }

    [HttpDelete("{keyClient}/adresses/{keyAdresse}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RemoveAdresse(
        string keyClient, string keyAdresse, CancellationToken cancellationToken)
    {
        await clients.RemoveAdresseAsync(keyClient, keyAdresse, cancellationToken);
        return NoContent();
    }

    [HttpPost("{keyClient}/banques")]
    [ProducesResponseType<BanqueTiersDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BanqueTiersDto>> AddBanque(
        string keyClient, SaveBanqueRequest request, CancellationToken cancellationToken)
    {
        var created = await clients.AddBanqueAsync(keyClient, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { keyClient }, created);
    }

    [HttpDelete("{keyClient}/banques/{keyBanque}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveBanque(
        string keyClient, string keyBanque, CancellationToken cancellationToken)
    {
        await clients.RemoveBanqueAsync(keyClient, keyBanque, cancellationToken);
        return NoContent();
    }
}

/// <summary>
/// Payments / règlements — the migration of <c>frm_liste_reglement_client.vb</c>,
/// <c>frm_Saisie_Reglement_Etrangers.vb</c> and <c>frm_Balance_Agee_Client.vb</c>.
/// </summary>
[ApiController]
[Route("api/v1/reglements")]
[Tags("Payments")]
public sealed class ReglementsController(IReglementService reglements) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<ReglementListItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ReglementListItem>>> Search(
        [FromQuery] TypeReglement? type,
        [FromQuery] string? keyTiers,
        [FromQuery] StatutReglement? statut,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default,
        [FromQuery] string? search = null,
        [FromQuery] string? tiers = null) =>
        Ok(await reglements.SearchAsync(type, keyTiers, statut, from, to, page, pageSize, cancellationToken, search, tiers));

    [HttpGet("{keyReglement}")]
    [ProducesResponseType<ReglementDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReglementDetail>> Get(string keyReglement, CancellationToken cancellationToken) =>
        Ok(await reglements.GetAsync(keyReglement, cancellationToken));

    [HttpPost]
    [ProducesResponseType<ReglementDetail>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ReglementDetail>> Create(
        CreateReglementRequest request, CancellationToken cancellationToken)
    {
        var created = await reglements.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { keyReglement = created.KeyReglement }, created);
    }

    /// <summary>
    /// Allocates (lettre) the payment against an invoice.
    /// </summary>
    /// <remarks>
    /// Returns the allocation plus the evaluated conversion difference and its classification. When the
    /// difference is below the company's automatic threshold, an <c>OD</c> counter-invoice is created
    /// automatically and its key is returned in <c>odKeyFacture</c>.
    /// </remarks>
    [HttpPost("{keyReglement}/allocations")]
    [ProducesResponseType<AllocateResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AllocateResult>> Allocate(
        string keyReglement, AllocateRequest request, CancellationToken cancellationToken) =>
        Ok(await reglements.AllocateAsync(keyReglement, request, cancellationToken));

    [HttpDelete("{keyReglement}/allocations/{keyFacture}")]
    [ProducesResponseType<ReglementDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReglementDetail>> Deallocate(
        string keyReglement, string keyFacture, CancellationToken cancellationToken) =>
        Ok(await reglements.DeallocateAsync(keyReglement, keyFacture, cancellationToken));

    /// <summary>Remits the payment to the bank, generating a remittance reference.</summary>
    [HttpPost("{keyReglement}/remise")]
    [ProducesResponseType<ReglementDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReglementDetail>> Remit(
        string keyReglement,
        [FromQuery] string keyTresorie,
        [FromQuery] string? refCompta,
        [FromQuery] DateTime dateRemise,
        CancellationToken cancellationToken) =>
        Ok(await reglements.RemitAsync(keyReglement, keyTresorie, refCompta, dateRemise, cancellationToken));

    /// <summary>Flags the payment as unpaid (impayé) with its incident reason.</summary>
    [HttpPost("{keyReglement}/impaye")]
    [ProducesResponseType<ReglementDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReglementDetail>> MarkImpaye(
        string keyReglement,
        [FromQuery] string motif,
        [FromQuery] string? commentaire,
        CancellationToken cancellationToken) =>
        Ok(await reglements.MarkImpayeAsync(keyReglement, motif, commentaire, cancellationToken));
}
