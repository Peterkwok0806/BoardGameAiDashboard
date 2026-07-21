namespace BoardGameAiDashboard.Application.Common.Exceptions;

/// <summary>
/// Thrown when a business rule conflict occurs (e.g., duplicate email registration).
/// The global middleware converts this to a 409 ProblemDetails response.
/// </summary>
public class ConflictException : Exception
{
    public ConflictException()
        : base("The request conflicts with the current state of the resource.")
    {
    }

    public ConflictException(string message)
        : base(message)
    {
    }
}
