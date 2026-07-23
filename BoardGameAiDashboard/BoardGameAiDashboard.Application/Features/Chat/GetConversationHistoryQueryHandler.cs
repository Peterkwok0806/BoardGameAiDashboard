using BoardGameAiDashboard.Application.Common.Interfaces;
using MediatR;

namespace BoardGameAiDashboard.Application.Features.Chat;

/// <summary>
/// Retrieves all messages for a given conversation, ordered by creation time ascending.
/// </summary>
internal sealed class GetConversationHistoryQueryHandler
    : IRequestHandler<GetConversationHistoryQuery, List<ChatMessageDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetConversationHistoryQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<ChatMessageDto>> Handle(
        GetConversationHistoryQuery request, CancellationToken cancellationToken)
    {
        var (items, _) = await _unitOfWork.ChatMessages.GetPagedAsync(
            pageNumber: 1,
            pageSize: 100, // Conversations should not exceed 100 messages
            filter: m => m.ConversationId == request.ConversationId,
            cancellationToken: cancellationToken);

        return items
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ChatMessageDto
            {
                Id = m.Id,
                Content = m.Content,
                IsFromAi = m.IsFromAi,
                CreatedAt = m.CreatedAt
            })
            .ToList();
    }
}
