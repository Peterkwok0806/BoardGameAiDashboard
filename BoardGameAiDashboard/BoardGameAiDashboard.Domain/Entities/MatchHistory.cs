using BoardGameAiDashboard.Domain.Common;

namespace BoardGameAiDashboard.Domain.Entities;

public class MatchHistory : BaseEntity
{
    public Guid GameId { get; private set; }
    // ML.NET prediction feature fields (example)
    public int PlayerCount { get; private set; }
    public bool IsWinner { get; private set; }
    public DateTime PlayedAt { get; private set; }
    // Core: per-player match details and environment (stored as SQL JSON)
    public Dictionary<string, string> GameFeatures { get; private set; } = new();
    // EF Core navigation
    public virtual Game Game { get; private set; } = null!;

    private MatchHistory() { }

    public MatchHistory(Guid gameId, int playerCount, bool isWinner, Dictionary<string, string> gameFeatures)
    {
        GameId = gameId;
        PlayerCount = playerCount;
        IsWinner = isWinner;
        GameFeatures = gameFeatures ?? new Dictionary<string, string>();
        PlayedAt = DateTime.UtcNow;
    }
}
