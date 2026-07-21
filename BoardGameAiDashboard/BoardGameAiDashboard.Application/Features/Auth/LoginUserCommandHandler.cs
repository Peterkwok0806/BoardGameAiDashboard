using BoardGameAiDashboard.Application.Common.Exceptions;
using BoardGameAiDashboard.Application.Common.Interfaces;
using MediatR;

namespace BoardGameAiDashboard.Application.Features.Auth;

/// <summary>
/// CQRS handler for user login.
/// Validates credentials and returns a JWT token pair.
/// Throws <see cref="UnauthorizedException"/> if credentials are invalid.
/// </summary>
internal sealed class LoginUserCommandHandler
    : IRequestHandler<LoginUserCommand, TokenPairResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;

    public LoginUserCommandHandler(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<TokenPairResponse> Handle(
        LoginUserCommand request, CancellationToken cancellationToken)
    {
        // 1. Find user by email
        var user = await _unitOfWork.Users.FindOneAsync(
            u => u.Email == request.Email, cancellationToken);

        if (user is null)
        {
            throw new UnauthorizedException("Invalid email or password.");
        }

        // 2. Verify password
        var passwordValid = _passwordHasher.VerifyPassword(request.Password, user.PasswordHash);

        if (!passwordValid)
        {
            throw new UnauthorizedException("Invalid email or password.");
        }

        // 3. Generate JWT token pair
        return await _jwtTokenService.GenerateTokenPairAsync(
            user.Id, user.Email, ipAddress: string.Empty, cancellationToken);
    }
}
