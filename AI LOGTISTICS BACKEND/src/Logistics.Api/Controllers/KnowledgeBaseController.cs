using Logistics.Application.AI.Agent;
using Logistics.Application.AI.RAG;
using Logistics.Application.Abstractions;
using Logistics.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Logistics.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/ai/documents")]
[Tags("Knowledge Base / RAG")]
public sealed class KnowledgeBaseController(IKnowledgeBaseService documents, IRetrievalService retrieval, IAgentContext context, KnowledgeBaseOptions options, ILogger<KnowledgeBaseController> logger) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<KnowledgeDocument>> List([FromQuery] string? search, [FromQuery] string? type, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        => documents.ListAsync(context.DocumentCollectionId, new(search, type, status, page, pageSize), cancellationToken);
    [HttpGet("{id}")]
    public Task<KnowledgeDocument> Get(string id, CancellationToken cancellationToken) => documents.GetAsync(context.DocumentCollectionId, id, cancellationToken);
    /// <summary>Accepts 1–10 files. Each returned document reports its indexing outcome; no simulated success.</summary>
    [HttpPost]
    [RequestSizeLimit(50 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 50 * 1024 * 1024)]
    [ProducesResponseType<IReadOnlyList<KnowledgeDocument>>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Upload([FromForm] List<IFormFile> files, CancellationToken cancellationToken)
    {
        if (files.Count is < 1 || files.Count > options.MaximumBatchFiles) throw new BusinessRuleException("documents.files", "Sélectionnez entre 1 et 10 fichiers.");
        if (files.Any(file => file.Length is < 1 || file.Length > options.MaximumFileBytes)) throw new BusinessRuleException("documents.size", "Fichier vide ou trop volumineux.");
        var uploads = new List<KnowledgeUpload>();
        foreach (var file in files)
        {
            await using var input = file.OpenReadStream(); using var output = new MemoryStream();
            var buffer = new byte[81920];
            while (await input.ReadAsync(buffer, cancellationToken) is var read && read > 0)
            {
                if (output.Length + read > options.MaximumFileBytes) throw new BusinessRuleException("documents.size", "Fichier trop volumineux.");
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
            uploads.Add(new(file.FileName, output.ToArray()));
        }
        var result = await documents.UploadAsync(context.DocumentCollectionId, uploads, cancellationToken);
        logger.LogInformation("Knowledge upload: {Count} documents, {Indexed} indexed, {Failed} failed.", result.Count, result.Count(document => document.Status == "indexed"), result.Count(document => document.Status == "failed"));
        return StatusCode(StatusCodes.Status201Created, result);
    }
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    { await documents.DeleteAsync(context.DocumentCollectionId, id, cancellationToken); logger.LogInformation("Knowledge document {Id} deleted.", id); return NoContent(); }
    [HttpPost("{id}/reindex")]
    public Task<KnowledgeDocument> Reindex(string id, CancellationToken cancellationToken) => documents.ReindexAsync(context.DocumentCollectionId, id, cancellationToken);
    public sealed record RetrievalInput(string Question, int TopK = 5);
    [HttpPost("search")]
    public Task<IReadOnlyList<RetrievedPassage>> Search(RetrievalInput input, CancellationToken cancellationToken)
        => retrieval.RetrieveAsync(new(context.DocumentCollectionId, input.Question, input.TopK), cancellationToken);
}
