using BoardGameAiDashboard.Application.Features.MatchHistory;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BoardGameAiDashboard.Api.Controllers;

/// <summary>
/// Match history endpoints — Phase 2 placeholder (Game CRUD + Match History).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class MatchHistoryController : ControllerBase
{
    private readonly ISender _sender;

    public MatchHistoryController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Records a completed match result.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> RecordMatch(
        [FromBody] RecordMatchCommand command,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetMatchHistory), new { gameId = result }, result);
    }

    /// <summary>
    /// Retrieves match history for a specific game.
    /// </summary>
    [HttpGet("game/{gameId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMatchHistory(
        Guid gameId,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetMatchHistoryQuery
        {
            GameId = gameId,
            PageSize = pageSize
        };
        var result = await _sender.Send(query, ct);
        return Ok(result);
    }
}
