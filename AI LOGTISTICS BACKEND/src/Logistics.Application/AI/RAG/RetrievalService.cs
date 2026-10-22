namespace Logistics.Application.AI.RAG;

public sealed class RetrievalService : IRetrievalService
{
    private readonly IEmbeddingService _embeddings;
    private readonly IVectorStore _vectors;

    public RetrievalService(IEmbeddingService embeddings, IVectorStore vectors)
    {
        ArgumentNullException.ThrowIfNull(embeddings);
        ArgumentNullException.ThrowIfNull(vectors);
        _embeddings = embeddings;
        _vectors = vectors;
    }

    public async Task<IReadOnlyList<RetrievedPassage>> RetrieveAsync(RetrievalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RagValidation.Search(request.CollectionId, request.TopK, request.MinimumScore);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Question);
        cancellationToken.ThrowIfCancellationRequested();
        var query = await _embeddings.GenerateAsync(request.Question, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (query is null) throw new InvalidOperationException("The embedding provider returned no vector.");
        var passages = await _vectors.SearchAsync(new(request.CollectionId, query, request.TopK, request.MinimumScore),
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return passages ?? throw new InvalidOperationException("The vector store returned no result collection.");
    }
}
