namespace BoardGameAiDashboard.Application.Common.Exceptions;

/// <summary>
/// Thrown when an ML prediction operation fails.
/// The global middleware converts this to a 503 Service Unavailable response.
/// </summary>
public class PredictionException : Exception
{
    /// <summary>
    /// Gets the prediction error code for programmatic handling.
    /// </summary>
    public string ErrorCode { get; }

    public PredictionException()
        : base("ML prediction failed.")
    {
        ErrorCode = "Unknown";
    }

    public PredictionException(string message)
        : base(message)
    {
        ErrorCode = "Unknown";
    }

    public PredictionException(string message, string errorCode)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public PredictionException(string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = "Unknown";
    }

    public PredictionException(string message, string errorCode, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
