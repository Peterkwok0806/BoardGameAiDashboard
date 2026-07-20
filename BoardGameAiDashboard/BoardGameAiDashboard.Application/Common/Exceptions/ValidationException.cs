using FluentValidation.Results;

namespace BoardGameAiDashboard.Application.Common.Exceptions;

/// <summary>
/// Thrown when one or more FluentValidation rules fail.
/// Carries errors grouped by property name as <c>Dictionary&lt;string, string[]&gt;</c>
/// so the global middleware can produce RFC 7807 ProblemDetails.
/// </summary>
public class ValidationException : Exception
{
    /// <summary>
    /// Validation errors keyed by property name.
    /// Each value is an array of error messages for that property.
    /// </summary>
    public IDictionary<string, string[]> Errors { get; }

    /// <summary>
    /// Parameterless constructor (required for serialization).
    /// </summary>
    public ValidationException()
        : base("One or more validation failures have occurred.")
    {
        Errors = new Dictionary<string, string[]>();
    }

    /// <summary>
    /// Creates a <see cref="ValidationException"/> from FluentValidation failures.
    /// </summary>
    /// <param name="failures">
    /// The collection of <see cref="ValidationFailure"/> instances produced by FluentValidation.
    /// </param>
    public ValidationException(IEnumerable<ValidationFailure> failures)
        : base("One or more validation failures have occurred.")
    {
        Errors = failures
            .GroupBy(
                f => f.PropertyName,
                f => f.ErrorMessage)
            .ToDictionary(
                g => g.Key,
                g => g.ToArray());
    }
}
