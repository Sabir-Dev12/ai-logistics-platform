using System.Reflection;
using System.Text;
using Logistics.Api.AI.Agent;
using Logistics.Api.Extensions;
using Logistics.Api.Mcp;
using Logistics.Application.Abstractions;
using Logistics.Application.AI;
using Logistics.Application.AI.Agent;
using Logistics.Application.AI.Chat;
using Logistics.Application.AI.RAG;
using Logistics.Application.Features.Tracking;
using Logistics.Domain.Common;
using Logistics.Infrastructure.AI.RAG;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Logistics.UnitTests;

/// <summary>Exercises production chat, agent, RAG indexing/retrieval and MCP dispatcher together.</summary>
public sealed class ChatResourceIntegrationTests
{
    private const string Question = "Quel est le statut du dossier DOS001 et quelle est la procédure en cas de retard ?";

    [Fact]
    public async Task Chat_uses_both_indexed_rag_passages_and_mcp_business_data_in_its_response()
    {
        var (provider, spy) = Create(); using (provider) using (var scope = provider.CreateScope())
        {
            await Index(scope.ServiceProvider, "S01:documents", "guide", "Vérifier les événements de suivi puis contacter le service logistique.");
            await Index(scope.ServiceProvider, "S99:documents", "other-company", "Document confidentiel d’une autre société.");
            Assert.IsType<ChatService>(scope.ServiceProvider.GetRequiredService<IChatService>());
            Assert.IsType<AgentService>(scope.ServiceProvider.GetRequiredService<IAgentService>());
            Assert.IsType<AgentMcpTools>(scope.ServiceProvider.GetRequiredService<IAgentMcpTools>());
            Assert.IsType<RetrievalService>(scope.ServiceProvider.GetRequiredService<IRetrievalService>());
            var response = await scope.ServiceProvider.GetRequiredService<IChatService>().SendMessageAsync(new(Question));
            Assert.Contains("Source : événements de suivi", response.Message);
            Assert.Contains("DELIVERED", response.Message);
            Assert.Contains("Source documentaire : guide.txt", response.Message);
            Assert.Contains("contacter le service logistique", response.Message);
            Assert.DoesNotContain("confidentiel", response.Message);
            Assert.Equal("GetTimelineAsync", Assert.Single(spy.Calls).Method.Name);
        }
    }

    [Fact]
    public async Task Streaming_keeps_documentary_evidence_when_the_mcp_business_record_is_missing()
    {
        var (provider, spy) = Create();
        spy.Handler = (_, _) => Task.FromException<IReadOnlyList<TrackingEventDto>>(new EntityNotFoundException("Dossier", "private-details"));
        using (provider) using (var scope = provider.CreateScope())
        {
            await Index(scope.ServiceProvider, "S01:documents", "guide", "Procédure documentaire de suivi des expéditions.");
            var fragments = new List<string>();
            await foreach (var response in scope.ServiceProvider.GetRequiredService<IChatService>().StreamMessageAsync(new(Question))) fragments.Add(response.Message);
            var text = string.Concat(fragments);
            Assert.Contains("Je n’ai trouvé aucun résultat", text);
            Assert.DoesNotContain("not_found", text);
            Assert.Contains("Procédure documentaire", text);
            Assert.DoesNotContain("Livré", text); Assert.DoesNotContain("private-details", text);
            Assert.True(fragments.Count > 1);
        }
    }

    [Fact]
    public async Task An_empty_current_company_collection_does_not_use_documents_from_another_company()
    {
        var (provider, _) = Create(); using (provider) using (var scope = provider.CreateScope())
        {
            await Index(scope.ServiceProvider, "S99:documents", "private", "Informations confidentielles étrangères.");
            var response = await scope.ServiceProvider.GetRequiredService<IChatService>().SendMessageAsync(new(Question));
            Assert.Contains("Source : événements de suivi", response.Message);
            Assert.Contains("Aucun passage documentaire pertinent trouvé", response.Message);
            Assert.DoesNotContain("confidentielles", response.Message);
        }
    }

    private static async Task Index(IServiceProvider services, string collection, string id, string text)
    {
        using var content = new MemoryStream(Encoding.UTF8.GetBytes(text));
        await services.GetRequiredService<IDocumentIndexingService>().IndexAsync(collection, new(id, content, id + ".txt"));
    }

    private static (ServiceProvider Provider, McpToolTests.ServiceSpy Spy) Create()
    {
        var tracking = DispatchProxy.Create<ITrackingService, McpToolTests.ServiceSpy>();
        var spy = (McpToolTests.ServiceSpy)(object)tracking;
        spy.Handler = (_, args) => Task.FromResult<IReadOnlyList<TrackingEventDto>>([new(TrackingEntity.Dossier, (string)args[1]!, "DELIVERED", "Livré", false,
            DateTime.UtcNow, DateTime.UtcNow, null, "P001", null, 0, null, "AG01", "S01")]);
        var services = new ServiceCollection(); services.AddLogging();
        services.AddSingleton<IHostApplicationLifetime, FixtureLifetime>();
        services.AddSingleton<ILLMService, Planner>();
        services.AddSingleton<IEmbeddingService, FixtureEmbeddings>();
        services.AddSingleton<ITenantContext, Tenant>();
        services.AddSingleton(tracking);
        services.AddScoped<ILogisticsMcpTool, GetTrackingTool>();
        services.AddScoped<LogisticsMcpDispatcher>();
        services.AddRagBackend(); services.AddChatSignalR();
        return (services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }), spy);
    }

    private sealed class Planner : ILLMService, IStructuredLLMService
    {
        public Task<string> GenerateJsonAsync(string prompt, CancellationToken cancellationToken = default) => Task.FromResult(
            """{"requiresBusinessData":true,"useRag":true,"toolCalls":[{"name":"GetTracking","arguments":{"entity":"Dossier","entityKey":"DOS001"}}]}""");
        public Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Structured source planning must be used.");
        public IAsyncEnumerable<string> StreamResponseAsync(string prompt, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Business evidence must not be replaced by generated values.");
    }
    // Deterministic fixtures only; production uses the configured Ollama embedding model.
    private sealed class FixtureEmbeddings : IEmbeddingService
    {
        public Task<EmbeddingVector> GenerateAsync(string text, CancellationToken cancellationToken = default) => Task.FromResult(new EmbeddingVector("fixture", [1f, 0f]));
    }
    private sealed class Tenant : ITenantContext
    {
        public string SocieteKey => "S01";
        public string? AgenceKey => "AG01";
        public string? UserKey => "P001";
        public string? UserDisplayName => "Tester";
        public string? Workstation => null;
    }
    private sealed class FixtureLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() { }
    }
}
