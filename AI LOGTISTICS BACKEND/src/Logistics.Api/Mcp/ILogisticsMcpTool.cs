using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace Logistics.Api.Mcp;

public interface ILogisticsMcpTool
{
    Tool Definition { get; }
    Task<CallToolResult> ExecuteAsync(IReadOnlyDictionary<string, JsonElement>? arguments,
        CancellationToken cancellationToken = default);
}

public sealed record McpToolError(string Code, string Message, string? Field = null);
public sealed record McpToolResponse<T>(bool Success, T? Data, McpToolError? Error, string CorrelationId);
