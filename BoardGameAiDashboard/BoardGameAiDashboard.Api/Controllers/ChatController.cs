using BoardGameAiDashboard.Application.Features.Chat;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoardGameAiDashboard.Api.Controllers;

/// <summary>
/// AI Chat endpoints — multi-turn RAG with query rewriting and conversation history.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public sealed class ChatController : ControllerBase
{
    private readonly ISender _sender;

    public ChatController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Sends a chat message and receives an AI-generated response.
    /// Pass a ConversationId to continue an existing conversation (three-stage RAG pipeline).
    /// Omit ConversationId to start a new conversation.
    /// </summary>
    [HttpPost("send")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SendMessage(
        [FromBody] SendChatMessageCommand command,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(command, ct);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves conversation history for a specific chat session.
    /// </summary>
    [HttpGet("conversation/{conversationId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConversationHistory(
        Guid conversationId,
        CancellationToken ct = default)
    {
        var query = new GetConversationHistoryQuery
        {
            ConversationId = conversationId
        };
        var result = await _sender.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves chat history for a specific user (all conversations).
    /// </summary>
    [HttpGet("history/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChatHistory(
        Guid userId,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetChatHistoryQuery
        {
            UserId = userId,
            PageSize = pageSize
        };
        var result = await _sender.Send(query, ct);
        return Ok(result);
    }
}
