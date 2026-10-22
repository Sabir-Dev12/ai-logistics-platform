namespace Logistics.Application.AI;

/// <summary>The composition root has not been supplied with a real language-model provider.</summary>
public sealed class LLMNotConfiguredException : InvalidOperationException
{
    public LLMNotConfiguredException() : base("No language-model provider is configured.") { }
}
