using System.Text.Json.Nodes;
using Logistics.Application.Features.Clients;

namespace Logistics.Api.Mcp;

public sealed class GetClientTool(IClientService clients, ILogger<GetClientTool> logger)
    : ReadOnlyMcpTool<ClientDetail>("GetClient", "Read a client by internal key, or by exact client reference when byReference is true. Never guess a key from a reference.",
        McpToolInput.Schema(new JsonObject
        {
            ["keyClient"] = McpToolInput.KeyProperty("Copy the exact client key or reference from the user question."),
            ["byReference"] = new JsonObject { ["type"] = "boolean", ["description"] = "True when the user supplies a client reference (ref client / référence client), rather than an internal key.", ["default"] = false }
        }, "keyClient"), logger)
{
    protected override Task<ClientDetail> ReadAsync(McpToolInput input, CancellationToken cancellationToken)
    {
        input.AllowOnly("keyClient", "byReference");
        var identifier = input.Key("keyClient");
        return input.Boolean("byReference")
            ? clients.GetByReferenceAsync(identifier, cancellationToken)
            : clients.GetAsync(identifier, cancellationToken);
    }
}
