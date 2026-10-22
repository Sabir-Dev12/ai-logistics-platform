using Logistics.Application.AI;

namespace Logistics.Infrastructure.AI;

/// <summary>
/// Explicit unavailable-provider fallback. Replace this registration with a real streaming adapter.
/// It never generates simulated production responses or calls an unselected external service.
/// </summary>
public sealed class UnconfiguredLLMService : ILLMService
{
    public Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromException<string>(new LLMNotConfiguredException());
    }

    public IAsyncEnumerable<string> StreamResponseAsync(string prompt, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new LLMNotConfiguredException();
    }
}
