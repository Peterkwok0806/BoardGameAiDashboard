using MediatR;

namespace BoardGameAiDashboard.Application.Features.Chat;

/// <summary>
/// CQRS command to send a chat message and receive an AI-generated response.
/// Supports conversation context via ConversationId and history-based query rewriting.
/// </summary>
public sealed record SendChatMessageCommand : IRequest<SendChatMessageCommandResponse>
{
    /// <summary>The user identifier.</summary>
    public Guid UserId { get; init; }

    /// <summary>The user's chat message.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Optional game context ID for game-specific queries.</summary>
    public Guid? GameId { get; init; }

    /// <summary>
    /// Optional conversation ID. If provided, the system loads history for this conversation
    /// and performs a three-stage RAG pipeline (rewrite → retrieve → generate).
    /// If null, a new conversation is created and a simple one-shot RAG is used.
    /// </summary>
    public Guid? ConversationId { get; init; }
}

/// <summary>Response DTO for the chat command.</summary>
public sealed record SendChatMessageCommandResponse
{
    /// <summary>The user message details.</summary>
    public ChatMessageDto UserMessage { get; init; } = default!;

    /// <summary>The AI-generated response details.</summary>
    public ChatMessageDto AiMessage { get; init; } = default!;

    /// <summary>Source section titles used by RAG.</summary>
    public List<string> Sources { get; init; } = new();

    /// <summary>Conversation ID (newly created or existing).</summary>
    public Guid ConversationId { get; init; }
}
