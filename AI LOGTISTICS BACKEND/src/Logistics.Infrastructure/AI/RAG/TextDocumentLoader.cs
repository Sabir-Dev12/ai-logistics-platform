using System.Text;
using Logistics.Application.AI.RAG;

namespace Logistics.Infrastructure.AI.RAG;

/// <summary>Bounded UTF-8 text loading from caller-owned streams. Binary formats require a separate loader adapter.</summary>
public sealed class TextDocumentLoader : IDocumentLoader
{
    private readonly DocumentLoadingOptions _options;

    public TextDocumentLoader(DocumentLoadingOptions? options = null)
    {
        _options = options ?? new();
        _options.Validate();
    }

    public async Task<RagDocument> LoadAsync(DocumentSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(source.DocumentId);
        ArgumentNullException.ThrowIfNull(source.Content);
        if (!source.Content.CanRead) throw new ArgumentException("A readable stream is required.", nameof(source));
        cancellationToken.ThrowIfCancellationRequested();
        using var reader = new StreamReader(source.Content, new UTF8Encoding(false, true),
            detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
        var content = new StringBuilder();
        var buffer = new char[4096];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var remaining = _options.MaximumCharacters - content.Length;
            var read = await reader.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining + 1)), cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (read == 0) break;
            if (read > remaining) throw new InvalidOperationException("The document exceeds the configured text size limit.");
            content.Append(buffer, 0, read);
        }
        var text = content.ToString();
        if (text.StartsWith('\uFEFF')) text = text[1..];
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("The document must contain text.", nameof(source));
        if (text.Any(character => character == '\0')) throw new ArgumentException("The document must be a UTF-8 text document.", nameof(source));
        return new(source.DocumentId, text, source.SourceName);
    }
}
