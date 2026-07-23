using BoardGameAiDashboard.Application.Features.Chat;

namespace BoardGameAiDashboard.Application.Common.Interfaces;

/// <summary>
/// Orchestrates the full RAG pipeline: embed query → vector search → prompt → generate answer.
/// Implementation lives in the Infrastructure layer.
/// </summary>
public interface IRagService
{
    /// <summary>
    /// Execute a RAG query against game rule documents.
    /// </summary>
    /// <param name="question">User's natural language question.</param>
    /// <param name="gameId">Optional game filter — narrow search to one game.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>RAG response containing the AI reply and source chunk titles.</returns>
    Task<RagResponse> QueryAsync(string question, Guid? gameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute a RAG query with conversation history for context-aware search.
    /// Performs: query rewrite → embed rewritten query → vector search → generate answer with history.
    /// </summary>
    /// <param name="question">User's latest message.</param>
    /// <param name="gameId">Optional game filter.</param>
    /// <param name="gameName">Optional game name for query rewriting context.</param>
    /// <param name="history">Conversation history for context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>RAG response containing the AI reply and source chunk titles.</returns>
    Task<RagResponse> QueryAsync(
        string question,
        Guid? gameId,
        string? gameName,
        IReadOnlyList<ChatMessageDto> history,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// RAG query result.
/// </summary>
public sealed record RagResponse(string Reply, IReadOnlyList<string> Sources);
