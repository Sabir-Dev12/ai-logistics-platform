using Logistics.Application.AI.RAG;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Logistics.Infrastructure.AI.RAG;

public static class KnowledgeBaseServiceCollectionExtensions
{
    public static IServiceCollection AddKnowledgeBase(this IServiceCollection services, IConfiguration configuration, string contentRoot)
    {
        var options = configuration.GetSection("AI:KnowledgeBase").Get<KnowledgeBaseOptions>() ?? new();
        if (options.MaximumFileBytes < 1 || options.MaximumBatchFiles is < 1 or > 10 || options.MaximumDocuments < 1 || options.MaximumCollectionBytes < options.MaximumFileBytes)
            throw new ArgumentException("Invalid knowledge base limits.");
        options.DataDirectory = Path.GetFullPath(options.DataDirectory, contentRoot);
        services.AddSingleton(options);
        services.AddSingleton<FileKnowledgeStore>();
        services.AddSingleton<IKnowledgeDocumentStore>(provider => provider.GetRequiredService<FileKnowledgeStore>());
        services.AddSingleton<IVectorStore>(provider => provider.GetRequiredService<FileKnowledgeStore>());
        services.AddScoped<IKnowledgeTextExtractor, KnowledgeTextExtractor>();
        services.AddScoped<IKnowledgeBaseService, KnowledgeBaseService>();
        return services;
    }
}
