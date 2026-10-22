using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Logistics.Api.AI.Agent;
using Logistics.Api.Hubs;
using Logistics.Api.Mcp;
using Logistics.Application.AI.Agent;
using Logistics.Application.AI.Chat;
using Logistics.Application.Features.Authentication;
using Logistics.Application.Features.Tracking;
using Logistics.Domain.Common;
using Logistics.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Logistics.UnitTests;

public sealed class AgentMcpAdapterTests
{
    [Fact]
    public async Task Adapter_reuses_the_existing_mcp_tool_and_business_service()
    {
        var (adapter, spy) = Create();
        Assert.Equal("GetTracking", Assert.Single(adapter.List()).Name);
        using var source = new CancellationTokenSource();
        var result = await adapter.CallAsync("GetTracking", Args(new { entity = "Dossier", entityKey = "DOS001" }), source.Token);
        Assert.True(result.Success);
        Assert.Equal("DOS001", result.Data!.Value[0].GetProperty("cleEntite").GetString());
        Assert.NotEmpty(result.CorrelationId!);
        var call = Assert.Single(spy.Calls);
        Assert.Equal("GetTimelineAsync", call.Method.Name);
        Assert.Equal(source.Token, call.Arguments[^1]);
    }

    [Fact]
    public async Task Sql_and_tenant_arguments_are_rejected_by_mcp_before_the_business_service()
    {
        var (adapter, spy) = Create();
        var result = await adapter.CallAsync("GetTracking", Args(new { entity = "Dossier", entityKey = "DOS001", sql = "SELECT private FROM t_clients", societeKey = "S99" }));
        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Equal("validation_failed", result.ErrorCode);
        Assert.Empty(spy.Calls);
    }

    [Fact]
    public async Task Mcp_business_errors_remain_structured_failures_without_provider_details()
    {
        var (adapter, spy) = Create();
        spy.Handler = (_, _) => Task.FromException<IReadOnlyList<TrackingEventDto>>(new EntityNotFoundException("Dossier", "private-key"));
        var result = await adapter.CallAsync("GetTracking", Args(new { entity = "Dossier", entityKey = "DOS001" }));
        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Equal("not_found", result.ErrorCode);
        Assert.DoesNotContain("private", JsonSerializer.Serialize(result));
    }

    [Fact]
    public async Task Signalr_filter_transfers_the_authenticated_caller_into_the_invocation_scope()
    {
        var services = new ServiceCollection();
        services.AddScoped<CallerContext>();
        using var root = services.BuildServiceProvider();
        using var httpScope = root.CreateScope();
        using var invocationScope = root.CreateScope();
        var caller = new CallerIdentity("S01", "AG01", "P001", "Tester", null, DateTime.UtcNow.AddMinutes(5));
        httpScope.ServiceProvider.GetRequiredService<CallerContext>().Caller = caller;
        var http = new DefaultHttpContext { RequestServices = httpScope.ServiceProvider };
        var context = new Caller(http, authenticated: true);
        var hub = new ChatHub(new UnusedChat(), NullLogger<ChatHub>.Instance) { Context = context };
        var invocation = new HubInvocationContext(context, invocationScope.ServiceProvider, hub,
            typeof(ChatHub).GetMethod(nameof(ChatHub.StreamMessage))!, new object?[] { new ChatRequest("Question"), default(CancellationToken) });
        var called = false;
        await new ChatCallerContextFilter().InvokeMethodAsync(invocation, current =>
        {
            var target = current.ServiceProvider.GetRequiredService<CallerContext>();
            Assert.Same(caller, target.Caller);
            Assert.False(target.HeaderFallbackAllowed);
            called = true;
            return ValueTask.FromResult<object?>(null);
        });
        Assert.True(called);
    }

    private static Dictionary<string, JsonElement> Args(object value) => JsonSerializer.SerializeToElement(value)
        .EnumerateObject().ToDictionary(property => property.Name, property => property.Value.Clone());

    private static (AgentMcpTools Adapter, McpToolTests.ServiceSpy Spy) Create()
    {
        var service = DispatchProxy.Create<ITrackingService, McpToolTests.ServiceSpy>();
        var spy = (McpToolTests.ServiceSpy)(object)service;
        spy.Handler = (_, args) => Task.FromResult<IReadOnlyList<TrackingEventDto>>([new(TrackingEntity.Dossier, (string)args[1]!, "DELIVERED", "Livré",
            false, DateTime.UtcNow, DateTime.UtcNow, null, "P001", null, 0, null, "AG01", "S01")]);
        var tool = new GetTrackingTool(service, NullLogger<GetTrackingTool>.Instance);
        var dispatcher = new LogisticsMcpDispatcher([tool], NullLogger<LogisticsMcpDispatcher>.Instance);
        return (new(dispatcher), spy);
    }

    private sealed class Caller : HubCallerContext
    {
        public Caller(HttpContext http, bool authenticated)
        {
            Features.Set<IHttpContextFeature>(new ContextFeature(http));
            User = new ClaimsPrincipal(new ClaimsIdentity([], authenticated ? "fixture" : null));
        }
        public override string ConnectionId => "agent-test";
        public override string? UserIdentifier => "P001";
        public override ClaimsPrincipal? User { get; }
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => default;
        public override void Abort() { }
    }
    private sealed class ContextFeature(HttpContext http) : IHttpContextFeature
    {
        public HttpContext? HttpContext { get; set; } = http;
    }
    private sealed class UnusedChat : IChatService
    {
        public Task<ChatResponse> SendMessageAsync(ChatRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<ChatResponse> StreamMessageAsync(ChatRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
