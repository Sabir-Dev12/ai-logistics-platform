namespace Logistics.Infrastructure.AI.Ollama;

public sealed class OllamaOptions
{
    public string BaseUrl { get; set; } = "http://127.0.0.1:11434/";
    public string ChatModel { get; set; } = "qwen3:1.7b";
    public string EmbeddingModel { get; set; } = "embeddinggemma:300m";
    public int ContextSize { get; set; } = 4096;
    public int MaximumOutputTokens { get; set; } = 1024;
    public int TimeoutSeconds { get; set; } = 180;
    public string KeepAlive { get; set; } = "1m";

    public Uri Validate()
    {
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) || !uri.IsLoopback
            || uri.Scheme is not ("http" or "https") || uri.AbsolutePath != "/"
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("Ollama BaseUrl must be a loopback HTTP(S) origin.");
        if (string.IsNullOrWhiteSpace(ChatModel) || string.IsNullOrWhiteSpace(EmbeddingModel)
            || ChatModel.Contains(":cloud", StringComparison.OrdinalIgnoreCase) || EmbeddingModel.Contains(":cloud", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Select local Ollama models; cloud models are not permitted.");
        if (ContextSize is < 512 or > 32768 || MaximumOutputTokens is < 1 or > 8192 || TimeoutSeconds is < 1 or > 600)
            throw new ArgumentOutOfRangeException(nameof(ContextSize), "Invalid Ollama resource limits.");
        if (string.IsNullOrWhiteSpace(KeepAlive)) throw new ArgumentException("Ollama KeepAlive is required.");
        return uri;
    }
}
