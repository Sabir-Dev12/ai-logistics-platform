using System.Runtime.CompilerServices;
using Logistics.Application.AI.Agent;

namespace Logistics.Application.AI.Chat;

/// <summary>
/// Validates chat input and delegates source orchestration to the injected AI agent.
/// This service retains no conversation state and has no dependency on a controller or transport.
/// </summary>
public sealed class ChatService : IChatService
{
    private readonly IAgentService _agent;

    public ChatService(IAgentService agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        _agent = agent;
    }

    public async Task<ChatResponse> SendMessageAsync(
        ChatRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        cancellationToken.ThrowIfCancellationRequested();

        // Preserve the user's text, including formatting, for the agent.
        var message = await _agent
            .GenerateResponseAsync(request.Message, cancellationToken)
            .ConfigureAwait(false);

        // Do not return a response for a cancelled call even if the provider ignored cancellation.
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new InvalidOperationException("The agent returned an empty response.");
        }

        return new ChatResponse(message);
    }

    public async IAsyncEnumerable<ChatResponse> StreamMessageAsync(
        ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        cancellationToken.ThrowIfCancellationRequested();
        var containsText = false;

        await foreach (var fragment in _agent.StreamResponseAsync(request.Message, cancellationToken)
            .WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (fragment is null)
                throw new InvalidOperationException("The agent returned a null text fragment.");
            if (fragment.Length == 0)
                continue;

            containsText |= !string.IsNullOrWhiteSpace(fragment);
            yield return new ChatResponse(fragment);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!containsText)
            throw new InvalidOperationException("The agent returned an empty response.");
    }

    private static void ValidateRequest(ChatRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Message))
            throw new ArgumentException("A chat message is required.", nameof(request));
    }
}
