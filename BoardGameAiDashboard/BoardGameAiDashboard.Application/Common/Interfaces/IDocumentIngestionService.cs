using BoardGameAiDashboard.Application.Common.Interfaces;

namespace BoardGameAiDashboard.Application.Common.Interfaces;

/// <summary>
/// Orchestrates PDF ingestion: parse → chunk → embed → store.
/// Coordinates PdfParser, DocumentChunker, EmbeddingService, and VectorSearchService.
/// Implementation lives in the Infrastructure layer.
/// </summary>
public interface IDocumentIngestionService
{
    /// <summary>
    /// Ingest a game rulebook PDF from a stream.
    /// Preferred method for web API scenarios (avoids loading entire file into memory).
    /// </summary>
    /// <param name="gameId">Game identifier for metadata.</param>
    /// <param name="pdfStream">PDF file stream.</param>
    /// <param name="sectionTitles">Optional ordered section titles for semantic segmentation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of chunks ingested.</returns>
    Task<int> IngestGameRulesAsync(
        Guid gameId,
        Stream pdfStream,
        IReadOnlyList<string>? sectionTitles = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ingest a game rulebook PDF from a file path.
    /// Typically used for CLI or background job scenarios.
    /// </summary>
    /// <param name="gameId">Game identifier for metadata.</param>
    /// <param name="pdfFilePath">Path to the PDF file.</param>
    /// <param name="sectionTitles">Optional ordered section titles for semantic segmentation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of chunks ingested.</returns>
    Task<int> IngestGameRulesAsync(
        Guid gameId,
        string pdfFilePath,
        IReadOnlyList<string>? sectionTitles = null,
        CancellationToken cancellationToken = default);
}
