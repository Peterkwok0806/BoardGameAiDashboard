using BoardGameAiDashboard.Domain.Common;

namespace BoardGameAiDashboard.Domain.Entities;

public class GameRuleChunk : BaseEntity
{
    public Guid GameId { get; private set; }
    public string Content { get; private set; } = string.Empty;       // Rule plain text content
    public string SectionTitle { get; private set; } = string.Empty;  // Rule section (e.g. Setup, Scoring)
    // Qdrant vector database Point ID (UUID string)
    public string QdrantPointId { get; private set; } = string.Empty;

    // EF Core navigation
    public virtual Game Game { get; private set; } = null!;

    private GameRuleChunk() { }

    public GameRuleChunk(Guid gameId, string content, string sectionTitle, string qdrantPointId)
    {
        GameId = gameId;
        Content = content;
        SectionTitle = sectionTitle;
        QdrantPointId = qdrantPointId;
    }
}
