using BoardGameAiDashboard.Domain.Common;

namespace BoardGameAiDashboard.Domain.Entities;

public class GameCharacter : BaseEntity
{
    public Guid GameId { get; private set; }
    public string CodeName { get; private set; } = string.Empty; // System code (e.g. "ZhangFei")
    public string Name { get; private set; } = string.Empty;     // Display name (e.g. "張飛")
    public string SkillDescription { get; private set; } = string.Empty; // Skill description (RAG lookup)
    // Game-specific character properties (stored as SQL JSON)
    public Dictionary<string, string> CustomProperties { get; private set; } = new();
    // EF Core navigation
    public virtual Game Game { get; private set; } = null!;

    private GameCharacter() { }

    public GameCharacter(Guid gameId, string codeName, string name, string skillDescription, Dictionary<string, string> customProperties)
    {
        GameId = gameId;
        CodeName = codeName;
        Name = name;
        SkillDescription = skillDescription;
        CustomProperties = customProperties ?? new Dictionary<string, string>();
    }
}
