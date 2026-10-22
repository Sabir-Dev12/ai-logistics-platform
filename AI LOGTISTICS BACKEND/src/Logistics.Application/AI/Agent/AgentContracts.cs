using System.Text.Json;

namespace Logistics.Application.AI.Agent;

public interface IAgentService
{
    Task<string> GenerateResponseAsync(string question, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> StreamResponseAsync(string question, CancellationToken cancellationToken = default);
}

public sealed record AgentToolDefinition(string Name, string Description, JsonElement InputSchema);
public sealed record AgentToolResult(bool Success, JsonElement? Data, string? ErrorCode = null, string? CorrelationId = null);

public interface IAgentMcpTools
{
    IReadOnlyList<AgentToolDefinition> List();
    Task<AgentToolResult> CallAsync(string name, IReadOnlyDictionary<string, JsonElement> arguments,
        CancellationToken cancellationToken = default);
}

public interface IAgentContext
{
    /// <summary>Resolved from trusted caller context, never supplied by the model.</summary>
    string DocumentCollectionId { get; }
}

public sealed record AgentToolAuditEvent(string CallId, string Tool, string Outcome, string? ErrorCode, string? CorrelationId);

public interface IAgentToolAudit
{
    /// <summary>Records call lifecycle without logging input arguments, results, prompts or credentials.</summary>
    void Record(AgentToolAuditEvent entry);
}
