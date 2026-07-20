using BoardGameAiDashboard.Domain.Common;

namespace BoardGameAiDashboard.Domain.Entities;

public class GameCard : BaseEntity
{
    public Guid GameId { get; private set; }
    public string CodeName { get; private set; } = string.Empty; // System code (e.g. "ZhugeLianNu")
    public string Name { get; private set; } = string.Empty;     // Display name (e.g. "諸葛連弩")
    public string Description { get; private set; } = string.Empty; // Card effect description (RAG lookup)
    // Game-specific card properties (stored as SQL JSON)
    public Dictionary<string, string> CardProperties { get; private set; } = new();
    // EF Core navigation
    public virtual Game Game { get; private set; } = null!;

    private GameCard() { }

    public GameCard(Guid gameId, string codeName, string name, string description, Dictionary<string, string> cardProperties)
    {
        GameId = gameId;
        CodeName = codeName;
        Name = name;
        Description = description;
        CardProperties = cardProperties ?? new Dictionary<string, string>();
    }
}
