namespace Logistics.Application.AI.RAG;

public sealed class EmbeddingNotConfiguredException() : InvalidOperationException(
    "No embedding provider is configured. Register an IEmbeddingService implementation using the same model/version for indexing and retrieval.");
