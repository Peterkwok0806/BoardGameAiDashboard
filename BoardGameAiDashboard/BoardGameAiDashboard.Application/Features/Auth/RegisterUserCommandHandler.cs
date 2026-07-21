using BoardGameAiDashboard.Application.Common.Exceptions;
using BoardGameAiDashboard.Application.Common.Interfaces;
using BoardGameAiDashboard.Domain.Entities;
using MediatR;

namespace BoardGameAiDashboard.Application.Features.Auth;

/// <summary>
/// CQRS handler for user registration.
/// Creates a new user account and returns a JWT token pair.
/// Throws <see cref="ConflictException"/> if the email is already taken.
/// </summary>
internal sealed class RegisterUserCommandHandler
    : IRequestHandler<RegisterUserCommand, TokenPairResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;

    public RegisterUserCommandHandler(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<TokenPairResponse> Handle(
        RegisterUserCommand request, CancellationToken cancellationToken)
    {
        // 1. Check if email is already taken
        var existingUsers = await _unitOfWork.Users.CountAsync(
            u => u.Email == request.Email, cancellationToken);

        if (existingUsers > 0)
        {
            throw new ConflictException("An account with this email address already exists.");
        }

        // 2. Hash the password
        var passwordHash = _passwordHasher.HashPassword(request.Password);

        // 3. Create the user entity
        var user = new User(request.Email, request.DisplayName, passwordHash);
        await _unitOfWork.Users.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 4. Generate JWT token pair
        return await _jwtTokenService.GenerateTokenPairAsync(
            user.Id, user.Email, ipAddress: string.Empty, cancellationToken);
    }
}
