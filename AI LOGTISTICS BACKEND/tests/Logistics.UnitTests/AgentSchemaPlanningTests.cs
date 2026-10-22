using System.Text.Json;
using Logistics.Api.Mcp;
using Logistics.Application.AI;
using Logistics.Application.AI.Agent;
using Logistics.Application.AI.RAG;
using Logistics.Application.Features.Clients;
using Microsoft.Extensions.Logging.Abstractions;

namespace Logistics.UnitTests;

public sealed class AgentSchemaPlanningTests
{
    private const string Question = "donne moi le détails de cette ref client : REF001";
    private const string ReferencePlan = """{"requiresBusinessData":true,"useRag":false,"toolCalls":[{"name":"GetClient","arguments":{"keyClient":"REF001","byReference":true}}]}""";

    [Fact]
    public async Task Client_reference_uses_the_advertised_schema_and_does_not_search_documents()
    {
        var planner = new Planner(_ => ReferencePlan);
        var tools = new Tools();
        var text = await new AgentService(planner, new NoRag(), tools, new Context(), new Audit()).GenerateResponseAsync(Question);
        var call = Assert.Single(tools.Calls);
        Assert.Equal("REF001", call["keyClient"].GetString()); Assert.True(call["byReference"].GetBoolean());
        Assert.Contains("CLI001", text); Assert.DoesNotContain("identifier_required", text);
        Assert.DoesNotContain("documentaire", text);
        var schema = Assert.Single(planner.Schemas);
        var choice = schema.GetProperty("properties").GetProperty("toolCalls").GetProperty("items").GetProperty("anyOf")[0];
        Assert.Equal("GetClient", choice.GetProperty("properties").GetProperty("name").GetProperty("const").GetString());
        Assert.Contains(choice.GetProperty("properties").GetProperty("arguments").GetProperty("required").EnumerateArray(), v => v.GetString() == "keyClient");
        Assert.False(choice.GetProperty("properties").GetProperty("arguments").GetProperty("additionalProperties").GetBoolean());
        Assert.Equal("REF001", choice.GetProperty("properties").GetProperty("arguments").GetProperty("properties").GetProperty("keyClient").GetProperty("enum")[0].GetString());
        Assert.True(choice.GetProperty("properties").GetProperty("arguments").GetProperty("properties").GetProperty("byReference").GetProperty("const").GetBoolean());
        Assert.Equal(1, schema.GetProperty("properties").GetProperty("toolCalls").GetProperty("minItems").GetInt32());
    }

    [Fact]
    public async Task An_invented_key_is_replanned_once_and_only_the_grounded_reference_reaches_mcp()
    {
        var planner = new Planner(index => index == 1 ? ReferencePlan.Replace("REF001", "CLI001") : ReferencePlan);
        var tools = new Tools();
        var text = await new AgentService(planner, new NoRag(), tools, new Context(), new Audit()).GenerateResponseAsync(Question);
        Assert.Equal(2, planner.Schemas.Count);
        Assert.Equal("REF001", Assert.Single(tools.Calls)["keyClient"].GetString());
        Assert.Contains("Source : fiche client", text);
    }

    [Fact]
    public async Task A_model_that_keeps_inventing_keys_never_reaches_mcp()
    {
        var planner = new Planner(_ => ReferencePlan.Replace("REF001", "CLI999"));
        var tools = new Tools();
        var text = await new AgentService(planner, new NoRag(), tools, new Context(), new Audit()).GenerateResponseAsync(Question);
        Assert.Equal(2, planner.Schemas.Count); Assert.Empty(tools.Calls);
        Assert.Contains("indiquez sa clé interne ou sa référence exacte", text); Assert.DoesNotContain("CLI999", text);
    }

    private sealed class Planner(Func<int, string> response) : ILLMService, ISchemaLLMService
    {
        public List<JsonElement> Schemas { get; } = [];
        public Task<string> GenerateJsonAsync(string prompt, JsonElement schema, CancellationToken cancellationToken = default)
        { Schemas.Add(schema.Clone()); return Task.FromResult(response(Schemas.Count)); }
        public Task<string> GenerateJsonAsync(string prompt, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Schema planning must be used.");
        public Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Business facts must not be generated.");
        public IAsyncEnumerable<string> StreamResponseAsync(string prompt, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class Tools : IAgentMcpTools
    {
        public List<IReadOnlyDictionary<string, JsonElement>> Calls { get; } = [];
        public IReadOnlyList<AgentToolDefinition> List()
        {
            var definition = new GetClientTool(null!, NullLogger<GetClientTool>.Instance).Definition;
            return [new(definition.Name, definition.Description!, definition.InputSchema)];
        }
        public Task<AgentToolResult> CallAsync(string name, IReadOnlyDictionary<string, JsonElement> arguments, CancellationToken cancellationToken = default)
        { Calls.Add(arguments); return Task.FromResult(new AgentToolResult(true, JsonSerializer.SerializeToElement(new { keyClient = "CLI001", refClient = "REF001" }))); }
    }
    private sealed class NoRag : IRetrievalService
    {
        public Task<IReadOnlyList<RetrievedPassage>> RetrieveAsync(RetrievalRequest request, CancellationToken cancellationToken = default) => throw new InvalidOperationException("A client detail request does not need documentary evidence.");
    }
    private sealed class Context : IAgentContext { public string DocumentCollectionId => "S01:documents"; }
    private sealed class Audit : IAgentToolAudit { public void Record(AgentToolAuditEvent entry) { } }
}
