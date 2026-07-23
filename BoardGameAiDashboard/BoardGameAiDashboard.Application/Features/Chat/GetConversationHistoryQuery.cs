using MediatR;

namespace BoardGameAiDashboard.Application.Features.Chat;

/// <summary>
/// CQRS query to retrieve conversation history for a specific chat session.
/// </summary>
public sealed record GetConversationHistoryQuery : IRequest<List<ChatMessageDto>>
{
    /// <summary>Conversation (chat session) identifier.</summary>
    public Guid ConversationId { get; init; }
}
