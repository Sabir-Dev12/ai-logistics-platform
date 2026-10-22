namespace Logistics.Application.AI.RAG;

public interface IEmbeddingService
{
    /// <summary>Uses the same model/version and dimensions for documents and questions. No hashing or fabricated embeddings.</summary>
    Task<EmbeddingVector> GenerateAsync(string text, CancellationToken cancellationToken = default);
}
