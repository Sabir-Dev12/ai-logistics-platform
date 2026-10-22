using Logistics.Api.Mcp;
using ModelContextProtocol.AspNetCore;

namespace Logistics.Api.Extensions;

public static class McpServiceCollectionExtensions
{
    public const string Endpoint = "/mcp";

    public static IServiceCollection AddLogisticsMcp(this IServiceCollection services)
    {
        services.AddScoped<ILogisticsMcpTool, GetClientTool>();
        services.AddScoped<ILogisticsMcpTool, GetInvoiceTool>();
        services.AddScoped<ILogisticsMcpTool, GetShipmentTool>();
        services.AddScoped<ILogisticsMcpTool, GetTrackingTool>();
        services.AddScoped<ILogisticsMcpTool, GetBookingTool>();
        services.AddScoped<LogisticsMcpDispatcher>();
        services.AddMcpServer(options => options.ServerInfo = new() { Name = "Logistics", Version = "1.0.0" })
            // HTTP request scope preserves the existing authenticated CallerContext and tenant services.
            .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
            .WithListToolsHandler((request, token) =>
            {
                token.ThrowIfCancellationRequested();
                return ValueTask.FromResult(request.Services!.GetRequiredService<LogisticsMcpDispatcher>().List());
            })
            .WithCallToolHandler(async (request, token) =>
                await request.Services!.GetRequiredService<LogisticsMcpDispatcher>().CallAsync(request.Params, token));
        return services;
    }

    public static IEndpointConventionBuilder MapLogisticsMcp(this WebApplication app)
    {
        // MCP requires explicit Origin validation, independently of browser CORS enforcement.
        var origins = app.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        app.UseWhen(context => context.Request.Path.StartsWithSegments(Endpoint), branch => branch.Use(async (context, next) =>
        {
            var origin = context.Request.Headers.Origin;
            if (origin.Count > 0 && (origin.Count != 1 || !origins.Contains(origin[0], StringComparer.Ordinal)))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            await next(context);
        }));
        return app.MapMcp(Endpoint).RequireAuthorization();
    }
}
