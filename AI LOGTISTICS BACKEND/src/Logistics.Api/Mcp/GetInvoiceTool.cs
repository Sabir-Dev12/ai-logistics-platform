using System.Text.Json.Nodes;
using Logistics.Application.Features.Invoicing;

namespace Logistics.Api.Mcp;

public sealed class GetInvoiceTool(IFactureVenteService invoices, ILogger<GetInvoiceTool> logger)
    : ReadOnlyMcpTool<FactureDetail>("GetInvoice", "Read a sales invoice using its identifier, including lines and tax totals.",
        McpToolInput.Schema(new JsonObject { ["keyFacture"] = McpToolInput.KeyProperty("Sales invoice identifier.") }, "keyFacture"), logger)
{
    protected override Task<FactureDetail> ReadAsync(McpToolInput input, CancellationToken cancellationToken)
    {
        input.AllowOnly("keyFacture");
        return invoices.GetAsync(input.Key("keyFacture"), cancellationToken);
    }
}
