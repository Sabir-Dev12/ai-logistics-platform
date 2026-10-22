using Logistics.Application.AI.RAG;

namespace Logistics.Infrastructure.AI.RAG;

public sealed class UnconfiguredEmbeddingService : IEmbeddingService
{
    public Task<EmbeddingVector> GenerateAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        cancellationToken.ThrowIfCancellationRequested();
        throw new EmbeddingNotConfiguredException();
    }
}
