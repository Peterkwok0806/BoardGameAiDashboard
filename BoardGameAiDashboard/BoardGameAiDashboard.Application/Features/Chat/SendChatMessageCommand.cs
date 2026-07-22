using MediatR;

namespace BoardGameAiDashboard.Application.Features.Chat;

/// <summary>
/// CQRS command to send a chat message and receive an AI-generated response.
/// </summary>
public sealed record SendChatMessageCommand : IRequest<SendChatMessageCommandResponse>
{
    /// <summary>The user identifier.</summary>
    public Guid UserId { get; init; }

    /// <summary>The user's chat message.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Optional game context ID for game-specific queries.</summary>
    public Guid? GameId { get; init; }
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
}
