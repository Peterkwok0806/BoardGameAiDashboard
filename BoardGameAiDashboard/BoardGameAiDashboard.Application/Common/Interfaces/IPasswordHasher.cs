namespace BoardGameAiDashboard.Application.Common.Interfaces;

/// <summary>
/// Password hashing abstraction.
/// Implementation lives in the Infrastructure layer (BCrypt).
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Hash a plain-text password.
    /// </summary>
    string HashPassword(string plainTextPassword);

    /// <summary>
    /// Verify a plain-text password against a BCrypt hash.
    /// </summary>
    bool VerifyPassword(string plainTextPassword, string hashedPassword);
}
