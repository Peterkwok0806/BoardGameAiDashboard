namespace BoardGameAiDashboard.Application.Common.Interfaces;

/// <summary>
/// Abstracts the current UTC time so domain logic and handlers
/// can be tested without depending on the system clock.
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>The current date and time in UTC.</summary>
    DateTime UtcNow { get; }
}
