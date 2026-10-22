using System.Data.Common;
using System.Reflection;
using System.Text.Json;
using Logistics.Api.Mcp;
using Logistics.Application.Features.Bookings;
using Logistics.Application.Features.Clients;
using Logistics.Application.Features.Invoicing;
using Logistics.Application.Features.Shipments;
using Logistics.Application.Features.Tracking;
using Logistics.Domain.Common;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace Logistics.UnitTests;

public sealed class McpToolTests
{
    [Theory]
    [InlineData("GetClient")]
    [InlineData("GetInvoice")]
    [InlineData("GetShipment")]
    [InlineData("GetTracking")]
    [InlineData("GetBooking")]
    public async Task Delegates_once_to_the_existing_read_service_and_returns_its_structured_data(string name)
    {
        var test = Create(name);
        using var source = new CancellationTokenSource();
        var result = await test.Tool.ExecuteAsync(test.Arguments, source.Token);
        Assert.False(result.IsError);
        var json = result.StructuredContent!.Value;
        Assert.True(json.GetProperty("success").GetBoolean());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("error").ValueKind);
        Assert.NotEmpty(json.GetProperty("correlationId").GetString()!);
        Assert.Equal(json.GetRawText(), Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
        var call = Assert.Single(test.Spy.Calls);
        Assert.Equal(name == "GetTracking" ? "GetTimelineAsync" : "GetAsync", call.Method.Name);
        Assert.Equal(source.Token, call.Arguments[^1]);
        if (name == "GetTracking")
        {
            Assert.Equal(TrackingEntity.Dossier, call.Arguments[0]);
            Assert.Equal("fixture-key", call.Arguments[1]);
            Assert.Equal(100, call.Arguments[2]);
            Assert.Equal("fixture-key", json.GetProperty("data")[0].GetProperty("cleEntite").GetString());
        }
        else
        {
            Assert.Equal("fixture-key", call.Arguments[0]);
            Assert.Equal("fixture-key", json.GetProperty("data").GetProperty(test.KeyField).GetString());
        }
        Assert.Contains(test.Logs, line => line.Contains("completed"));
    }

    [Theory]
    [InlineData("GetClient")]
    [InlineData("GetInvoice")]
    [InlineData("GetShipment")]
    [InlineData("GetTracking")]
    [InlineData("GetBooking")]
    public async Task Invalid_identifiers_are_rejected_before_calling_the_service(string name)
    {
        foreach (var invalid in new object?[] { null, "", "  ", 123, "line\nbreak", new string('x', 257) })
        {
            var test = Create(name);
            test.Arguments[test.KeyField] = JsonSerializer.SerializeToElement(invalid);
            var result = await test.Tool.ExecuteAsync(test.Arguments);
            AssertError(result, "validation_failed");
            Assert.Equal(test.KeyField, result.StructuredContent!.Value.GetProperty("error").GetProperty("field").GetString());
            Assert.Empty(test.Spy.Calls);
        }
        var missing = Create(name);
        missing.Arguments.Remove(missing.KeyField);
        AssertError(await missing.Tool.ExecuteAsync(missing.Arguments), "validation_failed");
        Assert.Empty(missing.Spy.Calls);
    }

    [Theory]
    [InlineData("GetClient")]
    [InlineData("GetInvoice")]
    [InlineData("GetShipment")]
    [InlineData("GetTracking")]
    [InlineData("GetBooking")]
    public async Task Extra_sql_or_tenant_arguments_are_rejected(string name)
    {
        foreach (var extra in new[] { "sql", "query", "societeKey", "connectionString", "table" })
        {
            var test = Create(name);
            test.Arguments[extra] = JsonSerializer.SerializeToElement("SELECT * FROM private_table");
            AssertError(await test.Tool.ExecuteAsync(test.Arguments), "validation_failed");
            Assert.Empty(test.Spy.Calls);
            Assert.DoesNotContain(test.Logs, line => line.Contains("SELECT") || line.Contains("private_table"));
        }
    }

    [Theory]
    [InlineData("GetClient")]
    [InlineData("GetInvoice")]
    [InlineData("GetShipment")]
    [InlineData("GetTracking")]
    [InlineData("GetBooking")]
    public async Task Maps_service_errors_and_never_exposes_provider_details(string name)
    {
        foreach (var (exception, code) in new (Exception, string)[]
        {
            (new EntityNotFoundException("entity", "private-key"), "not_found"),
            (new ForbiddenException("read", "private-permission"), "forbidden"),
            (new BusinessRuleException("read-rule", "private-rule-details"), "business_rule_violation"),
            (new ChampInvalideException("key", "private-validation-details"), "validation_failed"),
            (new FakeDatabaseException("SELECT private_password FROM private_table"), "database_unavailable"),
            (new InvalidOperationException("private-connection-string"), "server_error")
        })
        {
            var test = Create(name, exception);
            var result = await test.Tool.ExecuteAsync(test.Arguments);
            AssertError(result, code);
            Assert.Single(test.Spy.Calls);
            Assert.DoesNotContain("private", result.StructuredContent!.Value.GetRawText());
            Assert.Contains(test.Logs, line => line.Contains(code));
            Assert.DoesNotContain(test.Logs, line => line.Contains("private"));
        }
    }

    [Theory]
    [InlineData("GetClient")]
    [InlineData("GetInvoice")]
    [InlineData("GetShipment")]
    [InlineData("GetTracking")]
    [InlineData("GetBooking")]
    public async Task Precancelled_calls_do_not_invoke_the_service(string name)
    {
        var test = Create(name);
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => test.Tool.ExecuteAsync(test.Arguments, source.Token));
        Assert.Empty(test.Spy.Calls);
        Assert.Contains(test.Logs, line => line.Contains("cancelled"));
    }

    [Theory]
    [InlineData("GetClient")]
    [InlineData("GetInvoice")]
    [InlineData("GetShipment")]
    [InlineData("GetTracking")]
    [InlineData("GetBooking")]
    public async Task Cancellation_during_service_execution_is_propagated(string name)
    {
        var test = Create(name);
        using var source = new CancellationTokenSource();
        test.Spy.Handler = (method, args) =>
        {
            source.Cancel();
            throw new OperationCanceledException(source.Token);
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => test.Tool.ExecuteAsync(test.Arguments, source.Token));
        Assert.Single(test.Spy.Calls);
    }

    [Theory]
    [InlineData("GetClient")]
    [InlineData("GetInvoice")]
    [InlineData("GetShipment")]
    [InlineData("GetTracking")]
    [InlineData("GetBooking")]
    public void Advertises_a_closed_input_schema_structured_output_and_readonly_annotations(string name)
    {
        var test = Create(name);
        var tool = test.Tool.Definition;
        Assert.Equal(name, tool.Name);
        Assert.Equal("object", tool.InputSchema.GetProperty("type").GetString());
        Assert.False(tool.InputSchema.GetProperty("additionalProperties").GetBoolean());
        var fields = tool.InputSchema.GetProperty("properties");
        Assert.Equal(name == "GetTracking" ? 3 : name == "GetClient" ? 2 : 1, fields.EnumerateObject().Count());
        Assert.Equal("string", fields.GetProperty(test.KeyField).GetProperty("type").GetString());
        Assert.Equal(256, fields.GetProperty(test.KeyField).GetProperty("maxLength").GetInt32());
        Assert.Contains(tool.InputSchema.GetProperty("required").EnumerateArray(), v => v.GetString() == test.KeyField);
        Assert.True(tool.Annotations!.ReadOnlyHint);
        Assert.False(tool.Annotations.DestructiveHint);
        Assert.False(tool.Annotations.OpenWorldHint);
        Assert.True(tool.Annotations.IdempotentHint);
        Assert.Equal("object", tool.OutputSchema!.Value.GetProperty("type").GetString());
        Assert.True(tool.OutputSchema.Value.GetProperty("properties").TryGetProperty("data", out _));
        Assert.Empty(test.Spy.Calls);
    }

    [Fact]
    public async Task Tracking_validates_entity_names_and_limits_and_preserves_the_requested_limit()
    {
        foreach (var value in new object[] { "Dossier; DROP TABLE", "0", "dossier", 2, "Other" })
        {
            var test = Create("GetTracking");
            test.Arguments["entity"] = JsonSerializer.SerializeToElement(value);
            AssertError(await test.Tool.ExecuteAsync(test.Arguments), "validation_failed");
            Assert.Empty(test.Spy.Calls);
        }
        foreach (var value in new object?[] { 0, 501, -1, 1.5, "10", null })
        {
            var test = Create("GetTracking");
            test.Arguments["take"] = JsonSerializer.SerializeToElement(value);
            AssertError(await test.Tool.ExecuteAsync(test.Arguments), "validation_failed");
            Assert.Empty(test.Spy.Calls);
        }
        foreach (var take in new[] { 1, 25, 500 })
        {
            var test = Create("GetTracking");
            test.Arguments["take"] = JsonSerializer.SerializeToElement(take);
            Assert.False((await test.Tool.ExecuteAsync(test.Arguments)).IsError);
            Assert.Equal(take, Assert.Single(test.Spy.Calls).Arguments[2]);
        }
    }

    [Fact]
    public async Task Dispatcher_lists_exactly_five_tools_and_refuses_sql_execution_tools()
    {
        var names = new[] { "GetClient", "GetInvoice", "GetShipment", "GetTracking", "GetBooking" };
        var cases = names.Select(n => Create(n)).ToArray();
        var dispatcher = new LogisticsMcpDispatcher(cases.Select(c => c.Tool), Microsoft.Extensions.Logging.Abstractions.NullLogger<LogisticsMcpDispatcher>.Instance);
        Assert.Equal(names.Order(), dispatcher.List().Tools.Select(t => t.Name).Order());
        foreach (var name in new[] { "ExecuteSql", "SearchDatabase", "GetClient;DROP TABLE", "getclient" })
        {
            var error = await Assert.ThrowsAsync<McpProtocolException>(() => dispatcher.CallAsync(new() { Name = name }, default));
            Assert.Contains("Unknown tool", error.Message);
        }
        Assert.All(cases, test => Assert.Empty(test.Spy.Calls));
        var client = cases[0];
        Assert.False((await dispatcher.CallAsync(new() { Name = "GetClient", Arguments = client.Arguments }, default)).IsError);
        Assert.Single(client.Spy.Calls);
    }

    private static void AssertError(CallToolResult result, string code)
    {
        Assert.True(result.IsError);
        var json = result.StructuredContent!.Value;
        Assert.False(json.GetProperty("success").GetBoolean());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("data").ValueKind);
        Assert.Equal(code, json.GetProperty("error").GetProperty("code").GetString());
        Assert.NotEmpty(json.GetProperty("correlationId").GetString()!);
    }

    private static ToolCase Create(string name, Exception? exception = null) => name switch
    {
        "GetClient" => Create<IClientService, ClientDetail, GetClientTool>("keyClient", """{"keyClient":"fixture-key","societeKey":"S01","raisonSociale":"Client","adresses":[],"banques":[]} """, (s, l) => new(s, l), exception),
        "GetInvoice" => Create<IFactureVenteService, FactureDetail, GetInvoiceTool>("keyFacture", """{"keyFacture":"fixture-key","societeKey":"S01","deviseFacture":"MAD","lignes":[],"tva":[]} """, (s, l) => new(s, l), exception),
        "GetShipment" => Create<IDossierService, DossierDetail, GetShipmentTool>("keyDossier", """{"keyDossier":"fixture-key","societeKey":"S01","colisages":[]} """, (s, l) => new(s, l), exception),
        "GetBooking" => Create<IBookingService, BookingDto, GetBookingTool>("keyBooking", """{"keyBooking":"fixture-key","numBooking":"BK001","keyStatutBooking":"OPEN","equipements":[],"colisages":[]} """, (s, l) => new(s, l), exception),
        "GetTracking" => Tracking(exception),
        _ => throw new ArgumentException(name)
    };

    private static ToolCase Tracking(Exception? exception)
    {
        var test = Create<ITrackingService, IReadOnlyList<TrackingEventDto>, GetTrackingTool>("entityKey", """[{"entite":2,"cleEntite":"fixture-key","societe":"S01"}]""", (s, l) => new(s, l), exception);
        test.Arguments["entity"] = JsonSerializer.SerializeToElement("Dossier");
        return test;
    }

    private static ToolCase Create<TService, TData, TTool>(string field, string json, Func<TService, ILogger<TTool>, TTool> factory, Exception? exception)
        where TService : class where TTool : ILogisticsMcpTool
    {
        var service = DispatchProxy.Create<TService, ServiceSpy>();
        var spy = (ServiceSpy)(object)service;
        var data = JsonSerializer.Deserialize<TData>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        spy.Handler = (_, _) => exception is null ? Task.FromResult(data) : Task.FromException<TData>(exception);
        var logs = new List<string>();
        return new(factory(service, new CaptureLogger<TTool>(logs)), spy, field,
            new Dictionary<string, JsonElement> { [field] = JsonSerializer.SerializeToElement("fixture-key") }, logs);
    }

    private sealed record ToolCase(ILogisticsMcpTool Tool, ServiceSpy Spy, string KeyField,
        Dictionary<string, JsonElement> Arguments, List<string> Logs);

    public class ServiceSpy : DispatchProxy
    {
        public Func<MethodInfo, object?[], object?> Handler { get; set; } = (_, _) => throw new InvalidOperationException();
        public List<(MethodInfo Method, object?[] Arguments)> Calls { get; } = [];
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Calls.Add((targetMethod!, args!));
            return Handler(targetMethod!, args!);
        }
    }

    private sealed class CaptureLogger<T>(List<string> lines) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => lines.Add(formatter(state, exception));
    }

    private sealed class FakeDatabaseException(string message) : DbException(message);
}
