using BoardGameAiDashboard.Application.Common.Interfaces;
using BoardGameAiDashboard.Domain.Entities;
using MediatR;

namespace BoardGameAiDashboard.Application.Features.Chat;

/// <summary>
/// RAG Chat flow with conversation history support:
/// 1. Determine conversation (new or existing)
/// 2. Load conversation history for context-aware query rewriting
/// 3. RAG pipeline: rewrite query → vector search → generate answer with history
/// 4. Persist user message + AI response to EF Core
/// 5. Return combined result
/// </summary>
internal sealed class SendChatMessageCommandHandler
    : IRequestHandler<SendChatMessageCommand, SendChatMessageCommandResponse>
{
    private readonly IRagService _ragService;
    private readonly IUnitOfWork _unitOfWork;

    public SendChatMessageCommandHandler(
        IRagService ragService,
        IUnitOfWork unitOfWork)
    {
        _ragService = ragService;
        _unitOfWork = unitOfWork;
    }

    public async Task<SendChatMessageCommandResponse> Handle(
        SendChatMessageCommand request, CancellationToken cancellationToken)
    {
        // 1. Determine conversation ID
        var conversationId = request.ConversationId ?? Guid.NewGuid();

        // 2. Load conversation history for context-aware query rewriting
        List<ChatMessageDto> history = new();

        if (request.ConversationId.HasValue)
        {
            var (items, _) = await _unitOfWork.ChatMessages.GetPagedAsync(
                pageNumber: 1,
                pageSize: 20,
                filter: m => m.ConversationId == request.ConversationId.Value,
                cancellationToken: cancellationToken);

            history = items
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

        // 3. Resolve game name for query rewriting context
        string? gameName = null;
        if (request.GameId.HasValue)
        {
            var game = await _unitOfWork.Games.GetByIdAsync(
                request.GameId.Value, cancellationToken);
            gameName = game?.Name;
        }

        // 4. RAG pipeline with history
        var ragResult = await _ragService.QueryAsync(
            request.Message,
            request.GameId,
            gameName,
            history,
            cancellationToken);

        // 5. Persist user message
        var userMessage = new ChatMessage(
            userId: request.UserId,
            gameId: request.GameId,
            conversationId: conversationId,
            content: request.Message,
            isFromAi: false,
            sources: new List<string>());
        await _unitOfWork.ChatMessages.AddAsync(userMessage, cancellationToken);

        // 6. Persist AI response
        var aiMessage = new ChatMessage(
            userId: request.UserId,
            gameId: request.GameId,
            conversationId: conversationId,
            content: ragResult.Reply,
            isFromAi: true,
            sources: ragResult.Sources.ToList());
        await _unitOfWork.ChatMessages.AddAsync(aiMessage, cancellationToken);

        // 7. Save to database in a single transaction
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 8. Return combined result
        return new SendChatMessageCommandResponse
        {
            UserMessage = new ChatMessageDto
            {
                Id = userMessage.Id,
                Content = userMessage.Content,
                IsFromAi = false,
                CreatedAt = userMessage.CreatedAt
            },
            AiMessage = new ChatMessageDto
            {
                Id = aiMessage.Id,
                Content = aiMessage.Content,
                IsFromAi = true,
                CreatedAt = aiMessage.CreatedAt
            },
            Sources = ragResult.Sources.ToList(),
            ConversationId = conversationId
        };
    }
}
