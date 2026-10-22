namespace Logistics.Application.AI.RAG;

public interface IDocumentIndexingService
{
    Task<DocumentIndexingResult> IndexAsync(string collectionId, DocumentSource source,
        CancellationToken cancellationToken = default);
}
