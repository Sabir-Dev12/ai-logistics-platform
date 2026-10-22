using System.Runtime.CompilerServices;
using Logistics.Application.AI;

namespace Logistics.Infrastructure.AI.Ollama;

public sealed class OllamaLLMService(OllamaClient client, OllamaOptions options) : ILLMService, ISchemaLLMService
{
    private object Payload(string prompt, bool stream, bool json, System.Text.Json.JsonElement? schema = null)
    {
        var payload = new Dictionary<string, object>
        {
            ["model"] = options.ChatModel,
            ["messages"] = new[] { new { role = "user", content = prompt } },
            ["stream"] = stream, ["think"] = false,
            ["keep_alive"] = options.KeepAlive,
            ["options"] = new { num_ctx = options.ContextSize, num_predict = options.MaximumOutputTokens, temperature = 0.1 }
        };
        if (schema is { } value) payload["format"] = value;
        else if (json) payload["format"] = "json";
        return payload;
    }

    public Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default) => Generate(prompt, false, cancellationToken);
    public Task<string> GenerateJsonAsync(string prompt, CancellationToken cancellationToken = default) => Generate(prompt, true, cancellationToken);
    public Task<string> GenerateJsonAsync(string prompt, System.Text.Json.JsonElement schema, CancellationToken cancellationToken = default) => Generate(prompt, true, cancellationToken, schema);

    private async Task<string> Generate(string prompt, bool json, CancellationToken token, System.Text.Json.JsonElement? schema = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
        using var response = await client.SendAsync("api/chat", Payload(prompt, false, json, schema), timeout.Token).ConfigureAwait(false);
        using var document = OllamaClient.Parse(await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false));
        var root = document.RootElement;
        if (!root.TryGetProperty("done", out var done) || done.ValueKind != System.Text.Json.JsonValueKind.True)
            throw new OllamaException("incomplete_response");
        if (root.TryGetProperty("done_reason", out var reason) && reason.GetString() == "length") throw new OllamaException("output_limit");
        return OllamaClient.Content(root);
    }

    public async IAsyncEnumerable<string> StreamResponseAsync(string prompt, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
        using var response = await client.SendAsync("api/chat", Payload(prompt, true, false), timeout.Token).ConfigureAwait(false);
        await using var body = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
        using var reader = new StreamReader(body);
        while (await reader.ReadLineAsync(timeout.Token).ConfigureAwait(false) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var document = OllamaClient.Parse(line);
            var root = document.RootElement;
            if (!root.TryGetProperty("done", out var done) || done.ValueKind is not (System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False))
                throw new OllamaException("invalid_response");
            var content = OllamaClient.Content(root);
            if (content.Length > 0) yield return content;
            if (done.GetBoolean())
            {
                if (root.TryGetProperty("done_reason", out var reason) && reason.GetString() == "length") throw new OllamaException("output_limit");
                yield break;
            }
        }
        throw new OllamaException("incomplete_stream");
    }
}
