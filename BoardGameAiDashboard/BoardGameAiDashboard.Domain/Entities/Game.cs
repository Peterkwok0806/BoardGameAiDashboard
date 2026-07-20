using BoardGameAiDashboard.Domain.Common;

namespace BoardGameAiDashboard.Domain.Entities;

public class Game : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public int MinPlayers { get; private set; }
    public int MaxPlayers { get; private set; }

    // EF Core navigation: one game has many rule chunks, characters, cards, and match histories
    public virtual ICollection<GameRuleChunk> RuleChunks { get; private set; } = new List<GameRuleChunk>();
    public virtual ICollection<GameCharacter> Characters { get; private set; } = new List<GameCharacter>();
    public virtual ICollection<GameCard> Cards { get; private set; } = new List<GameCard>();
    public virtual ICollection<MatchHistory> MatchHistories { get; private set; } = new List<MatchHistory>();

    private Game() { } // EF Core constructor

    public Game(string name, string description, int minPlayers, int maxPlayers)
    {
        Name = name;
        Description = description;
        MinPlayers = minPlayers;
        MaxPlayers = maxPlayers;
    }
}
