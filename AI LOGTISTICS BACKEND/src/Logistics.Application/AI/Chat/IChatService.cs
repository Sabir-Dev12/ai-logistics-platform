namespace Logistics.Application.AI.Chat;

/// <summary>A transport-independent chat use case, callable without an HTTP or SignalR context.</summary>
public interface IChatService
{
    /// <summary>
    /// Sends one message to the configured AI agent. No conversation state is retained.
    /// </summary>
    Task<ChatResponse> SendMessageAsync(ChatRequest request, CancellationToken cancellationToken = default);

    /// <summary>Streams incremental text fragments without retaining the full answer.</summary>
    IAsyncEnumerable<ChatResponse> StreamMessageAsync(ChatRequest request, CancellationToken cancellationToken = default);
}
