using BoardGameAiDashboard.Domain.Common;

namespace BoardGameAiDashboard.Domain.Entities;

/// <summary>
/// Application user entity for JWT authentication.
/// Uses BCrypt password hashing — no ASP.NET Core Identity dependency.
/// </summary>
public class User : BaseEntity
{
    /// <summary>User's email address (unique, used as login identifier).</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>User's display name shown in the UI.</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>BCrypt-hashed password (never store plain text).</summary>
    public string PasswordHash { get; private set; } = string.Empty;

    /// <summary>Navigation: one user has many refresh tokens.</summary>
    public virtual ICollection<RefreshToken> RefreshTokens { get; private set; } = new List<RefreshToken>();

    private User() { } // EF Core constructor

    public User(string email, string displayName, string passwordHash)
    {
        Email = email;
        DisplayName = displayName;
        PasswordHash = passwordHash;
    }

    /// <summary>
    /// Update user profile metadata.
    /// </summary>
    public void UpdateProfile(string displayName)
    {
        DisplayName = displayName;
        MarkUpdated();
    }

    /// <summary>
    /// Change password (expects pre-hashed value).
    /// </summary>
    public void ChangePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
        MarkUpdated();
    }
}
