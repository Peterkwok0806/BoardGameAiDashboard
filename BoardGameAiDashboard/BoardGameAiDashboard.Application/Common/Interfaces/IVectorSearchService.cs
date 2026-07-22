using System.Collections.Generic;

namespace BoardGameAiDashboard.Application.Common.Interfaces;

/// <summary>
/// Abstraction over the vector database (Qdrant).
/// Provides similarity search, upsert, delete and collection management.
/// Implementation lives in the Infrastructure layer.
/// </summary>
public interface IVectorSearchService
{
    /// <summary>
    /// Perform a cosine-similarity search and return the top-K results.
    /// </summary>
    /// <param name="queryEmbedding">Embedding vector of the user query.</param>
    /// <param name="topK">Maximum number of results.</param>
    /// <param name="gameId">
    /// Optional game filter — when supplied, only chunks belonging to the given game are returned.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        float[] queryEmbedding,
        int topK,
        Guid? gameId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Insert or update a single point in the vector collection.
    /// </summary>
    Task UpsertAsync(
        string pointId,
        float[] embedding,
        Dictionary<string, string> metadata,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a single point by its ID.
    /// </summary>
    Task DeleteAsync(
        string pointId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensure the collection exists; create it with the correct dimension if it does not.
    /// </summary>
    Task EnsureCollectionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// A single vector-search result with score and metadata payload.
/// </summary>
public sealed record VectorSearchResult(
    string PointId,
    float Score,
    string Content,
    string SectionTitle,
    Guid GameId);
