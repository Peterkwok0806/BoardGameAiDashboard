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
}

/// <summary>
/// RAG query result.
/// </summary>
public sealed record RagResponse(string Reply, IReadOnlyList<string> Sources);
