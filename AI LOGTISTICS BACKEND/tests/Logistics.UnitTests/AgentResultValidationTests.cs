using System.Text.Json;
using Logistics.Application.AI;
using Logistics.Application.AI.Agent;
using Logistics.Application.AI.RAG;

namespace Logistics.UnitTests;

public sealed class AgentResultValidationTests
{
    [Theory]
    [InlineData("null")]
    [InlineData("\"invented amount 999\"")]
    [InlineData("999")]
    public async Task Successful_tools_must_return_structured_data_not_freeform_or_null(string json)
    {
        var agent = new AgentService(new Planner(), new NoRag(), new Tools(json), new Context(), new Audit());
        var text = await agent.GenerateResponseAsync("FAC001 ?");
        Assert.Contains("réponse reçue", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("999", text);
        Assert.Contains("Je ne peux pas en confirmer", text);
    }
    private sealed class Planner : ILLMService
    {
        public Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default) =>
            Task.FromResult("""{"requiresBusinessData":true,"useRag":false,"toolCalls":[{"name":"GetInvoice","arguments":{"keyFacture":"FAC001"}}]}""");
        public IAsyncEnumerable<string> StreamResponseAsync(string prompt, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class Tools(string json) : IAgentMcpTools
    {
        public IReadOnlyList<AgentToolDefinition> List() => [new("GetInvoice", "Invoice", JsonSerializer.SerializeToElement(new { type = "object" }))];
        public Task<AgentToolResult> CallAsync(string name, IReadOnlyDictionary<string, JsonElement> arguments, CancellationToken cancellationToken = default)
            => Task.FromResult(new AgentToolResult(true, JsonSerializer.Deserialize<JsonElement>(json)));
    }
    private sealed class Context : IAgentContext { public string DocumentCollectionId => "S01:documents"; }
    private sealed class Audit : IAgentToolAudit { public void Record(AgentToolAuditEvent entry) { } }
    private sealed class NoRag : IRetrievalService
    {
        public Task<IReadOnlyList<RetrievedPassage>> RetrieveAsync(RetrievalRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
