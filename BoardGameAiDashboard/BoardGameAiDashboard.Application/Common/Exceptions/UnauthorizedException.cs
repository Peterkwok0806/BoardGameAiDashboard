namespace BoardGameAiDashboard.Application.Common.Exceptions;

/// <summary>
/// Thrown when authentication fails (invalid credentials, expired/invalid refresh token, etc.).
/// The global middleware converts this to a 401 ProblemDetails response.
/// </summary>
public class UnauthorizedException : Exception
{
    public UnauthorizedException()
        : base("Authentication failed.")
    {
    }

    public UnauthorizedException(string message)
        : base(message)
    {
    }
}
