using System.Text.Json.Nodes;
using Logistics.Application.Features.Shipments;

namespace Logistics.Api.Mcp;

public sealed class GetShipmentTool(IDossierService shipments, ILogger<GetShipmentTool> logger)
    : ReadOnlyMcpTool<DossierDetail>("GetShipment", "Read an expedition dossier using its identifier, including packages and status.",
        McpToolInput.Schema(new JsonObject { ["keyDossier"] = McpToolInput.KeyProperty("Shipment dossier identifier.") }, "keyDossier"), logger)
{
    protected override Task<DossierDetail> ReadAsync(McpToolInput input, CancellationToken cancellationToken)
    {
        input.AllowOnly("keyDossier");
        return shipments.GetAsync(input.Key("keyDossier"), cancellationToken);
    }
}
