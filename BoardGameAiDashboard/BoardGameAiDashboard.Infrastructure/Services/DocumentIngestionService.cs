using System.Text.RegularExpressions;
using BoardGameAiDashboard.Application.Common.Interfaces;
using BoardGameAiDashboard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Embeddings;

namespace BoardGameAiDashboard.Infrastructure.Services;

/// <summary>
/// Orchestrates PDF ingestion: parse → chunk → embed → store (EF Core + Qdrant).
/// </summary>
public sealed class DocumentIngestionService : IDocumentIngestionService
{
    private readonly IPdfParser _pdfParser;
    private readonly IDocumentChunker _documentChunker;
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private readonly IVectorSearchService _vectorSearchService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DocumentIngestionService> _logger;

    public DocumentIngestionService(
        IPdfParser pdfParser,
        IDocumentChunker documentChunker,
        ITextEmbeddingGenerationService embeddingService,
        IVectorSearchService vectorSearchService,
        IUnitOfWork unitOfWork,
        ILogger<DocumentIngestionService> logger)
    {
        _pdfParser = pdfParser;
        _documentChunker = documentChunker;
        _embeddingService = embeddingService;
        _vectorSearchService = vectorSearchService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> IngestGameRulesAsync(
        Guid gameId,
        Stream pdfStream,
        IReadOnlyList<string>? sectionTitles = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting PDF ingestion for GameId={GameId} from stream (Length={Length}, CanSeek={CanSeek})",
            gameId, pdfStream.CanSeek ? pdfStream.Length : (long?)null, pdfStream.CanSeek);

        // PdfPig requires a seekable stream for Open(); buffer if non-seekable.
        Stream streamToUse;
        MemoryStream? rentedBuffer = null;

        if (!pdfStream.CanSeek)
        {
            rentedBuffer = new MemoryStream();
            await pdfStream.CopyToAsync(rentedBuffer, cancellationToken);
            rentedBuffer.Position = 0;
            streamToUse = rentedBuffer;
        }
        else
        {
            streamToUse = pdfStream;
        }

        try
        {
            // Pass the seekable stream directly to the parser — no byte[] copy.
            var rawText = await _pdfParser.ExtractTextAsync(streamToUse, cancellationToken);

            if (string.IsNullOrWhiteSpace(rawText))
            {
                _logger.LogWarning(
                    "PDF extraction returned empty content for GameId={GameId}", gameId);
                return 0;
            }

            return await ProcessIngestionAsync(gameId, rawText, sectionTitles, cancellationToken);
        }
        finally
        {
            rentedBuffer?.Dispose();
        }
    }

    /// <summary>
    /// Shared ingestion pipeline: segment → chunk → embed → store.
    /// Any failure during chunk processing will throw immediately —
    /// partial ingestion of corrupted/incomplete data is NOT acceptable.
    /// </summary>
    private async Task<int> ProcessIngestionAsync(
        Guid gameId,
        string rawText,
        IReadOnlyList<string>? sectionTitles,
        CancellationToken cancellationToken)
    {
        // 2. Segment into named sections
        var sections = SegmentIntoSections(rawText, sectionTitles);

        _logger.LogInformation(
            "Segmented text into {SectionCount} sections", sections.Count);

        // 3. Chunk all sections
        var chunks = _documentChunker.ChunkAll(sections, gameId);

        if (chunks.Count == 0)
        {
            _logger.LogWarning("No chunks produced from PDF for GameId={GameId}", gameId);
            return 0;
        }

        _logger.LogInformation(
            "Produced {ChunkCount} chunks from {SectionCount} sections",
            chunks.Count, sections.Count);

        // 4. Delete existing chunks for this game (re-ingestion)
        await DeleteExistingChunksAsync(gameId, cancellationToken);

        // 5. Ensure Qdrant collection exists
        await _vectorSearchService.EnsureCollectionAsync(cancellationToken);

        // 6. Process each chunk: embed → store in Qdrant → save to EF Core
        //    Any exception will propagate outward immediately — no silent swallowing.
        var ingestedCount = 0;

        foreach (var chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 6a. Generate embedding
            var embeddingResult = await _embeddingService
                .GenerateEmbeddingAsync(chunk.Content, cancellationToken: cancellationToken);
            var embeddingArray = embeddingResult.ToArray();

            // 6b. Create Qdrant point ID
            var qdrantPointId = Guid.NewGuid().ToString();

            // 6c. Upsert to Qdrant with metadata
            var metadata = new Dictionary<string, string>
            {
                ["game_id"] = chunk.GameId.ToString(),
                ["section_title"] = chunk.SectionTitle,
                ["content"] = chunk.Content
            };

            await _vectorSearchService.UpsertAsync(
                qdrantPointId, embeddingArray, metadata, cancellationToken);

            // 6d. Save to EF Core (GameRuleChunk entity)
            var ruleChunk = new GameRuleChunk(
                chunk.GameId,
                chunk.Content,
                chunk.SectionTitle,
                qdrantPointId);

            await _unitOfWork.Rules.AddAsync(ruleChunk, cancellationToken);
            ingestedCount++;
        }

        // 7. Persist all EF Core changes in one transaction
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Ingestion complete for GameId={GameId}: {Count}/{Total} chunks stored",
            gameId, ingestedCount, chunks.Count);

        return ingestedCount;
    }

    /// <summary>
    /// Delete existing rule chunks for a game from both Qdrant and EF Core.
    /// Uses projection to read only QdrantPointId, then ExecuteDeleteAsync for bulk delete.
    /// No tracking conflicts — projection results are never attached to the change tracker.
    /// </summary>
    private async Task DeleteExistingChunksAsync(
        Guid gameId, CancellationToken cancellationToken)
    {
        // Step A: Read only QdrantPointId via projection (not a tracked entity).
        var qdrantIds = await _unitOfWork.Rules
            .Query()
            .Where(c => c.GameId == gameId)
            .Select(c => c.QdrantPointId)
            .ToListAsync(cancellationToken);

        // Step B: Delete from Qdrant vector store
        foreach (var qdrantId in qdrantIds)
        {
            await _vectorSearchService.DeleteAsync(qdrantId, cancellationToken);
        }

        // Step C: Bulk delete from EF Core using ExecuteDeleteAsync (raw SQL delete)
        if (qdrantIds.Count > 0)
        {
            await _unitOfWork.Rules
                .Query()
                .Where(c => c.GameId == gameId)
                .ExecuteDeleteAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Deleted {Count} existing chunks for GameId={GameId}",
            qdrantIds.Count, gameId);
    }

    /// <summary>
    /// Segment raw text into named sections.
    /// If sectionTitles are provided, uses them as split markers.
    /// Otherwise, attempts heuristic heading detection.
    /// Falls back to a single "Full Document" section.
    /// </summary>
    private static IReadOnlyList<DocumentSection> SegmentIntoSections(
        string rawText,
        IReadOnlyList<string>? sectionTitles)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return Array.Empty<DocumentSection>();

        var cleanText = rawText.Replace("\r\n", "\n").Replace("\r", "\n");

        // If explicit section titles provided, split by them
        if (sectionTitles != null && sectionTitles.Count > 0)
        {
            return SplitBySectionTitles(cleanText, sectionTitles);
        }

        // Heuristic: try to find markdown-style headings (## Title) or ALL CAPS lines
        var sections = TryHeuristicSplit(cleanText);
        if (sections.Count > 1)
            return sections;

        // Fallback: treat entire document as a single section
        return new List<DocumentSection>
        {
            new("Full Document", cleanText.Trim())
        };
    }

    /// <summary>
    /// Split text by explicit section title markers.
    /// </summary>
    private static IReadOnlyList<DocumentSection> SplitBySectionTitles(
        string text, IReadOnlyList<string> sectionTitles)
    {
        var sections = new List<DocumentSection>();
        var lines = text.Split('\n');
        var currentTitle = sectionTitles[0];
        var currentContent = new System.Text.StringBuilder();

        var titleSet = new HashSet<string>(
            sectionTitles.Select(t => t.Trim()),
            StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            // Check if this line matches any section title
            if (titleSet.Contains(trimmedLine))
            {
                // Save previous section
                if (currentContent.Length > 0)
                {
                    sections.Add(new DocumentSection(
                        currentTitle, currentContent.ToString().Trim()));
                }

                currentTitle = trimmedLine;
                currentContent.Clear();
            }
            else
            {
                currentContent.AppendLine(line);
            }
        }

        // Save final section
        if (currentContent.Length > 0)
        {
            sections.Add(new DocumentSection(
                currentTitle, currentContent.ToString().Trim()));
        }

        return sections;
    }

    /// <summary>
    /// Attempt to detect section breaks via heuristics:
    /// - Lines that are TITLE CASE or ALL CAPS (>= 3 chars, no period)
    /// - Lines starting with ## or ### (markdown headings)
    /// </summary>
    private static IReadOnlyList<DocumentSection> TryHeuristicSplit(string text)
    {
        var sections = new List<DocumentSection>();
        var lines = text.Split('\n');
        var currentTitle = "Preamble";
        var currentContent = new System.Text.StringBuilder();

        var headingPattern = new Regex(
            @"^(#{1,3}\s+.+|[A-Z][A-Z\s]{2,}[^\.\n]*$|[A-Z][a-z]+(?:\s+[A-Z][a-z]+)+\s*$)",
            RegexOptions.Multiline);

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            if (trimmedLine.Length > 3 &&
                trimmedLine.Length < 80 &&
                headingPattern.IsMatch(trimmedLine) &&
                !trimmedLine.Contains("  "))
            {
                // Save previous section
                if (currentContent.Length > 0)
                {
                    sections.Add(new DocumentSection(
                        currentTitle, currentContent.ToString().Trim()));
                }

                // Clean markdown heading markers
                currentTitle = trimmedLine
                    .Replace("#", string.Empty)
                    .Trim();

                currentContent.Clear();
            }
            else
            {
                currentContent.AppendLine(line);
            }
        }

        // Save final section
        if (currentContent.Length > 0)
        {
            sections.Add(new DocumentSection(
                currentTitle, currentContent.ToString().Trim()));
        }

        return sections;
    }
}
