using MediatR;

namespace BoardGameAiDashboard.Application.Features.Chat;

/// <summary>
/// CQRS command to send a chat message and receive an AI-generated response.
/// Planned for Phase 7 (RAG Chat).
/// </summary>
public sealed record SendChatMessageCommand : IRequest<SendChatMessageCommandResponse>
{
    /// <summary>The user's chat message.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Optional game context ID for game-specific queries.</summary>
    public Guid? GameId { get; init; }
}

/// <summary>Response DTO for the chat command.</summary>
public sealed record SendChatMessageCommandResponse
{
    /// <summary>The AI-generated response message.</summary>
    public string Reply { get; init; } = string.Empty;

    /// <summary>Sources used to generate the response.</summary>
    public List<string> Sources { get; init; } = new();
}
