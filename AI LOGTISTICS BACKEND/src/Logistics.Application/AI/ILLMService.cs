namespace Logistics.Application.AI;

/// <summary>
/// Provider-independent contract for generating a textual response with a language model.
/// Implementations own provider configuration, credentials and network calls.
/// </summary>
public interface ILLMService
{
    /// <summary>
    /// Generates a response to the supplied prompt. Provider failures must be reported as exceptions,
    /// and implementations should honour <paramref name="cancellationToken"/>.
    /// </summary>
    Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Yields text fragments as the provider generates them, preserving order and whitespace.
    /// Implementations must honour cancellation and release the upstream response when disposed.
    /// This contract must not be implemented by splitting a previously buffered full response.
    /// </summary>
    IAsyncEnumerable<string> StreamResponseAsync(string prompt, CancellationToken cancellationToken = default);
}
