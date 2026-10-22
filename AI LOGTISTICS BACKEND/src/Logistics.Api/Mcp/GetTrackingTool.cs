using System.Text.Json.Nodes;
using Logistics.Application.Features.Tracking;

namespace Logistics.Api.Mcp;

public sealed class GetTrackingTool(ITrackingService tracking, ILogger<GetTrackingTool> logger)
    : ReadOnlyMcpTool<IReadOnlyList<TrackingEventDto>>("GetTracking", "Read the latest tracking events for an entity, most recent first. An empty array means no visible events.",
        McpToolInput.Schema(new JsonObject
        {
            ["entity"] = new JsonObject
            {
                ["type"] = "string", ["description"] = "Tracked entity kind. Use Dossier for a shipment.",
                ["enum"] = new JsonArray(Enum.GetNames<TrackingEntity>().Select(n => (JsonNode?)JsonValue.Create(n)).ToArray())
            },
            ["entityKey"] = McpToolInput.KeyProperty("Identifier of the tracked entity."),
            ["take"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1, ["maximum"] = 500, ["default"] = 100 }
        }, "entity", "entityKey"), logger)
{
    protected override Task<IReadOnlyList<TrackingEventDto>> ReadAsync(McpToolInput input, CancellationToken cancellationToken)
    {
        input.AllowOnly("entity", "entityKey", "take");
        return tracking.GetTimelineAsync(input.EnumName<TrackingEntity>("entity"), input.Key("entityKey"), input.Take(), cancellationToken);
    }
}
