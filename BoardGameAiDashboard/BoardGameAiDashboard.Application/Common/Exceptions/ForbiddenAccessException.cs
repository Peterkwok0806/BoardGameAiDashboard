namespace BoardGameAiDashboard.Application.Common.Exceptions;

/// <summary>
/// Thrown when the authenticated user lacks permission to perform the requested action.
/// The global middleware converts this to a 403 ProblemDetails response.
/// </summary>
public class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException()
        : base("You do not have permission to perform this action.")
    {
    }
}
