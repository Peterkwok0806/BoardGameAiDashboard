using BoardGameAiDashboard.Application.Features.Chat;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BoardGameAiDashboard.Api.Controllers;

/// <summary>
/// AI Chat endpoints — Phase 3 placeholder (RAG integration).
/// </summary>
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
    /// Retrieves chat history for a specific user.
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
