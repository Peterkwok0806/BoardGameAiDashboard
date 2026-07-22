using BoardGameAiDashboard.Application.Common.Interfaces;
using BoardGameAiDashboard.Domain.Entities;
using MediatR;

namespace BoardGameAiDashboard.Application.Features.Chat;

/// <summary>
/// RAG Chat flow:
/// 1. Build system prompt (game rules context + multilingual instructions)
/// 2. Vector-search relevant game rules via Qdrant
/// 3. Generate AI response via Semantic Kernel → Ollama LLM
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
        // 1. RAG: generate AI response with game rules context
        var ragResult = await _ragService.QueryAsync(
            request.Message,
            request.GameId,
            cancellationToken);

        // 2. Persist user message
        var userMessage = new ChatMessage(
            userId: request.UserId,
            gameId: request.GameId,
            content: request.Message,
            isFromAi: false,
            sources: new List<string>());
        await _unitOfWork.ChatMessages.AddAsync(userMessage, cancellationToken);

        // 3. Persist AI response (sources stored directly as List<string>, EF Core handles JSON)
        var aiMessage = new ChatMessage(
            userId: request.UserId,
            gameId: request.GameId,
            content: ragResult.Reply,
            isFromAi: true,
            sources: ragResult.Sources.ToList());
        await _unitOfWork.ChatMessages.AddAsync(aiMessage, cancellationToken);

        // 4. Save to database in a single transaction
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 5. Return combined result
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
            Sources = ragResult.Sources.ToList()
        };
    }
}
