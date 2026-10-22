using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Logistics.Infrastructure.AI.Ollama;

public sealed class OllamaClient(HttpClient http, OllamaOptions options)
{
    public async Task<HttpResponseMessage> SendAsync(string path, object payload, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(options.Validate(), path))
        { Content = JsonContent.Create(payload) };
        HttpResponseMessage response;
        try { response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { throw new OllamaException("timeout"); }
        catch (HttpRequestException) { throw new OllamaException("unavailable"); }
        if (!response.IsSuccessStatusCode)
        {
            var code = response.StatusCode == HttpStatusCode.NotFound ? "model_not_found" : "http_error";
            response.Dispose(); throw new OllamaException(code);
        }
        return response;
    }

    public static JsonDocument Parse(string content)
    {
        try
        {
            var json = JsonDocument.Parse(content);
            if (json.RootElement.ValueKind != JsonValueKind.Object || json.RootElement.TryGetProperty("error", out _))
            { json.Dispose(); throw new OllamaException("invalid_response"); }
            return json;
        }
        catch (JsonException) { throw new OllamaException("invalid_response"); }
    }

    public static string Content(JsonElement root)
    {
        if (!root.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object
            || !message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.String)
            throw new OllamaException("invalid_response");
        return content.GetString()!;
    }
}
