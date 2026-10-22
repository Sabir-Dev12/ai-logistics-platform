using System.Security.Claims;
using System.Runtime.CompilerServices;
using Logistics.Api.Hubs;
using Logistics.Application.AI;
using Logistics.Application.AI.Chat;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;

namespace Logistics.UnitTests;

public sealed class ChatHubTests
{
    [Fact]
    public async Task Delegates_to_the_chat_service_and_preserves_stream_items()
    {
        using var disconnected = new CancellationTokenSource();
        var request = new ChatRequest("Question");
        var first = new ChatResponse("first");
        var service = new FakeChat((_, _) => Items(first, new ChatResponse("second")));
        var hub = CreateHub(service, disconnected.Token);
        await using var stream = hub.StreamMessage(request).GetAsyncEnumerator();
        Assert.True(await stream.MoveNextAsync());
        Assert.Same(request, service.Request);
        Assert.Same(first, stream.Current);
        Assert.True(await stream.MoveNextAsync());
        Assert.Equal("second", stream.Current.Message);
        Assert.False(await stream.MoveNextAsync());
    }

    [Fact]
    public async Task Client_cancellation_reaches_the_service()
    {
        using var cancellation = new CancellationTokenSource();
        var service = new FakeChat((_, token) => Waiting(token));
        var hub = CreateHub(service, CancellationToken.None);
        await using var stream = hub.StreamMessage(new ChatRequest("Question"), cancellation.Token).GetAsyncEnumerator();
        Assert.True(await stream.MoveNextAsync());
        var pending = stream.MoveNextAsync().AsTask();
        cancellation.Cancel();
        Assert.Equal("chat_cancelled", (await Assert.ThrowsAsync<HubException>(() => pending)).Message);
        Assert.True(service.Token.IsCancellationRequested);
    }

    [Fact]
    public async Task Disconnect_cancels_generation_and_disposes_the_service_stream()
    {
        using var disconnected = new CancellationTokenSource();
        var disposed = false;
        var service = new FakeChat((_, token) => Waiting(token, () => disposed = true));
        var hub = CreateHub(service, disconnected.Token);
        await hub.OnConnectedAsync();
        await using var stream = hub.StreamMessage(new ChatRequest("Question")).GetAsyncEnumerator();
        Assert.True(await stream.MoveNextAsync());
        var pending = stream.MoveNextAsync().AsTask();
        disconnected.Cancel();
        await Assert.ThrowsAsync<HubException>(() => pending);
        await hub.OnDisconnectedAsync(null);
        Assert.True(service.Token.IsCancellationRequested);
        Assert.True(disposed);
    }

    [Fact]
    public async Task Provider_exception_details_are_not_exposed_to_the_frontend()
    {
        var service = new FakeChat((_, _) => throw new InvalidOperationException("provider-secret-key"));
        var hub = CreateHub(service, CancellationToken.None);
        await using var stream = hub.StreamMessage(new ChatRequest("Question")).GetAsyncEnumerator();
        var error = await Assert.ThrowsAsync<HubException>(() => stream.MoveNextAsync().AsTask());
        Assert.Equal("chat_generation_failed", error.Message);
        Assert.DoesNotContain("provider-secret-key", error.ToString());
    }

    [Fact]
    public async Task Input_validation_is_delegated_and_reported_as_a_transport_error()
    {
        var service = new FakeChat((_, _) => throw new ArgumentException("Application rejected the request."));
        var hub = CreateHub(service, CancellationToken.None);
        var request = new ChatRequest("");
        await using var stream = hub.StreamMessage(request).GetAsyncEnumerator();
        var error = await Assert.ThrowsAsync<HubException>(() => stream.MoveNextAsync().AsTask());
        Assert.Same(request, service.Request);
        Assert.Equal("chat_invalid_request", error.Message);
    }

    [Fact]
    public async Task Missing_provider_configuration_has_an_explicit_error()
    {
        var service = new FakeChat((_, _) => throw new LLMNotConfiguredException());
        var hub = CreateHub(service, CancellationToken.None);
        await using var stream = hub.StreamMessage(new ChatRequest("Question")).GetAsyncEnumerator();
        Assert.Equal("chat_llm_not_configured",
            (await Assert.ThrowsAsync<HubException>(() => stream.MoveNextAsync().AsTask())).Message);
    }

    private static ChatHub CreateHub(IChatService service, CancellationToken disconnected) => new(service, NullLogger<ChatHub>.Instance)
    { Context = new TestContext(disconnected) };

    private static async IAsyncEnumerable<ChatResponse> Items(params ChatResponse[] items)
    {
        await Task.CompletedTask;
        foreach (var item in items) yield return item;
    }

    private static async IAsyncEnumerable<ChatResponse> Waiting(
        [EnumeratorCancellation] CancellationToken token, Action? disposed = null)
    {
        try
        {
            yield return new ChatResponse("first");
            await Task.Delay(Timeout.Infinite, token);
        }
        finally { disposed?.Invoke(); }
    }

    private sealed class FakeChat(Func<ChatRequest, CancellationToken, IAsyncEnumerable<ChatResponse>> stream) : IChatService
    {
        public ChatRequest? Request { get; private set; }
        public CancellationToken Token { get; private set; }
        public Task<ChatResponse> SendMessageAsync(ChatRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Hub must use the streaming use case.");
        public IAsyncEnumerable<ChatResponse> StreamMessageAsync(ChatRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            Token = cancellationToken;
            return stream(request, cancellationToken);
        }
    }

    private sealed class TestContext(CancellationToken disconnected) : HubCallerContext
    {
        public override string ConnectionId => "connection-test";
        public override string? UserIdentifier => "P001";
        public override ClaimsPrincipal? User => new();
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => disconnected;
        public override void Abort() { }
    }
}
