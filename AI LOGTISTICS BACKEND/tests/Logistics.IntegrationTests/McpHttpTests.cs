using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using Logistics.Api.Authentication;
using Logistics.Api.Extensions;
using Logistics.Application.Abstractions;
using Logistics.Application.Features.Authentication;
using Logistics.Application.Features.Bookings;
using Logistics.Application.Features.Clients;
using Logistics.Application.Features.Invoicing;
using Logistics.Application.Features.Shipments;
using Logistics.Application.Features.Tracking;
using Logistics.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Client;

namespace Logistics.IntegrationTests;

/// <summary>Real MCP SDK client/server over Kestrel HTTP. Business services are controlled doubles; no database is touched.</summary>
public sealed class McpHttpTests
{
    [Theory]
    [InlineData("2025-11-25")]
    [InlineData("2026-07-28")]
    public async Task Discovers_exactly_five_tools_and_reads_structured_tracking_over_http(string protocol)
    {
        await using var server = await McpTestServer.Start();
        await using var client = await server.Connect("S01", protocol);
        var tools = await client.ListToolsAsync();
        Assert.Equal(new[] { "GetBooking", "GetClient", "GetInvoice", "GetShipment", "GetTracking" }, tools.Select(t => t.Name).Order());
        var result = await client.CallToolAsync("GetTracking", new Dictionary<string, object?>
        {
            ["entity"] = "Dossier", ["entityKey"] = "DOS001", ["take"] = 10
        });
        Assert.False(result.IsError);
        var data = result.StructuredContent!.Value.GetProperty("data")[0];
        Assert.Equal("DOS001", data.GetProperty("cleEntite").GetString());
        Assert.Equal("Dossier", data.GetProperty("entite").GetString());
        Assert.Equal("S01", data.GetProperty("societe").GetString());
    }

    [Fact]
    public async Task Endpoint_requires_a_valid_bearer_token_and_refuses_query_tokens()
    {
        await using var server = await McpTestServer.Start();
        using var http = new HttpClient();
        foreach (var authorization in new[] { "", "Bearer invalid-token" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, server.Url + "/mcp") { Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json") };
            if (authorization.Length > 0) request.Headers.TryAddWithoutValidation("Authorization", authorization);
            using var response = await http.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        using var queryResponse = await http.PostAsync(server.Url + "/mcp?access_token=" + Uri.EscapeDataString(server.Token("S01")), new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Unauthorized, queryResponse.StatusCode);
    }

    [Fact]
    public async Task Rejects_untrusted_origins_and_accepts_the_configured_origin()
    {
        await using var server = await McpTestServer.Start();
        using var http = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, server.Url + "/mcp") { Content = new StringContent("{}") };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", server.Token("S01"));
        request.Headers.Add("Origin", "https://untrusted.example");
        using var response = await http.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var client = await server.Connect("S01", origin: "https://frontend.example");
        Assert.Equal(5, (await client.ListToolsAsync()).Count);
    }

    [Fact]
    public async Task Sql_execution_is_not_a_registered_tool_and_unknown_tools_are_protocol_errors()
    {
        await using var server = await McpTestServer.Start();
        await using var client = await server.Connect("S01");
        var exception = await Assert.ThrowsAsync<McpProtocolException>(() => client.CallToolAsync("ExecuteSql",
            new Dictionary<string, object?> { ["sql"] = "DELETE FROM t_clients" }).AsTask());
        Assert.Contains("Unknown tool", exception.Message);
    }

    [Fact]
    public async Task All_five_tools_report_argument_errors_as_structured_tool_errors()
    {
        await using var server = await McpTestServer.Start();
        await using var client = await server.Connect("S01");
        foreach (var tool in await client.ListToolsAsync())
        {
            var result = await client.CallToolAsync(tool.Name, new Dictionary<string, object?> { ["sql"] = "SELECT private_value" });
            Assert.True(result.IsError);
            Assert.Equal("validation_failed", result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString());
            Assert.DoesNotContain("private_value", result.StructuredContent.Value.GetRawText());
        }
    }

    [Fact]
    public async Task Request_scopes_preserve_signed_caller_identity_without_accepting_a_tenant_override()
    {
        await using var server = await McpTestServer.Start();
        await using var first = await server.Connect("S01");
        await using var second = await server.Connect("S02");
        var arguments = new Dictionary<string, object?> { ["entity"] = "Booking", ["entityKey"] = "BK001" };
        foreach (var (client, tenant) in new[] { (first, "S01"), (second, "S02"), (first, "S01") })
        {
            var result = await client.CallToolAsync("GetTracking", arguments);
            Assert.False(result.IsError);
            Assert.Equal(tenant, result.StructuredContent!.Value.GetProperty("data")[0].GetProperty("societe").GetString());
        }
        arguments["societeKey"] = "S99";
        Assert.True((await first.CallToolAsync("GetTracking", arguments)).IsError);
    }

    [Fact]
    public async Task Service_failures_are_sanitized_and_the_client_can_continue()
    {
        await using var server = await McpTestServer.Start();
        await using var client = await server.Connect("S01");
        foreach (var (key, code) in new[] { ("missing", "not_found"), ("denied", "forbidden"), ("failure", "server_error") })
        {
            var result = await client.CallToolAsync("GetTracking", new Dictionary<string, object?> { ["entity"] = "Dossier", ["entityKey"] = key });
            Assert.True(result.IsError);
            Assert.Equal(code, result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString());
            Assert.DoesNotContain("private", result.StructuredContent.Value.GetRawText());
        }
        Assert.False((await client.CallToolAsync("GetTracking", new Dictionary<string, object?> { ["entity"] = "Dossier", ["entityKey"] = "DOS001" })).IsError);
    }

    private sealed class ScopedTrackingService(ITenantContext tenant) : ITrackingService
    {
        public Task<IReadOnlyList<TrackingEventDto>> GetTimelineAsync(TrackingEntity entity, string entityKey, int take = 100, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entityKey == "missing") throw new EntityNotFoundException("Dossier", "private-key");
            if (entityKey == "denied") throw new ForbiddenException("read", "private-permission");
            if (entityKey == "failure") throw new InvalidOperationException("private-provider-message");
            return Task.FromResult<IReadOnlyList<TrackingEventDto>>([new(entity, entityKey, null, null, false, DateTime.UtcNow,
                DateTime.UtcNow, null, tenant.UserKey, null, 0, null, tenant.AgenceKey, tenant.SocieteKey)]);
        }

        public Task<IReadOnlyList<TrackingEventDto>> GetActivityAsync(IReadOnlyCollection<TrackingEntity>? entities, DateTime? from, DateTime? to, int take = 100, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EntityTrackingStatusDto> GetStatusAsync(TrackingEntity entity, string entityKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<StatutDto>> GetStatutsAsync(string? utilisation = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TrackingEventDto> RecordEventAsync(TrackingEntity entity, string entityKey, RecordTrackingEventRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    public class UnusedBusinessService : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => throw new InvalidOperationException("An invalid request must not invoke a business service.");
    }

    private sealed class McpTestServer(WebApplication app, string url) : IAsyncDisposable
    {
        public string Url { get; } = url;
        public string Token(string company) => app.Services.GetRequiredService<ILogisticsTokenService>()
            .Issue(new(company, "AG01", "P001", "Tester", "MCP-TEST", DateTime.UtcNow.AddMinutes(5)));

        public static async Task<McpTestServer> Start()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Logging.ClearProviders();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:SigningKey"] = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
                ["Authentication:AllowHeaderFallback"] = "false", ["Authentication:TokenLifetimeMinutes"] = "5",
                ["Cors:AllowedOrigins:0"] = "https://frontend.example"
            });
            builder.Services.AddApiAuthentication(builder.Configuration, estDeveloppement: false);
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddApiInfrastructure();
            builder.Services.AddSingleton(DispatchProxy.Create<IClientService, UnusedBusinessService>());
            builder.Services.AddSingleton(DispatchProxy.Create<IFactureVenteService, UnusedBusinessService>());
            builder.Services.AddSingleton(DispatchProxy.Create<IDossierService, UnusedBusinessService>());
            builder.Services.AddSingleton(DispatchProxy.Create<IBookingService, UnusedBusinessService>());
            builder.Services.AddScoped<ITrackingService, ScopedTrackingService>();
            builder.Services.AddLogisticsMcp();
            var app = builder.Build();
            app.UseAuthentication();
            app.UseMiddleware<CallerContextMiddleware>(false);
            app.UseAuthorization();
            app.MapLogisticsMcp();
            try
            {
                await app.StartAsync();
                var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
                return new(app, address);
            }
            catch { await app.DisposeAsync(); throw; }
        }

        public async Task<McpClient> Connect(string company, string? protocol = null, string? origin = null)
        {
            var headers = new Dictionary<string, string> { ["Authorization"] = "Bearer " + Token(company), ["X-Societe-Key"] = "S99" };
            if (origin is not null) headers["Origin"] = origin;
            var transport = new HttpClientTransport(new()
            {
                Endpoint = new(Url + "/mcp"), AdditionalHeaders = headers,
                TransportMode = HttpTransportMode.StreamableHttp
            });
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            try { return await McpClient.CreateAsync(transport, new() { ProtocolVersion = protocol }, cancellationToken: timeout.Token); }
            catch { await transport.DisposeAsync(); throw; }
        }

        public async ValueTask DisposeAsync()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await app.StopAsync(timeout.Token);
            await app.DisposeAsync();
        }
    }
}
