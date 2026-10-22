namespace Logistics.Application.AI.RAG;

/// <summary>Overlapping text windows. Preserves original content and source offsets, including surrogate pairs.</summary>
public sealed class CharacterChunkingService : IChunkingService
{
    private readonly ChunkingOptions _options;

    public CharacterChunkingService(ChunkingOptions? options = null)
    {
        _options = options ?? new();
        _options.Validate();
    }

    public IReadOnlyList<DocumentChunk> Chunk(RagDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(document.DocumentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(document.Text);
        cancellationToken.ThrowIfCancellationRequested();
        var chunks = new List<DocumentChunk>();
        var text = document.Text;
        for (var start = 0; start < text.Length;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var end = start + Math.Min(_options.ChunkSize, text.Length - start);
            if (end < text.Length && char.IsHighSurrogate(text[end - 1]) && char.IsLowSurrogate(text[end])) end--;
            var content = text[start..end];
            if (!string.IsNullOrWhiteSpace(content))
            {
                if (chunks.Count >= _options.MaximumChunks)
                    throw new InvalidOperationException("The document exceeds the configured chunk limit.");
                chunks.Add(new($"{document.DocumentId}:{chunks.Count}", document.DocumentId, chunks.Count,
                    content, start, end, document.SourceName));
            }
            if (end == text.Length) break;
            var next = Math.Max(start + 1, end - _options.Overlap);
            if (char.IsLowSurrogate(text[next]) && char.IsHighSurrogate(text[next - 1])) next++;
            start = next;
        }
        return chunks.AsReadOnly();
    }
}
