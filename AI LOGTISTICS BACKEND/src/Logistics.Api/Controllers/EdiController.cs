using Logistics.Application.Features.Edi;
using Microsoft.AspNetCore.Mvc;

namespace Logistics.Api.Controllers;

/// <summary>
/// EDI / Intégration — the STC fixed-width import (the port of
/// <c>frm_Integration_EDI_IMPORT_STC.vb</c>).
/// </summary>
/// <remarks>
/// <para>
/// The file is <b>1000 characters per line</b>, and a record's type is the two characters at offset 9:
/// <c>"00"</c> is a voyage header, anything else is a dossier line. A single misaligned line rejects the
/// <b>whole file</b> — a shifted line still reads plausible values, so a partial import would be quietly
/// wrong rather than visibly broken.
/// </para>
/// <para>
/// <c>POST /analyser</c> is a true dry run: the parser is pure, so it reports what would be staged
/// without touching anything. <c>POST /importer</c> stages the records for the caller's slice
/// (company + agency + user), replacing whatever was staged there before.
/// </para>
/// <para>
/// <b>Promotion is not implemented.</b> Turning staged rows into voyages and dossiers needs the
/// referential reconciliation that maps the file's EDI codes (port labels, destination reference,
/// incoterm, currency, COD) onto application keys. Staging a row that has not been reconciled would only
/// fail at promotion, so the gap is deliberate and visible rather than half-built.
/// </para>
/// </remarks>
[ApiController]
[Route("api/v1/edi/stc")]
[Tags("EDI")]
public sealed class EdiController(IEdiStcService edi, IEdiReconciliationService reconciliation) : ControllerBase
{
    /// <summary>The maximum accepted upload, so a stray binary cannot exhaust memory.</summary>
    private const long TailleMaxOctets = 20 * 1024 * 1024;

    /// <summary>
    /// Analyses an STC/EDI file and reports its records and anomalies. Writes nothing.
    /// </summary>
    /// <remarks>
    /// Returns <c>fichierAccepte = false</c> with the offending line numbers when the file is misaligned;
    /// no records are produced in that case.
    /// </remarks>
    [HttpPost("analyser")]
    [RequestSizeLimit(TailleMaxOctets)]
    [ProducesResponseType<EdiResultatAnalyse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EdiResultatAnalyse>> Analyser(IFormFile fichier, CancellationToken cancellationToken)
    {
        var lignes = await LireAsync(fichier, cancellationToken);
        return Ok(edi.Analyser(lignes));
    }

    /// <summary>
    /// Parses the file and stages its records for the caller, replacing the previous staging slice.
    /// </summary>
    /// <remarks>
    /// A misaligned file stages nothing. Records that carry anomalies are still staged — the operators
    /// work from the anomaly register and fix rows in place — but they will not import.
    /// </remarks>
    [HttpPost("importer")]
    [RequestSizeLimit(TailleMaxOctets)]
    [ProducesResponseType<EdiImportResultat>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EdiImportResultat>> Importer(IFormFile fichier, CancellationToken cancellationToken)
    {
        var lignes = await LireAsync(fichier, cancellationToken);
        return Ok(await edi.ImporterAsync(lignes, cancellationToken));
    }

    /// <summary>
    /// Resolves the staged slice against the referentials.
    /// </summary>
    /// <remarks>
    /// This is the step the parser cannot do. The file speaks its own code space — port <b>labels</b>, a
    /// destination reference, two-character incoterm/currency/COD codes — so every value has to be mapped
    /// onto an application key before a row can become a voyage or dossier. Anything that cannot be
    /// resolved is recorded on the row itself, which is where the operators read it.
    /// </remarks>
    [HttpPost("reconcilier")]
    [ProducesResponseType<EdiReconciliationResultat>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EdiReconciliationResultat>> Reconcilier(CancellationToken cancellationToken) =>
        Ok(await reconciliation.ReconcilierAsync(cancellationToken));

    /// <summary>
    /// Creates voyages and dossiers from a fully reconciled slice, then consumes the staging rows.
    /// </summary>
    /// <remarks>
    /// Refused with 422 while any anomaly remains — the legacy promotion aborted on the first unresolved
    /// row, so a partial import is not offered here either. The file's voyage number becomes the voyage's
    /// <c>ref_agent</c>, which is what makes a re-import fail rather than duplicate.
    /// </remarks>
    [HttpPost("promouvoir")]
    [ProducesResponseType<EdiPromotionResultat>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<EdiPromotionResultat>> Promouvoir(CancellationToken cancellationToken) =>
        Ok(await reconciliation.PromouvoirAsync(cancellationToken));

    /// <summary>
    /// Reads the upload as text lines.
    /// </summary>
    /// <remarks>
    /// The file is fixed-width text, so no encoding detection is attempted: the offsets are byte positions
    /// the legacy reader produced with a plain text read, and guessing an encoding would shift them.
    /// </remarks>
    private static async Task<IReadOnlyList<string>> LireAsync(IFormFile? fichier, CancellationToken cancellationToken)
    {
        if (fichier is null || fichier.Length == 0)
        {
            throw new Domain.Common.BusinessRuleException("edi.fichier", "Aucun fichier n'a été fourni.");
        }

        using var lecteur = new StreamReader(fichier.OpenReadStream());
        var contenu = await lecteur.ReadToEndAsync(cancellationToken);

        return contenu
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
    }
}
