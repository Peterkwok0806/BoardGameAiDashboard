using System.Net;
using System.Text.Json;
using BoardGameAiDashboard.Application.Common.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace BoardGameAiDashboard.Api.Middleware;

/// <summary>
/// Global exception handling middleware.
/// Translates known exceptions into RFC 7807 ProblemDetails responses.
/// Unknown exceptions return a generic 500 ProblemDetails (no stack trace leak).
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title, detail) = exception switch
        {
            // ── Domain / Application Exceptions ──────────────────────
            NotFoundException notFound =>
                (HttpStatusCode.NotFound, "Resource Not Found", notFound.Message),

            ForbiddenAccessException forbidden =>
                (HttpStatusCode.Forbidden, "Forbidden", forbidden.Message),

            ValidationException validation =>
                (HttpStatusCode.BadRequest, "Validation Error", "One or more validation errors occurred."),

            // ── Unexpected Exceptions ────────────────────────────────
            _ =>
                (HttpStatusCode.InternalServerError, "Internal Server Error",
                 "An unexpected error occurred. Please try again later.")
        };

        var problemDetails = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Detail = detail,
            Type = $"https://httpstatuses.com/{(int)statusCode}",
            Instance = context.Request.Path
        };

        // ── Attach validation errors if present ──────────────────────
        if (exception is ValidationException valEx && valEx.Errors.Count > 0)
        {
            problemDetails.Extensions["errors"] = valEx.Errors;
        }

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(problemDetails, JsonOptions));
    }
}
