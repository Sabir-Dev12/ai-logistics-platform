namespace Logistics.Application.AI.RAG;

public sealed record DocumentSource(string DocumentId, Stream Content, string? SourceName = null);
public sealed record RagDocument(string DocumentId, string Text, string? SourceName = null);

/// <summary>Offsets are zero-based UTF-16 positions in the original text; EndOffset is exclusive.</summary>
public sealed record DocumentChunk(string ChunkId, string DocumentId, int Index, string Text,
    int StartOffset, int EndOffset, string? SourceName = null);

public sealed record EmbeddedChunk(DocumentChunk Chunk, EmbeddingVector Embedding);
public sealed record RetrievedPassage(DocumentChunk Chunk, double Score);
public sealed record RetrievalRequest(string CollectionId, string Question, int TopK = 5, double MinimumScore = 0);
public sealed record VectorSearchRequest(string CollectionId, EmbeddingVector Query, int TopK = 5, double MinimumScore = 0);
public sealed record DocumentIndexingResult(string DocumentId, int ChunkCount, string ModelId, int Dimensions);

/// <summary>Owns an immutable, finite, nonzero vector and identifies its embedding model/version.</summary>
public sealed class EmbeddingVector
{
    public EmbeddingVector(string modelId, IReadOnlyList<float> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count is < 1 or > 65_536)
            throw new ArgumentOutOfRangeException(nameof(values), "An embedding must have between 1 and 65536 dimensions.");
        var copy = values.ToArray();
        double sum = 0;
        foreach (var value in copy)
        {
            if (!float.IsFinite(value)) throw new ArgumentException("Embedding values must be finite.", nameof(values));
            sum += (double)value * value;
        }
        if (sum == 0) throw new ArgumentException("An embedding must not be a zero vector.", nameof(values));
        ModelId = modelId;
        Values = Array.AsReadOnly(copy);
        Magnitude = Math.Sqrt(sum);
    }

    public string ModelId { get; }
    public IReadOnlyList<float> Values { get; }
    public int Dimensions => Values.Count;
    public double Magnitude { get; }
}

internal static class RagValidation
{
    public static void Search(string collectionId, int topK, double minimumScore)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);
        if (topK is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(topK), "TopK must be between 1 and 1000.");
        if (!double.IsFinite(minimumScore) || minimumScore is < -1 or > 1)
            throw new ArgumentOutOfRangeException(nameof(minimumScore), "A cosine similarity threshold must be between -1 and 1.");
    }
}
