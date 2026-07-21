using BoardGameAiDashboard.Application.Features.Games.Commands.CreateGame;
using BoardGameAiDashboard.Application.Features.Games.Commands.DeleteGame;
using BoardGameAiDashboard.Application.Features.Games.Commands.UpdateGame;
using BoardGameAiDashboard.Application.Features.Games.Queries.GetGameById;
using BoardGameAiDashboard.Application.Features.Games.Queries.GetGames;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BoardGameAiDashboard.Api.Controllers;

/// <summary>
/// Board Game management endpoints — full CRUD.
/// All responses are automatically wrapped in the { success, data, timestamp } envelope
/// by <see cref="BoardGameAiDashboard.Api.Filters.ApiResultFilter"/>.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class GamesController : ControllerBase
{
    private readonly ISender _sender;

    public GamesController(ISender sender)
    {
        _sender = sender;
    }

    // ── GET /api/games?pageNumber=1&pageSize=10&searchTerm= ──────────

    /// <summary>
    /// Retrieves a paginated list of board games with optional search.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGames(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null,
        CancellationToken ct = default)
    {
        var query = new GetGamesQuery
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            SearchTerm = searchTerm
        };

        var result = await _sender.Send(query, ct);
        return Ok(result);
    }

    // ── GET /api/games/{id} ──────────────────────────────────────────

    /// <summary>
    /// Retrieves a single board game by its identifier.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGameById(
        Guid id,
        CancellationToken ct = default)
    {
        var query = new GetGameByIdQuery { Id = id };
        var result = await _sender.Send(query, ct);
        return Ok(result);
    }

    // ── POST /api/games ──────────────────────────────────────────────

    /// <summary>
    /// Creates a new board game.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateGame(
        [FromBody] CreateGameCommand command,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetGameById), new { id = result.Id }, result);
    }

    // ── PUT /api/games/{id} ──────────────────────────────────────────

    /// <summary>
    /// Updates an existing board game.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateGame(
        Guid id,
        [FromBody] UpdateGameCommand command,
        CancellationToken ct = default)
    {
        if (id != command.Id)
            return BadRequest(new { message = "Route id does not match request body id." });

        var result = await _sender.Send(command, ct);
        return Ok(result);
    }

    // ── DELETE /api/games/{id} ───────────────────────────────────────

    /// <summary>
    /// Soft-deletes a board game by its identifier.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteGame(
        Guid id,
        CancellationToken ct = default)
    {
        var command = new DeleteGameCommand { Id = id };
        await _sender.Send(command, ct);
        return NoContent();
    }
}
