using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BoardGameAiDashboard.Application.Common.Interfaces;
using BoardGameAiDashboard.Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace BoardGameAiDashboard.Infrastructure.Services;

/// <summary>
/// Qdrant-backed vector search service.
/// Provides similarity search, upsert/delete and collection management.
/// </summary>
public sealed class VectorSearchService : IVectorSearchService
{
    private readonly QdrantClient _client;
    private readonly QdrantSettings _settings;
    private readonly ILogger<VectorSearchService> _logger;

    public VectorSearchService(
        QdrantClient client,
        IOptions<QdrantSettings> settings,
        ILogger<VectorSearchService> logger)
    {
        _client = client;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        float[] queryEmbedding,
        int topK,
        Guid? gameId,
        CancellationToken cancellationToken)
    {
        // Build metadata filter if gameId is provided
        Filter? filter = null;
        if (gameId.HasValue)
        {
            filter = new Filter
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "game_id",
                            Match = new Match { Text = gameId.Value.ToString() }
                        }
                    }
                }
            };
        }

        var results = await _client.SearchAsync(
            collectionName: _settings.CollectionName,
            vector: queryEmbedding,
            limit: (ulong)topK,
            filter: filter,
            cancellationToken: cancellationToken);

        var searchResults = new List<VectorSearchResult>();

        foreach (var result in results)
        {
            var payload = result.Payload;

            var content = payload.ContainsKey("content")
                ? payload["content"].StringValue
                : string.Empty;

            var sectionTitle = payload.ContainsKey("section_title")
                ? payload["section_title"].StringValue
                : string.Empty;

            var gameIdStr = payload.ContainsKey("game_id")
                ? payload["game_id"].StringValue
                : string.Empty;

            if (Guid.TryParse(gameIdStr, out var resultGameId))
            {
                searchResults.Add(new VectorSearchResult(
                    result.Id.ToString(),
                    result.Score,
                    content,
                    sectionTitle,
                    resultGameId));
            }
        }

        _logger.LogDebug(
            "Qdrant search returned {Count} results for topK={TopK}, gameId={GameId}",
            searchResults.Count, topK, gameId);

        return searchResults;
    }

    /// <inheritdoc />
    public async Task UpsertAsync(
        string pointId,
        float[] embedding,
        Dictionary<string, string> metadata,
        CancellationToken cancellationToken)
    {
        var guidId = Guid.TryParse(pointId, out var parsed) ? parsed : Guid.NewGuid();

        var payload = new Dictionary<string, Value>();
        foreach (var kvp in metadata)
        {
            payload[kvp.Key] = new Value { StringValue = kvp.Value };
        }

        var point = new PointStruct
        {
            Id = guidId,
            Vectors = embedding,
            Payload = { payload }
        };

        await _client.UpsertAsync(
            collectionName: _settings.CollectionName,
            points: new[] { point },
            cancellationToken: cancellationToken);

        _logger.LogDebug("Upserted point {PointId} to Qdrant", pointId);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(
        string pointId,
        CancellationToken cancellationToken)
    {
        if (Guid.TryParse(pointId, out var guidId))
        {
            await _client.DeleteAsync(
                collectionName: _settings.CollectionName,
                ids: new[] { guidId },
                cancellationToken: cancellationToken);

            _logger.LogDebug("Deleted point {PointId} from Qdrant", pointId);
        }
    }

    /// <inheritdoc />
    public async Task EnsureCollectionAsync(CancellationToken cancellationToken = default)
    {
        var collections = await _client.ListCollectionsAsync(cancellationToken);

        if (!collections.Contains(_settings.CollectionName))
        {
            _logger.LogInformation(
                "Creating Qdrant collection '{Collection}' with dimension {Dimension}",
                _settings.CollectionName, _settings.VectorDimension);

            await _client.CreateCollectionAsync(
                collectionName: _settings.CollectionName,
                vectorsConfig: new VectorParams
                {
                    Size = (ulong)_settings.VectorDimension,
                    Distance = Distance.Cosine
                },
                cancellationToken: cancellationToken);
        }
    }
}
