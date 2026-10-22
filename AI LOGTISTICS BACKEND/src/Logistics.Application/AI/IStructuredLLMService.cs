namespace Logistics.Application.AI;

/// <summary>Optional provider capability for a JSON-only response, without coupling the agent to a provider.</summary>
public interface IStructuredLLMService
{
    Task<string> GenerateJsonAsync(string prompt, CancellationToken cancellationToken = default);
}

/// <summary>Optional local-provider capability for schema-constrained source planning.</summary>
public interface ISchemaLLMService : IStructuredLLMService
{
    Task<string> GenerateJsonAsync(string prompt, System.Text.Json.JsonElement schema, CancellationToken cancellationToken = default);
}
