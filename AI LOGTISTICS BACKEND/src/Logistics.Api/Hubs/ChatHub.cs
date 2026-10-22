using System.Runtime.CompilerServices;
using Logistics.Application.AI;
using Logistics.Application.AI.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Logistics.Api.Hubs;

/// <summary>SignalR transport adapter. All chat validation and generation belong to IChatService.</summary>
[Authorize]
public sealed class ChatHub : Hub
{
    public const string Endpoint = "/hubs/chat";
    private readonly IChatService _chat;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(IChatService chat, ILogger<ChatHub> logger)
    {
        ArgumentNullException.ThrowIfNull(chat);
        ArgumentNullException.ThrowIfNull(logger);
        _chat = chat;
        _logger = logger;
    }

    /// <summary>
    /// Called with connection.stream("StreamMessage", { message: "..." }).
    /// Each item is a ChatResponse containing a text delta, not the accumulated answer.
    /// SignalR supplies cancellation when the client disposes its stream subscription.
    /// </summary>
    public async IAsyncEnumerable<ChatResponse> StreamMessage(
        ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, Context.ConnectionAborted);
        await using var stream = CreateStream(request, lifetime.Token);

        while (await MoveNextAsync(stream, lifetime.Token).ConfigureAwait(false))
        {
            lifetime.Token.ThrowIfCancellationRequested();
            yield return stream.Current;
        }
    }

    public override Task OnConnectedAsync()
    {
        _logger.LogInformation("Chat connection established: {ConnectionId}.", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Chat connection closed: {ConnectionId}.", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    private IAsyncEnumerator<ChatResponse> CreateStream(ChatRequest request, CancellationToken token)
    {
        try { return _chat.StreamMessageAsync(request, token).GetAsyncEnumerator(token); }
        catch (Exception exception) { throw TransportError(exception, token); }
    }

    private async Task<bool> MoveNextAsync(IAsyncEnumerator<ChatResponse> stream, CancellationToken token)
    {
        try { return await stream.MoveNextAsync().ConfigureAwait(false); }
        catch (Exception exception) { throw TransportError(exception, token); }
    }

    private HubException TransportError(Exception exception, CancellationToken token)
    {
        if (token.IsCancellationRequested && exception is OperationCanceledException)
            return new HubException("chat_cancelled");
        if (exception is ArgumentException)
            return new HubException("chat_invalid_request");
        if (exception is LLMNotConfiguredException)
            return new HubException("chat_llm_not_configured");

        _logger.LogError(exception, "Chat stream failed for connection {ConnectionId}.", Context.ConnectionId);
        return new HubException("chat_generation_failed");
    }
}
