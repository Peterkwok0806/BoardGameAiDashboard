namespace BoardGameAiDashboard.Infrastructure.Settings;

/// <summary>
/// Strongly-typed settings for the Qdrant vector database.
/// Bound from appsettings.json section "Qdrant".
/// </summary>
public sealed class QdrantSettings
{
    /// <summary>Configuration section key.</summary>
    public const string SectionName = "Qdrant";

    /// <summary>Qdrant server URL (e.g. http://localhost:6334).</summary>
    public string Endpoint { get; set; } = "http://localhost:6334";

    /// <summary>Collection name for game-rule embeddings.</summary>
    public string CollectionName { get; set; } = "game_rules";

    /// <summary>Vector dimension — must match the embedding model output (mxbai-embed-large = 1024).</summary>
    public int VectorDimension { get; set; } = 1024;
}
