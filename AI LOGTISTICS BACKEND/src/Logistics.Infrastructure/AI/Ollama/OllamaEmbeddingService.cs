using System.Text.Json;
using Logistics.Application.AI.RAG;

namespace Logistics.Infrastructure.AI.Ollama;

public sealed class OllamaEmbeddingService(OllamaClient client, OllamaOptions options) : IEmbeddingService
{
    public async Task<EmbeddingVector> GenerateAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
        using var response = await client.SendAsync("api/embed", new { model = options.EmbeddingModel, input = text, truncate = false, keep_alive = options.KeepAlive }, timeout.Token).ConfigureAwait(false);
        using var document = OllamaClient.Parse(await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false));
        var root = document.RootElement;
        if (!root.TryGetProperty("embeddings", out var vectors) || vectors.ValueKind != JsonValueKind.Array || vectors.GetArrayLength() != 1
            || vectors[0].ValueKind != JsonValueKind.Array) throw new OllamaException("invalid_embedding");
        try
        {
            var values = vectors[0].EnumerateArray().Select(value => value.GetSingle()).ToArray();
            return new EmbeddingVector("ollama:" + options.EmbeddingModel, values);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or FormatException)
        { throw new OllamaException("invalid_embedding"); }
    }
}
