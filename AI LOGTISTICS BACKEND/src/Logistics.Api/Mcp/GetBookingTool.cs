using System.Text.Json.Nodes;
using Logistics.Application.Features.Bookings;

namespace Logistics.Api.Mcp;

public sealed class GetBookingTool(IBookingService bookings, ILogger<GetBookingTool> logger)
    : ReadOnlyMcpTool<BookingDto>("GetBooking", "Read a booking using its identifier, including equipment and packages.",
        McpToolInput.Schema(new JsonObject { ["keyBooking"] = McpToolInput.KeyProperty("Booking identifier.") }, "keyBooking"), logger)
{
    protected override Task<BookingDto> ReadAsync(McpToolInput input, CancellationToken cancellationToken)
    {
        input.AllowOnly("keyBooking");
        return bookings.GetAsync(input.Key("keyBooking"), cancellationToken);
    }
}
