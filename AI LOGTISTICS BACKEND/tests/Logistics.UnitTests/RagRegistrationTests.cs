using Logistics.Application.AI.RAG;
using Logistics.Infrastructure.AI.RAG;
using Microsoft.Extensions.DependencyInjection;

namespace Logistics.UnitTests;

public sealed class RagRegistrationTests
{
    [Fact]
    public async Task Default_registration_resolves_the_backend_and_reports_a_missing_embedding_provider()
    {
        var services = new ServiceCollection();
        services.AddRagBackend();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();
        Assert.IsType<TextDocumentLoader>(scope.ServiceProvider.GetRequiredService<IDocumentLoader>());
        Assert.IsType<CharacterChunkingService>(scope.ServiceProvider.GetRequiredService<IChunkingService>());
        Assert.IsType<InMemoryVectorStore>(scope.ServiceProvider.GetRequiredService<IVectorStore>());
        Assert.IsType<DocumentIndexingService>(scope.ServiceProvider.GetRequiredService<IDocumentIndexingService>());
        var retrieval = scope.ServiceProvider.GetRequiredService<IRetrievalService>();
        await Assert.ThrowsAsync<EmbeddingNotConfiguredException>(() => retrieval.RetrieveAsync(new("S01", "Question")));
    }

    [Fact]
    public void Preserves_custom_adapters_and_shares_the_vector_store_across_request_scopes()
    {
        var embeddings = new RagTestDoubles.Embeddings();
        var vectors = new RagTestDoubles.Store();
        var services = new ServiceCollection();
        services.AddSingleton<IEmbeddingService>(embeddings);
        services.AddSingleton<IVectorStore>(vectors);
        services.AddRagBackend();
        services.AddRagBackend();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        Assert.Same(embeddings, first.ServiceProvider.GetRequiredService<IEmbeddingService>());
        Assert.Same(vectors, first.ServiceProvider.GetRequiredService<IVectorStore>());
        Assert.Same(vectors, second.ServiceProvider.GetRequiredService<IVectorStore>());
        Assert.NotSame(first.ServiceProvider.GetRequiredService<IRetrievalService>(), second.ServiceProvider.GetRequiredService<IRetrievalService>());
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IRetrievalService));
    }

    [Fact]
    public async Task An_unconfigured_embedding_adapter_honors_cancellation_before_reporting_configuration()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new UnconfiguredEmbeddingService().GenerateAsync("Question", source.Token));
    }
}
