using BoardGameAiDashboard.Application.Features.Chat;

namespace BoardGameAiDashboard.Application.Common.Interfaces;

/// <summary>
/// Rewrites a user query using conversation history to produce a standalone
/// search query. This decouples context-carrying from vector search.
/// Implementation lives in the Infrastructure layer.
/// </summary>
public interface IQueryRewriter
{
    /// <summary>
    /// Given a user's latest message and prior conversation turns, produce
    /// a standalone query suitable for embedding and vector search.
    /// </summary>
    /// <param name="query">The user's current message (may contain pronouns/references).</param>
    /// <param name="history">Prior conversation turns (oldest → newest).</param>
    /// <param name="gameName">Optional game name for context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A rewritten, standalone query string.</returns>
    Task<string> RewriteAsync(
        string query,
        IReadOnlyList<ChatMessageDto> history,
        string? gameName = null,
        CancellationToken cancellationToken = default);
}
