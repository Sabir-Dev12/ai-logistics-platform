using Logistics.Infrastructure.AI.Ollama;

namespace Logistics.UnitTests;

public sealed class OllamaLiveFactAttribute : FactAttribute
{
    public OllamaLiveFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("OLLAMA_LIVE_TEST") != "1") Skip = "Opt-in: requires a local Ollama server and installed embedding model.";
    }
}

public sealed class OllamaLiveTests
{
    [OllamaLiveFact]
    public async Task Local_embedding_adapter_returns_a_real_nonzero_vector()
    {
        var options = new OllamaOptions { EmbeddingModel = Environment.GetEnvironmentVariable("OLLAMA_TEST_EMBEDDING_MODEL") ?? "embeddinggemma:300m" };
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false });
        var vector = await new OllamaEmbeddingService(new(http, options), options).GenerateAsync("Procédure de suivi d’une expédition de démonstration.");
        Assert.Equal("ollama:" + options.EmbeddingModel, vector.ModelId);
        Assert.True(vector.Values.Count > 0); Assert.True(vector.Magnitude > 0);
    }
}
