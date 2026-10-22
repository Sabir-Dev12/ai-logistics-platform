using Logistics.Application.Abstractions;
using Logistics.Application.AI.Agent;

namespace Logistics.Api.AI.Agent;

public sealed class AgentContext(ITenantContext tenant) : IAgentContext
{
    public string DocumentCollectionId => tenant.SocieteKey + ":documents";
}

public sealed class AgentToolAudit(ILogger<AgentToolAudit> logger) : IAgentToolAudit
{
    public void Record(AgentToolAuditEvent entry) => logger.LogInformation(
        "Agent tool call {CallId}: {Tool}, {Outcome}, code {ErrorCode}, MCP correlation {CorrelationId}",
        entry.CallId, entry.Tool, entry.Outcome, entry.ErrorCode, entry.CorrelationId);
}
