using MediatR;

namespace BoardGameAiDashboard.Application.Features.Chat;

/// <summary>
/// CQRS query to retrieve chat history for a user.
/// Planned for Phase 7 (RAG Chat).
/// </summary>
public sealed record GetChatHistoryQuery : IRequest<List<ChatMessageDto>>
{
    /// <summary>The user identifier.</summary>
    public Guid UserId { get; init; }

    /// <summary>Maximum number of messages to return.</summary>
    public int PageSize { get; init; } = 50;
}

/// <summary>DTO representing a single chat message.</summary>
public sealed record ChatMessageDto
{
    /// <summary>Unique message identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Message content.</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>Whether this message was sent by the AI.</summary>
    public bool IsFromAi { get; init; }

    /// <summary>UTC timestamp of the message.</summary>
    public DateTime CreatedAt { get; init; }
}
