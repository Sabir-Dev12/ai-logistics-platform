namespace Logistics.Application.AI.RAG;

public interface IVectorStore
{
    /// <summary>Atomically replaces all chunks of one document in an isolated collection.</summary>
    Task ReplaceDocumentAsync(string collectionId, string documentId, IReadOnlyList<EmbeddedChunk> chunks,
        CancellationToken cancellationToken = default);

    /// <summary>Returns passages sorted by decreasing cosine similarity, filtered by score and bounded by TopK.</summary>
    Task<IReadOnlyList<RetrievedPassage>> SearchAsync(VectorSearchRequest request,
        CancellationToken cancellationToken = default);
}
