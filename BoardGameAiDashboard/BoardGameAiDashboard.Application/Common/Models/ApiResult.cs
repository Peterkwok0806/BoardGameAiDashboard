namespace BoardGameAiDashboard.Application.Common.Models;

/// <summary>
/// Unified API response wrapper.
/// Success responses carry <see cref="Data"/>; failure responses carry <see cref="Errors"/>.
/// </summary>
public class ApiResult<T>
{
    /// <summary>Whether the operation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>The response payload (null on failure).</summary>
    public T? Data { get; init; }

    /// <summary>A human-readable message describing the outcome.</summary>
    public string? Message { get; init; }

    /// <summary>UTC timestamp when the response was produced.</summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Validation / domain errors keyed by field name.
    /// Each value is an array of error messages for that field.
    /// Null on success responses.
    /// </summary>
    public IDictionary<string, string[]>? Errors { get; init; }

    // ── Factory Methods ──────────────────────────────────────────────

    /// <summary>Create a success result with data.</summary>
    public static ApiResult<T> Ok(T data, string? message = null)
    {
        return new ApiResult<T>
        {
            Success = true,
            Data = data,
            Message = message,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>Create a failure result with optional validation errors.</summary>
    public static ApiResult<T> Fail(
        string message, 
        IDictionary<string, string[]>? errors = null)
    {
        return new ApiResult<T>
        {
            Success = false,
            Message = message,
            Errors = errors, // 如果呼叫時沒傳，這裡就是 null
            Timestamp = DateTime.UtcNow
        };
    }
}
