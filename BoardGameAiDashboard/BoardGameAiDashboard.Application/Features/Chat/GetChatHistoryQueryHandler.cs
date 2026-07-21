using MediatR;

namespace BoardGameAiDashboard.Application.Features.Chat;

/// <summary>
/// Placeholder handler for retrieving chat history.
/// Planned for Phase 7 (RAG Chat with Semantic Kernel + Qdrant).
/// </summary>
internal sealed class GetChatHistoryQueryHandler
    : IRequestHandler<GetChatHistoryQuery, List<ChatMessageDto>>
{
    public Task<List<ChatMessageDto>> Handle(
        GetChatHistoryQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException(
            "Chat history query is planned for Phase 7 (RAG Chat).");
    }
}
