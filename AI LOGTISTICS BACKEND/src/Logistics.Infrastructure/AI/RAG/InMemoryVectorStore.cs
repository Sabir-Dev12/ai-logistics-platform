using Logistics.Application.AI.RAG;

namespace Logistics.Infrastructure.AI.RAG;

/// <summary>Thread-safe baseline store. Collections are isolated; data is lost when the process stops.</summary>
public sealed class InMemoryVectorStore : IVectorStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, CollectionIndex> _collections = new(StringComparer.Ordinal);

    public Task ReplaceDocumentAsync(string collectionId, string documentId, IReadOnlyList<EmbeddedChunk> chunks,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        ArgumentNullException.ThrowIfNull(chunks);
        if (chunks.Count == 0) throw new ArgumentException("At least one chunk is required.", nameof(chunks));
        cancellationToken.ThrowIfCancellationRequested();
        var prepared = chunks.ToArray();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        EmbeddingVector? first = null;
        foreach (var entry in prepared)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(entry);
            ArgumentNullException.ThrowIfNull(entry.Chunk);
            ArgumentNullException.ThrowIfNull(entry.Embedding);
            var chunk = entry.Chunk;
            ArgumentException.ThrowIfNullOrWhiteSpace(chunk.ChunkId);
            ArgumentException.ThrowIfNullOrWhiteSpace(chunk.Text);
            if (chunk.DocumentId != documentId || chunk.Index < 0 || chunk.StartOffset < 0 || chunk.EndOffset < chunk.StartOffset
                || chunk.EndOffset - chunk.StartOffset != chunk.Text.Length || !ids.Add(chunk.ChunkId))
                throw new ArgumentException("Document chunks must have matching identities, valid offsets and unique identifiers.", nameof(chunks));
            first ??= entry.Embedding;
            EnsureCompatible(first, entry.Embedding);
        }
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _collections.TryGetValue(collectionId, out var existing);
            if (existing is not null) EnsureCompatible(existing.VectorSpace, first!);
            var retained = existing?.Chunks.Where(entry => entry.Chunk.DocumentId != documentId) ?? [];
            var replacement = retained.Concat(prepared).ToArray();
            cancellationToken.ThrowIfCancellationRequested();
            _collections[collectionId] = new(first!, replacement);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RetrievedPassage>> SearchAsync(VectorSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CollectionId);
        ArgumentNullException.ThrowIfNull(request.Query);
        if (request.TopK is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(request.TopK));
        if (!double.IsFinite(request.MinimumScore) || request.MinimumScore is < -1 or > 1)
            throw new ArgumentOutOfRangeException(nameof(request.MinimumScore));
        cancellationToken.ThrowIfCancellationRequested();
        CollectionIndex? index;
        lock (_gate) _collections.TryGetValue(request.CollectionId, out index);
        if (index is null) return Task.FromResult<IReadOnlyList<RetrievedPassage>>([]);
        EnsureCompatible(index.VectorSpace, request.Query);
        var candidates = new List<RetrievedPassage>();
        foreach (var entry in index.Chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var score = Similarity(request.Query, entry.Embedding);
            if (score >= request.MinimumScore) candidates.Add(new(entry.Chunk, score));
        }
        var results = candidates.OrderByDescending(result => result.Score)
            .ThenBy(result => result.Chunk.DocumentId, StringComparer.Ordinal).ThenBy(result => result.Chunk.Index)
            .ThenBy(result => result.Chunk.ChunkId, StringComparer.Ordinal).Take(request.TopK).ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<RetrievedPassage>>(Array.AsReadOnly(results));
    }

    private static void EnsureCompatible(EmbeddingVector expected, EmbeddingVector actual)
    {
        if (expected.ModelId != actual.ModelId || expected.Dimensions != actual.Dimensions)
            throw new InvalidOperationException("The embedding model/version or dimensions do not match the collection. Reindex into a separate collection with a consistent model.");
    }

    private static double Similarity(EmbeddingVector query, EmbeddingVector value)
    {
        double dot = 0;
        for (var i = 0; i < query.Dimensions; i++) dot += (double)query.Values[i] * value.Values[i];
        return Math.Clamp(dot / (query.Magnitude * value.Magnitude), -1, 1);
    }

    private sealed record CollectionIndex(EmbeddingVector VectorSpace, EmbeddedChunk[] Chunks);
}
