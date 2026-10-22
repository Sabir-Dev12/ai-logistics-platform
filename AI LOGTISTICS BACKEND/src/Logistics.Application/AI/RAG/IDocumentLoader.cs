namespace Logistics.Application.AI.RAG;

public interface IDocumentLoader
{
    /// <summary>Loads text from a caller-owned stream. The loader must leave that stream open.</summary>
    Task<RagDocument> LoadAsync(DocumentSource source, CancellationToken cancellationToken = default);
}
