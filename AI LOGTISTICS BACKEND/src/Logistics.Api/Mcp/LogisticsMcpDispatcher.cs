using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace Logistics.Api.Mcp;

/// <summary>Explicit read-only allowlist. No reflection discovery of business-service methods.</summary>
public sealed class LogisticsMcpDispatcher(IEnumerable<ILogisticsMcpTool> tools, ILogger<LogisticsMcpDispatcher> logger)
{
    private readonly IReadOnlyDictionary<string, ILogisticsMcpTool> _tools =
        tools.ToDictionary(tool => tool.Definition.Name, StringComparer.Ordinal);

    public ListToolsResult List() => new() { Tools = _tools.Values.Select(tool => tool.Definition).ToList() };

    public Task<CallToolResult> CallAsync(CallToolRequestParams? request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request is null || !_tools.TryGetValue(request.Name, out var tool))
        {
            logger.LogWarning("MCP rejected an unknown tool");
            throw new McpProtocolException("Unknown tool. Use tools/list to discover the available read-only tools.", McpErrorCode.InvalidParams);
        }
        return tool.ExecuteAsync(request.Arguments?.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal), cancellationToken);
    }
}
