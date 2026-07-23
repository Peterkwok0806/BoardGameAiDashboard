using BoardGameAiDashboard.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BoardGameAiDashboard.Application.Features.GameRules.Commands.IngestGameRules;

/// <summary>
/// Handles PDF ingestion: receives a file path, orchestrates the full RAG ingestion pipeline.
/// </summary>
public sealed class IngestGameRulesCommandHandler
    : IRequestHandler<IngestGameRulesCommand, IngestGameRulesResult>
{
    private readonly IDocumentIngestionService _ingestionService;
    private readonly ILogger<IngestGameRulesCommandHandler> _logger;

    public IngestGameRulesCommandHandler(
        IDocumentIngestionService ingestionService,
        ILogger<IngestGameRulesCommandHandler> logger)
    {
        _ingestionService = ingestionService;
        _logger = logger;
    }

    public async Task<IngestGameRulesResult> Handle(
        IngestGameRulesCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Handling IngestGameRules for GameId={GameId}, PdfStreamLength={Length}",
            request.GameId, request.PdfStream.CanSeek ? request.PdfStream.Length : (long?)null);

        var chunksIngested = await _ingestionService.IngestGameRulesAsync(
            request.GameId,
            request.PdfStream,
            sectionTitles: request.SectionTitles,
            cancellationToken: cancellationToken);

        return new IngestGameRulesResult
        {
            ChunksIngested = chunksIngested,
            Message = chunksIngested > 0
                ? $"Successfully ingested {chunksIngested} rule chunks for the game."
                : "No content could be extracted from the PDF."
        };
    }
}
