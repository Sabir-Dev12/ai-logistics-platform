using Logistics.Api.Mcp;
using Logistics.Application.AI.Agent;
using ModelContextProtocol.Protocol;

namespace Logistics.Api.AI.Agent;

/// <summary>Reuses the MCP dispatcher in the current authenticated scope without a loopback HTTP call.</summary>
public sealed class AgentMcpTools(LogisticsMcpDispatcher dispatcher) : IAgentMcpTools
{
    public IReadOnlyList<AgentToolDefinition> List() => dispatcher.List().Tools
        .Select(tool => new AgentToolDefinition(tool.Name, tool.Description ?? "", tool.InputSchema)).ToArray();

    public async Task<AgentToolResult> CallAsync(string name, IReadOnlyDictionary<string, System.Text.Json.JsonElement> arguments,
        CancellationToken cancellationToken = default)
    {
        var result = await dispatcher.CallAsync(new CallToolRequestParams
        { Name = name, Arguments = arguments.ToDictionary(pair => pair.Key, pair => pair.Value) }, cancellationToken);
        if (result.StructuredContent is not { } content || content.ValueKind != System.Text.Json.JsonValueKind.Object
            || !content.TryGetProperty("success", out var success) || success.ValueKind is not (System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False))
            return new(false, null, "invalid_tool_result");
        var correlation = content.TryGetProperty("correlationId", out var correlationId) ? correlationId.GetString() : null;
        if (success.GetBoolean() && result.IsError is not true && content.TryGetProperty("data", out var data) && data.ValueKind != System.Text.Json.JsonValueKind.Null)
            return new(true, data.Clone(), null, correlation);
        var code = content.TryGetProperty("error", out var error) && error.ValueKind == System.Text.Json.JsonValueKind.Object
            && error.TryGetProperty("code", out var errorCode) ? errorCode.GetString() : "tool_failed";
        return new(false, null, code, correlation);
    }
}
