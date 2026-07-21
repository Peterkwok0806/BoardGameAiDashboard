using BoardGameAiDashboard.Application.Features.Predictions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BoardGameAiDashboard.Api.Controllers;

/// <summary>
/// AI Predictions endpoints — Phase 4 placeholder (ML.NET + RAG).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class PredictionsController : ControllerBase
{
    private readonly ISender _sender;

    public PredictionsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Gets win rate prediction for a specific game.
    /// </summary>
    [HttpGet("win-rate/{gameId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWinRate(
        Guid gameId,
        CancellationToken ct = default)
    {
        var query = new GetWinRateQuery
        {
            GameId = gameId
        };
        var result = await _sender.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// Gets churn prediction for a specific user.
    /// </summary>
    [HttpGet("churn/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChurnPrediction(
        Guid userId,
        CancellationToken ct = default)
    {
        var query = new GetChurnPredictionQuery
        {
            UserId = userId
        };
        var result = await _sender.Send(query, ct);
        return Ok(result);
    }
}
