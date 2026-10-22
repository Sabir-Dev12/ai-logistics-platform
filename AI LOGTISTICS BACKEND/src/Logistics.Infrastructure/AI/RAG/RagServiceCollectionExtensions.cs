using Logistics.Application.AI.RAG;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Logistics.Infrastructure.AI.RAG;

public static class RagServiceCollectionExtensions
{
    public static IServiceCollection AddRagBackend(this IServiceCollection services,
        ChunkingOptions? chunkingOptions = null, DocumentLoadingOptions? loadingOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        chunkingOptions ??= new();
        loadingOptions ??= new();
        chunkingOptions.Validate();
        loadingOptions.Validate();
        services.TryAddSingleton(chunkingOptions);
        services.TryAddSingleton(loadingOptions);
        services.TryAddSingleton<IDocumentLoader, TextDocumentLoader>();
        services.TryAddSingleton<IChunkingService, CharacterChunkingService>();
        services.TryAddScoped<IEmbeddingService, UnconfiguredEmbeddingService>();
        services.TryAddSingleton<IVectorStore, InMemoryVectorStore>();
        services.TryAddScoped<IDocumentIndexingService, DocumentIndexingService>();
        services.TryAddScoped<IRetrievalService, RetrievalService>();
        return services;
    }
}
