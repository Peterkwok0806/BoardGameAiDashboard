using BoardGameAiDashboard.Application.Common.Interfaces;

namespace BoardGameAiDashboard.Infrastructure.Services.Auth;

/// <summary>
/// BCrypt-based password hashing implementation.
/// Uses a work factor of 12 (default) which provides a good balance of security and performance.
/// </summary>
public sealed class BCryptPasswordHasher : IPasswordHasher
{
    public string HashPassword(string plainTextPassword)
    {
        return BCrypt.Net.BCrypt.HashPassword(plainTextPassword);
    }

    public bool VerifyPassword(string plainTextPassword, string hashedPassword)
    {
        return BCrypt.Net.BCrypt.Verify(plainTextPassword, hashedPassword);
    }
}
