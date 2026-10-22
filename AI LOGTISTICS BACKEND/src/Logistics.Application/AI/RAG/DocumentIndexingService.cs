namespace Logistics.Application.AI.RAG;

/// <summary>Prepares all embeddings before committing an atomic document replacement to the store.</summary>
public sealed class DocumentIndexingService : IDocumentIndexingService
{
    private readonly IDocumentLoader _loader;
    private readonly IChunkingService _chunking;
    private readonly IEmbeddingService _embeddings;
    private readonly IVectorStore _vectors;

    public DocumentIndexingService(IDocumentLoader loader, IChunkingService chunking,
        IEmbeddingService embeddings, IVectorStore vectors)
    {
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(chunking);
        ArgumentNullException.ThrowIfNull(embeddings);
        ArgumentNullException.ThrowIfNull(vectors);
        _loader = loader;
        _chunking = chunking;
        _embeddings = embeddings;
        _vectors = vectors;
    }

    public async Task<DocumentIndexingResult> IndexAsync(string collectionId, DocumentSource source,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(source.DocumentId);
        cancellationToken.ThrowIfCancellationRequested();
        var document = await _loader.LoadAsync(source, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (document is null || document.DocumentId != source.DocumentId)
            throw new InvalidOperationException("The loader returned an unexpected document.");
        var chunks = _chunking.Chunk(document, cancellationToken);
        if (chunks is null || chunks.Count == 0) throw new InvalidOperationException("The document has no indexable passages.");
        var prepared = new List<EmbeddedChunk>(chunks.Count);
        foreach (var chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var vector = await _embeddings.GenerateAsync(chunk.Text, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (vector is null) throw new InvalidOperationException("The embedding provider returned no vector.");
            prepared.Add(new(chunk, vector));
        }
        // The store checks compatibility and commits all chunks together. A failure must preserve the old document.
        await _vectors.ReplaceDocumentAsync(collectionId, document.DocumentId, prepared.AsReadOnly(), cancellationToken)
            .ConfigureAwait(false);
        var first = prepared[0].Embedding;
        return new(document.DocumentId, prepared.Count, first.ModelId, first.Dimensions);
    }
}
