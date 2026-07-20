using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BoardGameAiDashboard.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that logs every request name, its parameters,
/// and the elapsed time using a high-resolution <see cref="Stopwatch"/>.
/// Errors are logged at <c>Error</c> level and re-thrown untouched
/// so the outer exception-handling middleware can handle them.
/// </summary>
public class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        // High-precision timer
        var sw = Stopwatch.StartNew();

        _logger.LogInformation(
            "Handling {RequestName} with {@Request}",
            requestName, request);

        try
        {
            var response = await next();

            _logger.LogInformation(
                "Handled {RequestName} in {ElapsedMs} ms",
                requestName, sw.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error handling {RequestName} after {ElapsedMs} ms",
                requestName, sw.ElapsedMilliseconds);

            throw; // Re-throw without modification
        }
        finally
        {
            sw.Stop();
        }
    }
}
