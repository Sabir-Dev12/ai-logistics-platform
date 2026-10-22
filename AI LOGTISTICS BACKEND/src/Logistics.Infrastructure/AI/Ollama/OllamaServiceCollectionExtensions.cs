using Logistics.Application.AI;
using Logistics.Application.AI.RAG;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Logistics.Infrastructure.AI.Ollama;

public static class OllamaServiceCollectionExtensions
{
    public static IServiceCollection AddLocalOllama(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection("AI:Ollama").Get<OllamaOptions>() ?? new();
        options.Validate();
        services.AddSingleton(options);
        services.AddHttpClient<OllamaClient>(http => http.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false });
        services.TryAddScoped<ILLMService, OllamaLLMService>();
        services.TryAddScoped<IEmbeddingService, OllamaEmbeddingService>();
        return services;
    }
}
