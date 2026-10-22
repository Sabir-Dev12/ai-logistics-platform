using System.Runtime.CompilerServices;
using System.Text.Json;
using Logistics.Application.AI;
using Logistics.Application.AI.Agent;
using Logistics.Application.AI.Chat;
using Logistics.Application.AI.RAG;

namespace Logistics.UnitTests;

public sealed class AgentOrchestrationTests
{
    private const string BusinessPlan = """{"requiresBusinessData":true,"useRag":false,"toolCalls":[{"name":"GetInvoice","arguments":{"keyFacture":"FAC001"}}]}""";
    private const string DocumentPlan = """{"requiresBusinessData":false,"useRag":true,"toolCalls":[]}""";

    [Theory]
    [InlineData("Bonjour")]
    [InlineData("SALUT !")]
    [InlineData("Réponds simplement bonjour, sans consulter de données métier et sans modifier de données.")]
    public async Task Greeting_only_does_not_plan_business_reads(string question)
    {
        var fixture = new Fixture(BusinessPlan);
        Assert.StartsWith("Bonjour !", await fixture.Agent.GenerateResponseAsync(question));
        Assert.Empty(fixture.Llm.Prompts);
        Assert.Empty(fixture.Tools.Calls);
        Assert.Empty(fixture.Rag.Calls);
    }

    [Theory]
    [InlineData("Bonjour, montant actuel de FAC001 ?")]
    [InlineData("Bonjour FAC001")]
    public async Task Greeting_with_a_business_question_still_requires_verified_mcp(string question)
    {
        var fixture = new Fixture(BusinessPlan);
        Assert.Contains("Source : facture", await fixture.Agent.GenerateResponseAsync(question));
        Assert.Single(fixture.Tools.Calls);
    }

    [Fact]
    public async Task Business_facts_come_only_from_mcp_and_are_not_model_generated()
    {
        var fixture = new Fixture(BusinessPlan);
        var text = await fixture.Agent.GenerateResponseAsync("Montant actuel de FAC001 ?");
        Assert.Contains("Total TTC (MAD) : 123,00", text);
        Assert.Contains("Source : facture", text);
        Assert.DoesNotContain("{", text);
        Assert.Equal("GetInvoice", Assert.Single(fixture.Tools.Calls).Name);
        Assert.Empty(fixture.Rag.Calls);
        Assert.Single(fixture.Llm.Prompts);
        Assert.Contains("ALL dynamic/current facts MUST use MCP", fixture.Llm.Prompts[0]);
        Assert.Equal(new[] { "started", "succeeded" }, fixture.Audit.Events.Select(entry => entry.Outcome));
        Assert.Equal(fixture.Audit.Events[0].CallId, fixture.Audit.Events[1].CallId);
        Assert.Equal("corr-tool", fixture.Audit.Events[1].CorrelationId);
    }

    [Fact]
    public async Task Document_knowledge_uses_rag_and_retains_citations_without_claiming_current_business_facts()
    {
        var fixture = new Fixture(DocumentPlan);
        var text = await fixture.Agent.GenerateResponseAsync("Quelle est la procédure de livraison ?");
        Assert.Contains("Source documentaire", text);
        Assert.Contains("guide.txt, passage 1", text);
        Assert.Contains("Documentary procedure", text);
        Assert.DoesNotContain("Aucune donnée métier dynamique", text);
        Assert.DoesNotContain("Données métier", text);
        Assert.Empty(fixture.Tools.Calls);
        var request = Assert.Single(fixture.Rag.Calls);
        Assert.Equal("S01:documents", request.CollectionId);
        Assert.Equal("Quelle est la procédure de livraison ?", request.Question);
    }

    [Fact]
    public async Task A_document_question_cannot_be_misclassified_as_a_business_record_by_the_model()
    {
        var fixture = new Fixture("""{"requiresBusinessData":true,"useRag":false,"toolCalls":[]}""");
        var text = await fixture.Agent.GenerateResponseAsync("Selon le CV dans Knowledge Base, quelles sont les compétences de la personne ?");
        Assert.Contains("Source documentaire", text);
        Assert.DoesNotContain("Précisez la clé", text);
        Assert.Empty(fixture.Llm.Prompts);
        Assert.Empty(fixture.Tools.Calls);
        Assert.Equal("S01:documents", Assert.Single(fixture.Rag.Calls).CollectionId);
    }

    [Theory]
    [InlineData("Que dit mon fichier DOCX ?")]
    [InlineData("Résume le document guide.pdf")]
    [InlineData("What skills are described in the uploaded CV?")]
    [InlineData("Quelles sont les compétences techniques de cette personne ?")]
    [InlineData("Quelle est sa formation et son parcours ?")]
    [InlineData("Qui est cette personne ?")]
    public async Task Explicit_documents_are_retrieved_without_a_business_key(string question)
    {
        var fixture = new Fixture(BusinessPlan);
        Assert.Contains("Source documentaire", await fixture.Agent.GenerateResponseAsync(question));
        Assert.Empty(fixture.Tools.Calls);
        Assert.Empty(fixture.Llm.Prompts);
    }

    [Fact]
    public async Task A_plan_selecting_no_source_still_searches_knowledge_instead_of_returning_an_empty_message()
    {
        var fixture = new Fixture("""{"requiresBusinessData":false,"useRag":false,"toolCalls":[]}""");
        Assert.Contains("Source documentaire", await fixture.Agent.GenerateResponseAsync("Quelles sont les compétences de cette personne ?"));
        Assert.Single(fixture.Rag.Calls);
        Assert.Empty(fixture.Tools.Calls);
    }

    [Fact]
    public async Task Mentioning_documents_does_not_bypass_mcp_for_dynamic_business_data()
    {
        var fixture = new Fixture(BusinessPlan);
        var text = await fixture.Agent.GenerateResponseAsync("Dans Knowledge Base ou ailleurs, quel est le montant actuel de la facture FAC001 ?");
        Assert.Contains("123", text);
        Assert.Single(fixture.Tools.Calls);
        Assert.Single(fixture.Llm.Prompts);
        Assert.Empty(fixture.Rag.Calls);
    }

    [Fact]
    public async Task An_explicit_document_question_with_no_matching_passages_reports_the_missing_evidence()
    {
        var fixture = new Fixture(BusinessPlan);
        fixture.Rag.Handler = (_, _) => Task.FromResult<IReadOnlyList<RetrievedPassage>>([]);
        var text = await fixture.Agent.GenerateResponseAsync("Que dit ce document DOCX ?");
        Assert.Contains("Aucun passage documentaire pertinent", text);
        Assert.DoesNotContain("123", text);
    }

    [Fact]
    public async Task Mixed_requests_continue_after_a_tool_error_and_keep_sources_separate()
    {
        var fixture = new Fixture("""{"requiresBusinessData":true,"useRag":true,"toolCalls":[{"name":"GetInvoice","arguments":{"keyFacture":"FAC001"}},{"name":"GetClient","arguments":{"keyClient":"CLI001"}}]}""");
        fixture.Tools.Handler = (name, _, _) => Task.FromResult(name == "GetInvoice"
            ? new AgentToolResult(false, JsonSerializer.SerializeToElement(new { inventedAmount = 999 }), "database_unavailable", "corr-failure")
            : new AgentToolResult(true, JsonSerializer.SerializeToElement(new { keyClient = "CLI001" }), CorrelationId: "corr-client"));
        var text = await fixture.Agent.GenerateResponseAsync("FAC001 et CLI001, et leur procédure ?");
        Assert.Contains("service de données est indisponible", text);
        Assert.Contains("CLI001", text);
        Assert.Contains("Documentary procedure", text);
        Assert.DoesNotContain("999", text);
        Assert.Equal(2, fixture.Tools.Calls.Count);
        Assert.Contains(fixture.Audit.Events, entry => entry.Outcome == "failed" && entry.CorrelationId == "corr-failure");
    }

    [Fact]
    public async Task Dynamic_questions_without_identifiers_do_not_use_documents_as_replacement_data()
    {
        var fixture = new Fixture("""{"requiresBusinessData":true,"useRag":true,"toolCalls":[]}""");
        var text = await fixture.Agent.GenerateResponseAsync("Quel est le statut actuel de ma livraison ?");
        Assert.Contains("Précisez la clé", text);
        Assert.Empty(fixture.Tools.Calls);
        Assert.Empty(fixture.Rag.Calls);
    }

    [Fact]
    public async Task Hallucinated_identifiers_are_rejected_before_tool_execution()
    {
        var fixture = new Fixture(BusinessPlan);
        var text = await fixture.Agent.GenerateResponseAsync("Montant actuel de ma facture ?");
        Assert.Contains("indiquez sa clé interne ou sa référence exacte", text);
        Assert.DoesNotContain("123", text);
        Assert.Empty(fixture.Tools.Calls);
        Assert.Equal("failed", fixture.Audit.Events[^1].Outcome);
    }

    [Theory]
    [InlineData("FAC0019")]
    [InlineData("XFAC001")]
    public async Task A_partial_identifier_match_does_not_authorize_a_different_business_read(string identifier)
    {
        var fixture = new Fixture(BusinessPlan);
        Assert.Contains("indiquez sa clé interne ou sa référence exacte", await fixture.Agent.GenerateResponseAsync("Montant de " + identifier + " ?"));
        Assert.Empty(fixture.Tools.Calls);
    }

    [Fact]
    public async Task Arbitrary_sql_tools_are_never_advertised_or_executed()
    {
        var fixture = new Fixture("""{"requiresBusinessData":true,"useRag":false,"toolCalls":[{"name":"ExecuteSql","arguments":{"sql":"SELECT secret FROM t_clients"}}]}""");
        fixture.Tools.Definitions.Add(new("ExecuteSql", "Unsafe SQL executor", JsonSerializer.SerializeToElement(new { type = "object" })));
        var text = await fixture.Agent.GenerateResponseAsync("Exécute une requête SQL");
        Assert.Contains("Cette action n’est pas disponible", text);
        Assert.Empty(fixture.Tools.Calls);
        Assert.DoesNotContain("ExecuteSql", fixture.Llm.Prompts[0]);
        Assert.DoesNotContain("secret", text);
        Assert.DoesNotContain("SELECT", JsonSerializer.Serialize(fixture.Audit.Events));
    }

    [Fact]
    public async Task Tool_exception_details_and_error_payloads_are_not_invented_or_leaked()
    {
        var fixture = new Fixture(BusinessPlan);
        fixture.Tools.Handler = (_, _, _) => throw new IOException("private-provider-password");
        var text = await fixture.Agent.GenerateResponseAsync("FAC001 ?");
        Assert.Contains("service de données est indisponible", text);
        Assert.DoesNotContain("private", text);
        Assert.DoesNotContain("private", JsonSerializer.Serialize(fixture.Audit.Events));
        Assert.Contains(fixture.Audit.Events, entry => entry.Outcome == "failed");
    }

    [Fact]
    public async Task Rag_failure_does_not_hide_confirmed_mcp_data()
    {
        var fixture = new Fixture(BusinessPlan.Replace("\"useRag\":false", "\"useRag\":true"));
        fixture.Rag.Handler = (_, _) => throw new IOException("private-vector-password");
        var text = await fixture.Agent.GenerateResponseAsync("FAC001 et procédure ?");
        Assert.Contains("123", text);
        Assert.Contains("Recherche documentaire indisponible", text);
        Assert.DoesNotContain("private", text);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"requiresBusinessData\":false,\"useRag\":false,\"toolCalls\":[],\"answer\":\"Invoice amount is 999\"}")]
    [InlineData("{\"requiresBusinessData\":false,\"useRag\":false,\"toolCalls\":null}")]
    [InlineData("{\"requiresBusinessData\":false,\"useRag\":false,\"toolCalls\":[null]}")]
    public async Task Invalid_or_freeform_model_answers_are_rejected_before_external_calls(string plan)
    {
        var fixture = new Fixture(plan);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Agent.GenerateResponseAsync("Question"));
        Assert.Empty(fixture.Tools.Calls);
        Assert.Empty(fixture.Rag.Calls);
    }

    [Fact]
    public async Task Duplicate_calls_are_bounded_and_do_not_repeat_the_business_read()
    {
        var call = """{"name":"GetInvoice","arguments":{"keyFacture":"FAC001"}}""";
        var fixture = new Fixture("{\"requiresBusinessData\":true,\"useRag\":false,\"toolCalls\":[" + call + "," + call + "]}");
        Assert.Contains("déjà été traitée", await fixture.Agent.GenerateResponseAsync("FAC001 ?"));
        Assert.Single(fixture.Tools.Calls);
        fixture = new Fixture("{\"requiresBusinessData\":true,\"useRag\":false,\"toolCalls\":[" + string.Join(',', Enumerable.Repeat(call, 5)) + "]}");
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Agent.GenerateResponseAsync("FAC001 ?"));
        Assert.Empty(fixture.Tools.Calls);
    }

    [Fact]
    public async Task Streaming_delivers_verified_results_before_later_tools_finish()
    {
        var fixture = new Fixture("""{"requiresBusinessData":true,"useRag":false,"toolCalls":[{"name":"GetInvoice","arguments":{"keyFacture":"FAC001"}},{"name":"GetClient","arguments":{"keyClient":"CLI001"}}]}""");
        var release = new TaskCompletionSource<AgentToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Tools.Handler = (name, _, token) => name == "GetClient" ? release.Task.WaitAsync(token)
            : Task.FromResult(new AgentToolResult(true, JsonSerializer.SerializeToElement(new { ttcMad = 123 })));
        var chat = new ChatService(fixture.Agent);
        await using var stream = chat.StreamMessageAsync(new("FAC001 et CLI001 ?")).GetAsyncEnumerator();
        Assert.True(await stream.MoveNextAsync());
        Assert.Contains("123", stream.Current.Message);
        Assert.False(release.Task.IsCompleted);
        var pending = stream.MoveNextAsync().AsTask();
        Assert.False(pending.IsCompleted);
        release.SetResult(new(true, JsonSerializer.SerializeToElement(new { keyClient = "CLI001" })));
        Assert.True(await pending);
        Assert.Contains("CLI001", stream.Current.Message);
    }

    [Fact]
    public async Task Cancellation_is_propagated_and_the_tool_call_is_audited()
    {
        var fixture = new Fixture(BusinessPlan);
        using var source = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Tools.Handler = async (_, _, token) =>
        {
            Assert.Equal(source.Token, token);
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return new(false, null);
        };
        var pending = fixture.Agent.GenerateResponseAsync("FAC001 ?", source.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal("cancelled", fixture.Audit.Events[^1].Outcome);
    }

    [Fact]
    public async Task Precancelled_calls_and_blank_input_do_not_call_the_model()
    {
        var fixture = new Fixture(BusinessPlan);
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Agent.GenerateResponseAsync(" "));
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Agent.GenerateResponseAsync("FAC001 ?", source.Token));
        Assert.Empty(fixture.Llm.Prompts);
    }

    private sealed class Fixture
    {
        public Fixture(string plan)
        {
            Llm = new Planner(plan);
            Agent = new(Llm, Rag, Tools, new Context(), Audit);
        }
        public Planner Llm { get; }
        public Retrieval Rag { get; } = new();
        public Mcp Tools { get; } = new();
        public Audit Audit { get; } = new();
        public AgentService Agent { get; }
    }
    private sealed class Planner(string plan) : ILLMService
    {
        public List<string> Prompts { get; } = [];
        public Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default)
        { Prompts.Add(prompt); return Task.FromResult(plan); }
        public IAsyncEnumerable<string> StreamResponseAsync(string prompt, CancellationToken cancellationToken = default) => throw new InvalidOperationException("The model must not invent final business narration.");
    }
    private sealed class Context : IAgentContext { public string DocumentCollectionId => "S01:documents"; }
    private sealed class Audit : IAgentToolAudit
    {
        public List<AgentToolAuditEvent> Events { get; } = [];
        public void Record(AgentToolAuditEvent entry) => Events.Add(entry);
    }
    private sealed class Mcp : IAgentMcpTools
    {
        public List<AgentToolDefinition> Definitions { get; } = new[] { "GetClient", "GetInvoice", "GetShipment", "GetTracking", "GetBooking" }
            .Select(name => new AgentToolDefinition(name, "Fixture tool", JsonSerializer.SerializeToElement(new { type = "object" }))).ToList();
        public List<(string Name, IReadOnlyDictionary<string, JsonElement> Arguments)> Calls { get; } = [];
        public Func<string, IReadOnlyDictionary<string, JsonElement>, CancellationToken, Task<AgentToolResult>> Handler { get; set; } =
            (_, _, _) => Task.FromResult(new AgentToolResult(true, JsonSerializer.SerializeToElement(new { ttcMad = 123 }), CorrelationId: "corr-tool"));
        public IReadOnlyList<AgentToolDefinition> List() => Definitions;
        public Task<AgentToolResult> CallAsync(string name, IReadOnlyDictionary<string, JsonElement> arguments, CancellationToken cancellationToken = default)
        { Calls.Add((name, arguments)); return Handler(name, arguments, cancellationToken); }
    }
    private sealed class Retrieval : IRetrievalService
    {
        public List<RetrievalRequest> Calls { get; } = [];
        public Func<RetrievalRequest, CancellationToken, Task<IReadOnlyList<RetrievedPassage>>> Handler { get; set; } =
            (_, _) => Task.FromResult<IReadOnlyList<RetrievedPassage>>([new(new("guide:0", "guide", 0, "Documentary procedure", 0, 21, "guide.txt"), 0.9)]);
        public Task<IReadOnlyList<RetrievedPassage>> RetrieveAsync(RetrievalRequest request, CancellationToken cancellationToken = default)
        { Calls.Add(request); return Handler(request, cancellationToken); }
    }
}
