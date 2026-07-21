using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BoardGameAiDashboard.Api.Filters;

/// <summary>
/// Action filter that wraps all successful responses into an
/// <c>{ "success": true, "data": ..., "timestamp": ... }</c> envelope.
/// Uses Dictionary&lt;string, object&gt; to avoid generic type issues at runtime.
/// <para>
/// Responses that are already <see cref="ProblemDetails"/> (errors) or
/// <c>application/problem+json</c> are passed through unwrapped.
/// </para>
/// </summary>
public sealed class ApiResultFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context) { }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        // ── Skip if already an error (ProblemDetails) ──────────────────
        if (context.Exception != null || context.Result is ObjectResult { StatusCode: >= 400 })
            return;

        // ── Skip if already wrapped (avoid double-wrap) ────────────────
        if (context.Result is ObjectResult objectResult &&
            objectResult.Value is IDictionary<string, object> dict &&
            dict.ContainsKey("success"))
            return;

        // ── Wrap successful ObjectResult ───────────────────────────────
        if (context.Result is ObjectResult result && result.Value is not null)
        {
            var wrapped = new Dictionary<string, object>
            {
                { "success", true },
                { "data", result.Value },
                { "timestamp", DateTime.UtcNow }
            };

            context.Result = new ObjectResult(wrapped)
            {
                StatusCode = result.StatusCode,
                ContentTypes = { "application/json" }
            };
        }
        // ── Wrap NoContent (204) ──────────────────────────────────────
        else if (context.Result is StatusCodeResult { StatusCode: 204 })
        {
            var wrapped = new Dictionary<string, object>
            {
                { "success", true },
                { "data", (object?)null },
                { "timestamp", DateTime.UtcNow }
            };

            context.Result = new ObjectResult(wrapped)
            {
                StatusCode = 204,
                ContentTypes = { "application/json" }
            };
        }
    }
}
