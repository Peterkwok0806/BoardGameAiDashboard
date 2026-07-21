using BoardGameAiDashboard.Application.Common.Exceptions;
using BoardGameAiDashboard.Application.Features.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BoardGameAiDashboard.Api.Controllers;

/// <summary>
/// Authentication endpoints — JWT-based auth.
/// Exceptions are handled globally by <c>ExceptionHandlingMiddleware</c>;
/// this controller intentionally contains no try/catch or status-code checks.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly ISender _sender;

    public AuthController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Registers a new user account.
    /// Returns 200 on success; 409 if the email is already taken.
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterUserCommand command,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(command, ct);
        return Ok(result);
    }

    /// <summary>
    /// Authenticates a user and returns a JWT token pair.
    /// Returns 200 on success; 401 if credentials are invalid.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginUserCommand command,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(command, ct);
        return Ok(result);
    }

    /// <summary>
    /// Refreshes an expired JWT token.
    /// Returns 200 on success; 401 if the refresh token is invalid.
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken(
        [FromBody] RefreshTokenCommand command,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(command, ct);
        return Ok(result);
    }

    /// <summary>
    /// Gets the currently authenticated user's profile.
    /// Returns 200 on success; 401 if the JWT is missing/invalid.
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser(
        CancellationToken ct = default)
    {
        var userId = GetUserIdFromClaims()
            ?? throw new UnauthorizedException("Invalid or missing user identity.");

        var query = new GetCurrentUserQuery(userId);
        var result = await _sender.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// Extracts the user's GUID from JWT claims (ClaimTypes.NameIdentifier or 'sub').
    /// </summary>
    private Guid? GetUserIdFromClaims()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                    ?? User.FindFirst("sub");

        if (claim is null || !Guid.TryParse(claim.Value, out var userId))
        {
            return null;
        }

        return userId;
    }
}
