using BoardGameAiDashboard.Application.Common.Interfaces;
using BoardGameAiDashboard.Domain.Entities;
using MediatR;

namespace BoardGameAiDashboard.Application.Features.Chat;

/// <summary>
/// Retrieves chat history for a specific game, ordered by creation time ascending.
/// </summary>
internal sealed class GetChatHistoryByGameQueryHandler
    : IRequestHandler<GetChatHistoryByGameQuery, List<ChatMessageDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetChatHistoryByGameQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<ChatMessageDto>> Handle(
        GetChatHistoryByGameQuery request, CancellationToken cancellationToken)
    {
        var (items, _) = await _unitOfWork.ChatMessages.GetPagedAsync(
            pageNumber: 1,
            pageSize: 200,
            filter: m => m.GameId == request.GameId,
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
