namespace BoardGameAiDashboard.Infrastructure.Settings;

/// <summary>
/// Strongly-typed settings for JWT authentication.
/// Bound from appsettings.json section "Jwt".
/// </summary>
public sealed class JwtSettings
{
    /// <summary>Configuration section key.</summary>
    public const string SectionName = "Jwt";

    /// <summary>JWT signing secret key (minimum 32 characters recommended).</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>Token issuer (e.g. "BoardGameAiDashboard").</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Token audience (e.g. "BoardGameAiDashboard").</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>Access token lifetime in minutes (default: 15).</summary>
    public int TokenTTLInMinutes { get; set; } = 15;

    /// <summary>Refresh token lifetime in hours (default: 168 = 7 days).</summary>
    public int RefreshTokenTTLInHours { get; set; } = 168;
}
