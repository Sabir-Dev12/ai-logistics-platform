namespace Logistics.Infrastructure.AI.Ollama;

/// <summary>Safe diagnostic category: never includes prompts, upstream bodies or business data.</summary>
public sealed class OllamaException(string code) : Exception($"Local Ollama request failed ({code}).")
{
    public string Code { get; } = code;
}
