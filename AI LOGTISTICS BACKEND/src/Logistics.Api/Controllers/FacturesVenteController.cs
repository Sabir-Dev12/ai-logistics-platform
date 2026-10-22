using Logistics.Application.Abstractions;
using Logistics.Application.Features.Invoicing;
using Logistics.Application.Features.ReferenceData;
using Logistics.Domain.Entities.Invoicing;
using Logistics.Domain.Entities.ReferenceData;
using Logistics.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Logistics.Api.Controllers;

/// <summary>
/// Sales invoicing — the migration of <c>frm_liste_facture_vente.vb</c>,
/// <c>frm_Detail_Taxation_Vente.vb</c> and <c>frm_Saisie_Facture_Achat.vb</c>.
/// </summary>
[ApiController]
[Route("api/v1/factures-vente")]
[Tags("Invoicing")]
public sealed class FacturesVenteController(IFactureVenteService factures) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<FactureListItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<FactureListItem>>> Search(
        [FromQuery] string? keyClient,
        [FromQuery] TypeFacture? type,
        [FromQuery] string? statut,
        [FromQuery] bool? factRegle,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default,
        [FromQuery] string? search = null) =>
        Ok(await factures.SearchAsync(keyClient, type, statut, factRegle, from, to, page, pageSize, cancellationToken, search));

    [HttpGet("{keyFacture}")]
    [ProducesResponseType<FactureDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FactureDetail>> Get(string keyFacture, CancellationToken cancellationToken) =>
        Ok(await factures.GetAsync(keyFacture, cancellationToken));

    /// <summary>
    /// Creates a sales invoice. Header totals are always derived from the supplied lines — the API never
    /// accepts a caller-computed total, which removes the legacy failure mode where the header buckets
    /// and the detail drifted apart.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<FactureDetail>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<FactureDetail>> Create(
        CreateFactureRequest request, CancellationToken cancellationToken)
    {
        var created = await factures.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { keyFacture = created.KeyFacture }, created);
    }

    /// <summary>Recomputes the header totals, VAT recap and withholding from the stored lines.</summary>
    [HttpPost("{keyFacture}/recalculate")]
    [ProducesResponseType<FactureDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<FactureDetail>> Recalculate(string keyFacture, CancellationToken cancellationToken) =>
        Ok(await factures.RecalculateAsync(keyFacture, cancellationToken));

    /// <summary>
    /// Generates the full credit note ("avoir total"): the source invoice is copied with every monetary
    /// column negated and each line key re-prefixed with 'A'.
    /// </summary>
    [HttpPost("{keyFacture}/avoir-total")]
    [ProducesResponseType<FactureDetail>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<FactureDetail>> CreateAvoirTotal(
        string keyFacture,
        [FromQuery] bool avoirTotalFactRegle = false,
        CancellationToken cancellationToken = default)
    {
        var avoir = await factures.CreateAvoirTotalAsync(keyFacture, avoirTotalFactRegle, cancellationToken);
        return CreatedAtAction(nameof(Get), new { keyFacture = avoir.KeyFacture }, avoir);
    }

    /// <summary>Applies a VAT exemption, zeroing the rate on every line and the VAT recap rows.</summary>
    [HttpPost("{keyFacture}/exoneration")]
    [ProducesResponseType<FactureDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<FactureDetail>> ApplyExoneration(
        string keyFacture, [FromQuery] string? commentaire, CancellationToken cancellationToken) =>
        Ok(await factures.ApplyExonerationAsync(keyFacture, commentaire, cancellationToken));
}

/// <summary>
/// Reference data — the migration of the ~50 <c>Class*.vb</c> referential classes and their codification
/// forms. One generic repository and one controller replace what used to be a hand-written data-access
/// class per table.
/// </summary>
[ApiController]
[Route("api/v1/reference")]
[Tags("Reference data")]
public sealed class ReferenceDataController(
    IReferenceDataRepository referenceData,
    IReferenceDataService referenceService,
    IParametreSocieteProvider parametres,
    IAuditTrail audit,
    ITenantContext tenant) : ControllerBase
{
    // Countries ---------------------------------------------------------------------------------
    [HttpGet("pays")]
    [ProducesResponseType<IReadOnlyList<Pays>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<Pays>>> ListPays(CancellationToken cancellationToken) =>
        Ok(await referenceData.ListAsync<Pays>(cancellationToken));

    [HttpPost("pays")]
    [ProducesResponseType<Pays>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Pays>> CreatePays(PaysRequest request, CancellationToken cancellationToken)
    {
        if (await referenceData.AnyAsync<Pays>(p => p.Id == request.CodePays.ToUpperInvariant(), cancellationToken))
        {
            return Conflict(new ProblemDetails { Title = "Duplicate country", Detail = $"Country '{request.CodePays}' already exists." });
        }

        var pays = new Pays(request.CodePays, request.IntitulePays, request.Alpha2);
        pays.Update(request.IntitulePays, request.Alpha2, request.Accord, request.ZonePays);
        referenceData.Add(pays);
        await referenceData.SaveChangesAsync(cancellationToken);

        await audit.JournalAsync("Pays", "Ajout", $"pays={request.IntitulePays}, alpha2={request.Alpha2}", cancellationToken);
        return CreatedAtAction(nameof(ListPays), new { code = pays.CodePays }, pays);
    }

    // Currencies --------------------------------------------------------------------------------
    [HttpGet("devises")]
    [ProducesResponseType<IReadOnlyList<Devise>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<Devise>>> ListDevises(CancellationToken cancellationToken) =>
        Ok(await referenceData.ListAsync<Devise>(cancellationToken));

    [HttpPost("devises")]
    [ProducesResponseType<Devise>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Devise>> CreateDevise(DeviseRequest request, CancellationToken cancellationToken)
    {
        if (await referenceData.AnyAsync<Devise>(d => d.Id == request.CodeDevise.ToUpperInvariant(), cancellationToken))
        {
            return Conflict(new ProblemDetails { Title = "Duplicate currency", Detail = $"Currency '{request.CodeDevise}' already exists." });
        }

        var devise = new Devise(request.CodeDevise, request.LibelleDevise, request.NbDecimal);
        devise.Update(request.LibelleDevise, request.LibelleFacture, request.LibelleCent, request.NbDecimal);
        referenceData.Add(devise);
        await referenceData.SaveChangesAsync(cancellationToken);

        await audit.JournalAsync("Devise", "Ajout", $"devise={request.CodeDevise}", cancellationToken);
        return CreatedAtAction(nameof(ListDevises), new { code = devise.CodeDevise }, devise);
    }

    // Agencies ----------------------------------------------------------------------------------
    [HttpGet("agences")]
    [ProducesResponseType<IReadOnlyList<Agence>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<Agence>>> ListAgences(CancellationToken cancellationToken)
    {
        var all = await referenceData.ListAsync<Agence>(cancellationToken);
        return Ok(all.Where(a => a.SocieteKey == tenant.SocieteKey).ToArray());
    }

    // VAT codes ---------------------------------------------------------------------------------
    [HttpGet("comptes-tva")]
    [ProducesResponseType<IReadOnlyList<CompteTva>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CompteTva>>> ListComptesTva(CancellationToken cancellationToken)
    {
        var all = await referenceData.ListAsync<CompteTva>(cancellationToken);
        return Ok(all.Where(c => c.SocieteKey == tenant.SocieteKey).ToArray());
    }

    // Payment modes -----------------------------------------------------------------------------
    [HttpGet("modes-paiement")]
    [ProducesResponseType<IReadOnlyList<ModePaiement>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ModePaiement>>> ListModesPaiement(CancellationToken cancellationToken) =>
        Ok(await referenceData.ListAsync<ModePaiement>(cancellationToken));

    // Treasury ----------------------------------------------------------------------------------
    [HttpGet("tresoreries")]
    [ProducesResponseType<IReadOnlyList<Tresorie>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<Tresorie>>> ListTresoreries(CancellationToken cancellationToken)
    {
        var all = await referenceData.ListAsync<Tresorie>(cancellationToken);
        return Ok(all.Where(t => t.SocieteKey == tenant.SocieteKey).ToArray());
    }

    // Dossier types -----------------------------------------------------------------------------
    [HttpGet("types-dossier")]
    [ProducesResponseType<IReadOnlyList<TypeDossier>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TypeDossier>>> ListTypesDossier(CancellationToken cancellationToken) =>
        Ok(await referenceData.ListAsync<TypeDossier>(cancellationToken));

    // Incoterms ---------------------------------------------------------------------------------
    [HttpGet("incoterms")]
    [ProducesResponseType<IReadOnlyList<Incoterm>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<Incoterm>>> ListIncoterms(CancellationToken cancellationToken)
    {
        var all = await referenceData.ListAsync<Incoterm>(cancellationToken);
        // Soft-deleted incoterms are excluded, as every legacy read did.
        return Ok(all.Where(i => !i.Supprime).ToArray());
    }

    // Bank-fee natures --------------------------------------------------------------------------
    [HttpGet("natures-frais-bancaire")]
    [ProducesResponseType<IReadOnlyList<NatureFraisBancaire>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NatureFraisBancaire>>> ListNaturesFraisBancaire(CancellationToken cancellationToken)
    {
        var all = await referenceData.ListAsync<NatureFraisBancaire>(cancellationToken);
        return Ok(all.Where(n => n.SocieteKey == tenant.SocieteKey).ToArray());
    }

    // Client classes / statuses / risks ---------------------------------------------------------
    [HttpGet("classes-client")]
    [ProducesResponseType<IReadOnlyList<ClasseClient>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClasseClient>>> ListClassesClient(CancellationToken cancellationToken)
    {
        var all = await referenceData.ListAsync<ClasseClient>(cancellationToken);
        return Ok(all.Where(c => c.SocieteKey == tenant.SocieteKey).ToArray());
    }

    [HttpGet("statuts-client")]
    [ProducesResponseType<IReadOnlyList<StatutClient>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StatutClient>>> ListStatutsClient(CancellationToken cancellationToken) =>
        Ok(await referenceData.ListAsync<StatutClient>(cancellationToken));

    [HttpGet("risques")]
    [ProducesResponseType<IReadOnlyList<Risque>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<Risque>>> ListRisques(CancellationToken cancellationToken) =>
        Ok(await referenceData.ListAsync<Risque>(cancellationToken));

    /// <summary>
    /// The company settings held in <c>t_parametre_societe</c>.
    /// </summary>
    /// <remarks>
    /// <b>Read-only.</b> The desktop application remains the authority for these values and caches twenty of
    /// them at login, so an API write would be invisible to it until restart — behaviour would diverge
    /// between the two front ends with nothing to show why. Writes are therefore not exposed.
    /// <para>
    /// Note that the table is key/value and every value is text: some are booleans (<c>Oui</c>/<c>Non</c>),
    /// some numeric strings, and some are <b>SQL predicate fragments</b> rather than data
    /// (<c>base_fret</c>, <c>ch_comm</c>, …). The latter are listed in <c>SetCodes.ParametresFragmentsSql</c>
    /// and are deliberately not interpreted.
    /// </para>
    /// </remarks>
    [HttpGet("parametres")]
    [ProducesResponseType<IReadOnlyList<ParametreSociete>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ParametreSociete>>> GetParametres(CancellationToken cancellationToken) =>
        Ok(await parametres.ListerAsync(cancellationToken));

    // Company parameters ------------------------------------------------------------------------
    [HttpGet("societe")]
    [ProducesResponseType<Societe>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Societe>> GetSociete(CancellationToken cancellationToken)
    {
        var societe = await referenceData.FindAsync<Societe>(tenant.SocieteKey);
        return societe is null ? NotFound() : Ok(societe);
    }

    /// <summary>
    /// Reports what still references a referential row, without changing anything.
    /// </summary>
    /// <remarks>
    /// Provided as a separate call because the legacy screens could only discover a blocking reference
    /// by attempting the delete and reading an error message.
    /// </remarks>
    [HttpGet("{kind}/{key}/references")]
    [ProducesResponseType<ReferenceCheckResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReferenceCheckResult>> CheckReferences(
        ReferentialKind kind, string key, CancellationToken cancellationToken) =>
        Ok(await referenceService.CheckReferencesAsync(kind, key, cancellationToken));

    /// <summary>
    /// Deletes a referential row, refusing with 422 while something still references it.
    /// </summary>
    /// <remarks>
    /// Referentials that the legacy application soft-deleted (<c>t_agence</c> via <c>supp</c>,
    /// <c>t_incoterm</c> via <c>supp_incoterm</c>) are still soft-deleted here; every other one is
    /// physically removed, matching the legacy behaviour.
    /// <para>
    /// Requires the parameterisation role. Deleting a referential is a configuration change, which is the
    /// surface the legacy <c>admin</c> flag gated — unlike ordinary business mutations.
    /// </para>
    /// </remarks>
    [HttpDelete("{kind}/{key}")]
    [Authorize(Policy = Authentication.ApiAuthentication.PolitiqueAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DeleteReferential(
        ReferentialKind kind, string key, CancellationToken cancellationToken)
    {
        await referenceService.DeleteAsync(kind, key, cancellationToken);
        return NoContent();
    }
}

public sealed record PaysRequest(string CodePays, string? IntitulePays, string? Alpha2, string? Accord, string? ZonePays);

public sealed record DeviseRequest(string CodeDevise, string? LibelleDevise, string? LibelleFacture, string? LibelleCent, int NbDecimal);
