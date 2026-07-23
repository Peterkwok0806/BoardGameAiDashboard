using BoardGameAiDashboard.Domain.Common;

namespace BoardGameAiDashboard.Domain.Entities;

/// <summary>
/// Stores individual chat messages (user questions and AI replies).
/// Append-only — no soft-delete logic needed in practice, but inherits BaseEntity for consistency.
/// </summary>
public class ChatMessage : BaseEntity
{
    /// <summary>FK to User — null for anonymous sessions (if ever supported).</summary>
    public Guid? UserId { get; private set; }

    /// <summary>Message content (user question or AI reply).</summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>True if this message is an AI-generated reply.</summary>
    public bool IsFromAi { get; private set; }

    /// <summary>Source chunk section titles used by RAG.</summary>
    public List<string> Sources { get; private set; } = new();

    /// <summary>Optional: the game context this conversation was about.</summary>
    public Guid? GameId { get; private set; }

    /// <summary>Group messages into conversations (chat sessions).</summary>
    public Guid ConversationId { get; private set; }

    private ChatMessage() { } // EF Core

    public ChatMessage(
        Guid? userId,
        Guid? gameId,
        Guid conversationId,
        string content,
        bool isFromAi,
        List<string> sources)
    {
        UserId = userId;
        GameId = gameId;
        ConversationId = conversationId;
        Content = content;
        IsFromAi = isFromAi;
        Sources = sources ?? new List<string>();
    }
}
