namespace BoardGameAiDashboard.Application.Common.Exceptions;

/// <summary>
/// Thrown when a requested entity is not found in the database.
/// The global middleware converts this to a 404 ProblemDetails response.
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException()
        : base("The requested resource was not found.")
    {
    }

    public NotFoundException(string name, object key)
        : base($"Entity \"{name}\" ({key}) was not found.")
    {
    }
}
