using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BoardGameAiDashboard.Application.Common.Exceptions;
using BoardGameAiDashboard.Application.Common.Interfaces;
using BoardGameAiDashboard.Application.Features.Auth;
using BoardGameAiDashboard.Domain.Entities;
using BoardGameAiDashboard.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BoardGameAiDashboard.Infrastructure.Services.Auth;

/// <summary>
/// JWT token service that generates access tokens and refresh tokens.
/// Refresh tokens are stored in the database via the Unit of Work.
/// Throws <see cref="UnauthorizedException"/> when token operations fail.
/// </summary>
public sealed class JwtTokenService : IJwtTokenService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly JwtSettings _settings;

    public JwtTokenService(IUnitOfWork unitOfWork, IOptions<JwtSettings> settings)
    {
        _unitOfWork = unitOfWork;
        _settings = settings.Value;
    }

    public async Task<TokenPairResponse> GenerateTokenPairAsync(
        Guid userId, string email, string ipAddress, CancellationToken cancellationToken = default)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_settings.TokenTTLInMinutes);

        // Generate access token
        var accessToken = GenerateAccessToken(userId, email, expiresAt);

        // Generate refresh token
        var refreshTokenValue = GenerateRefreshTokenValue();
        var refreshToken = new RefreshToken(
            refreshTokenValue,
            userId,
            DateTime.UtcNow.AddHours(_settings.RefreshTokenTTLInHours),
            ipAddress);

        await _unitOfWork.RefreshTokens.AddAsync(refreshToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new TokenPairResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenValue,
            ExpiresIn = (int)(expiresAt - DateTime.UtcNow).TotalSeconds
        };
    }

    public async Task<TokenPairResponse> RefreshTokenAsync(
        string refreshTokenValue, string ipAddress, CancellationToken cancellationToken = default)
    {
        var refreshToken = await _unitOfWork.RefreshTokens.Query()
            .FirstOrDefaultAsync(rt => rt.Token == refreshTokenValue && !rt.IsRevoked, cancellationToken);

        if (refreshToken is null || refreshToken.ExpiresAt <= DateTime.UtcNow)
        {
            throw new UnauthorizedException("Invalid or expired refresh token.");
        }

        // Revoke old refresh token
        refreshToken.Revoke();

        // Fetch user to get email and generate new tokens
        var user = await _unitOfWork.Users.GetByIdAsync(refreshToken.UserId, cancellationToken);
        if (user is null)
        {
            throw new UnauthorizedException("User not found.");
        }

        var newExpiresAt = DateTime.UtcNow.AddMinutes(_settings.TokenTTLInMinutes);

        var newAccessToken = GenerateAccessToken(user.Id, user.Email, newExpiresAt);

        var newRefreshTokenValue = GenerateRefreshTokenValue();
        var newRefreshToken = new RefreshToken(
            newRefreshTokenValue,
            refreshToken.UserId,
            DateTime.UtcNow.AddHours(_settings.RefreshTokenTTLInHours),
            ipAddress);

        await _unitOfWork.RefreshTokens.AddAsync(newRefreshToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new TokenPairResponse
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshTokenValue,
            ExpiresIn = (int)(newExpiresAt - DateTime.UtcNow).TotalSeconds
        };
    }

    public async Task RevokeAllUserTokensAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var activeTokens = await _unitOfWork.RefreshTokens.Query()
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.Revoke();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Generate a signed JWT access token with user claims.
    /// </summary>
    private string GenerateAccessToken(Guid userId, string email, DateTime expiresAt)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
        };

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Generate a cryptographically secure random refresh token string.
    /// </summary>
    private static string GenerateRefreshTokenValue()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
}
