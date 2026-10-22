using System.Net;
using System.Text;
using System.Text.Json;
using Logistics.Application.AI;
using Logistics.Application.AI.RAG;
using Logistics.Infrastructure.AI.Ollama;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Logistics.UnitTests;

public sealed class OllamaTests
{
    [Fact]
    public async Task Schema_generation_sends_the_schema_object_instead_of_only_json_mode()
    {
        var handler = new Handler(Json("""{"message":{"content":"{}"},"done":true}"""));
        using var http = new HttpClient(handler);
        var options = new OllamaOptions();
        ISchemaLLMService llm = new OllamaLLMService(new(http, options), options);
        var schema = JsonSerializer.SerializeToElement(new { type = "object", additionalProperties = false });
        await llm.GenerateJsonAsync("plan", schema);
        using var request = JsonDocument.Parse(handler.Body!);
        Assert.Equal(JsonValueKind.Object, request.RootElement.GetProperty("format").ValueKind);
        Assert.Equal(schema.GetRawText(), request.RootElement.GetProperty("format").GetRawText());
    }

    [Fact]
    public async Task Json_generation_uses_local_model_without_thinking_and_preserves_prompt()
    {
        var handler = new Handler(Json("""{"message":{"content":"{\"useRag\":true}"},"done":true}"""));
        using var http = new HttpClient(handler);
        var options = new OllamaOptions();
        var llm = new OllamaLLMService(new(http, options), options);
        Assert.Equal("{\"useRag\":true}", await llm.GenerateJsonAsync("planner input"));
        Assert.Equal("http://127.0.0.1:11434/api/chat", handler.Url);
        using var request = JsonDocument.Parse(handler.Body!);
        Assert.Equal("qwen3:1.7b", request.RootElement.GetProperty("model").GetString());
        Assert.Equal("json", request.RootElement.GetProperty("format").GetString());
        Assert.False(request.RootElement.GetProperty("think").GetBoolean());
        Assert.False(request.RootElement.GetProperty("stream").GetBoolean());
        Assert.Equal("planner input", request.RootElement.GetProperty("messages")[0].GetProperty("content").GetString());
    }

    [Fact]
    public async Task Streaming_delivers_first_delta_before_upstream_completes_and_ignores_thinking()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stream = new IncrementalStream(Encoding.UTF8.GetBytes("{\"message\":{\"content\":\"Bonjour \"},\"done\":false}\n"),
            Encoding.UTF8.GetBytes("{\"message\":{\"content\":\"monde\",\"thinking\":\"private\"},\"done\":true}\n"), release.Task);
        using var http = new HttpClient(new Handler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) }));
        var options = new OllamaOptions();
        await using var iterator = new OllamaLLMService(new(http, options), options).StreamResponseAsync("hello").GetAsyncEnumerator();
        Assert.True(await iterator.MoveNextAsync()); Assert.Equal("Bonjour ", iterator.Current);
        var next = iterator.MoveNextAsync().AsTask(); Assert.False(next.IsCompleted);
        release.SetResult(); Assert.True(await next); Assert.Equal("monde", iterator.Current);
        Assert.False(await iterator.MoveNextAsync());
    }

    [Theory]
    [InlineData("not json", "invalid_response")]
    [InlineData("{\"error\":\"private data\"}", "invalid_response")]
    [InlineData("{\"message\":{\"content\":\"partial\"},\"done\":false}\n", "incomplete_stream")]
    public async Task Invalid_or_incomplete_stream_is_an_error(string body, string code)
    {
        using var http = new HttpClient(new Handler(Json(body)));
        var options = new OllamaOptions();
        var error = await Assert.ThrowsAsync<OllamaException>(async () =>
        { await foreach (var _ in new OllamaLLMService(new(http, options), options).StreamResponseAsync("prompt")) { } });
        Assert.Equal(code, error.Code); Assert.DoesNotContain("private data", error.Message);
    }

    [Fact]
    public async Task Cancellation_releases_upstream_stream()
    {
        using var cancellation = new CancellationTokenSource();
        var stream = new IncrementalStream(Encoding.UTF8.GetBytes("{\"message\":{\"content\":\"first\"},\"done\":false}\n"), [], new TaskCompletionSource().Task);
        using var http = new HttpClient(new Handler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) }));
        var options = new OllamaOptions();
        await using (var iterator = new OllamaLLMService(new(http, options), options).StreamResponseAsync("prompt", cancellation.Token).GetAsyncEnumerator())
        {
            Assert.True(await iterator.MoveNextAsync()); cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await iterator.MoveNextAsync());
        }
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task Embeddings_use_a_separate_model_and_refuse_silent_truncation()
    {
        var handler = new Handler(Json("{\"embeddings\":[[0.1,0.2,0.3]]}"));
        using var http = new HttpClient(handler); var options = new OllamaOptions();
        var vector = await new OllamaEmbeddingService(new(http, options), options).GenerateAsync("document");
        Assert.Equal("ollama:embeddinggemma:300m", vector.ModelId); Assert.Equal(3, vector.Values.Count);
        Assert.EndsWith("/api/embed", handler.Url);
        using var payload = JsonDocument.Parse(handler.Body!); Assert.False(payload.RootElement.GetProperty("truncate").GetBoolean());
    }

    [Theory]
    [InlineData("{\"embeddings\":[]}")]
    [InlineData("{\"embeddings\":[[0,0]]}")]
    [InlineData("{\"embeddings\":[[\"bad\"]]}")]
    public async Task Malformed_embeddings_are_not_fabricated(string body)
    {
        using var http = new HttpClient(new Handler(Json(body))); var options = new OllamaOptions();
        await Assert.ThrowsAsync<OllamaException>(() => new OllamaEmbeddingService(new(http, options), options).GenerateAsync("document"));
    }

    [Fact]
    public async Task Missing_model_has_safe_actionable_error()
    {
        using var http = new HttpClient(new Handler(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("private upstream body") }));
        var options = new OllamaOptions(); var error = await Assert.ThrowsAsync<OllamaException>(() => new OllamaLLMService(new(http, options), options).GenerateResponseAsync("prompt"));
        Assert.Equal("model_not_found", error.Code); Assert.DoesNotContain("private", error.Message);
    }

    [Theory]
    [InlineData("http://example.com:11434/")]
    [InlineData("http://127.0.0.1:11434/api/")]
    public void Nonlocal_or_invalid_origin_is_rejected(string url) => Assert.Throws<ArgumentException>(() => new OllamaOptions { BaseUrl = url }.Validate());

    [Fact]
    public void Di_resolves_ollama_for_both_contracts_and_rejects_cloud_models()
    {
        var services = new ServiceCollection(); services.AddLocalOllama(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider(); using var scope = provider.CreateScope();
        Assert.IsType<OllamaLLMService>(scope.ServiceProvider.GetRequiredService<ILLMService>());
        Assert.IsType<OllamaEmbeddingService>(scope.ServiceProvider.GetRequiredService<IEmbeddingService>());
        Assert.Throws<ArgumentException>(() => new OllamaOptions { ChatModel = "large:cloud" }.Validate());
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    private sealed class Handler(HttpResponseMessage response) : HttpMessageHandler
    {
        public string? Url { get; private set; }
        public string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Url = request.RequestUri!.ToString(); Body = await request.Content!.ReadAsStringAsync(cancellationToken); return response; }
    }
    private sealed class IncrementalStream(byte[] first, byte[] second, Task release) : Stream
    {
        private int phase;
        public bool Disposed { get; private set; }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (phase == 0) { phase++; first.CopyTo(buffer); return first.Length; }
            if (phase == 1) { await release.WaitAsync(cancellationToken); phase++; second.CopyTo(buffer); return second.Length; }
            return 0;
        }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
