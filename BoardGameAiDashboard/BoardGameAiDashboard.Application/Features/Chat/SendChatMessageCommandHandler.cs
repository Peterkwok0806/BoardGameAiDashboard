using MediatR;

namespace BoardGameAiDashboard.Application.Features.Chat;

/// <summary>
/// Placeholder handler for sending chat messages.
/// Planned for Phase 7 (RAG Chat with Semantic Kernel + Qdrant).
/// </summary>
internal sealed class SendChatMessageCommandHandler
    : IRequestHandler<SendChatMessageCommand, SendChatMessageCommandResponse>
{
    public Task<SendChatMessageCommandResponse> Handle(
        SendChatMessageCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException(
            "Chat message handling is planned for Phase 7 (RAG Chat).");
    }
}
