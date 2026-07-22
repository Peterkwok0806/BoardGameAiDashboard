namespace BoardGameAiDashboard.Infrastructure.Settings;

/// <summary>
/// Strongly-typed settings for the local Ollama LLM and embedding services.
/// Bound from appsettings.json section "Ollama".
/// </summary>
public sealed class OllamaSettings
{
    /// <summary>Configuration section key.</summary>
    public const string SectionName = "Ollama";

    /// <summary>Base URL of the Ollama server (e.g. http://localhost:11434).</summary>
    public string Endpoint { get; set; } = "http://localhost:11434";

    /// <summary>Chat/completion model name (e.g. qwen2.5:3b).</summary>
    public string ChatModel { get; set; } = "qwen2.5:3b";

    /// <summary>Embedding model name (e.g. mxbai-embed-large:latest).</summary>
    public string EmbeddingModel { get; set; } = "mxbai-embed-large:latest";
}
