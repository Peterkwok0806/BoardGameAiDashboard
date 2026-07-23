using MediatR;

namespace BoardGameAiDashboard.Application.Features.GameRules.Commands.IngestGameRules;

/// <summary>
/// Command to ingest a game rulebook PDF into the RAG vector database.
/// Triggers the full pipeline: parse PDF → chunk → embed → store.
/// </summary>
public sealed record IngestGameRulesCommand : IRequest<IngestGameRulesResult>
{
    /// <summary>Game identifier this rulebook belongs to.</summary>
    public Guid GameId { get; init; }

    /// <summary>
    /// PDF file stream uploaded via multipart form.
    /// The stream is owned by the caller (typically the Controller's <c>await using</c>
    /// lifetime) and is consumed once during ingestion.
    /// </summary>
    public Stream PdfStream { get; init; } = Stream.Null;

    /// <summary>Optional section titles for semantic segmentation.</summary>
    public IReadOnlyList<string>? SectionTitles { get; init; }
}

/// <summary>
/// Result of the ingestion operation.
/// </summary>
public sealed record IngestGameRulesResult
{
    /// <summary>Number of chunks successfully ingested.</summary>
    public int ChunksIngested { get; init; }

    /// <summary>Success message.</summary>
    public string Message { get; init; } = string.Empty;
}
