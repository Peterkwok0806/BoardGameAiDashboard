using BoardGameAiDashboard.Application.Features.GameRules.Commands.IngestGameRules;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BoardGameAiDashboard.Api.Controllers;

/// <summary>
/// Game rules management — PDF upload and RAG ingestion.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class GameRulesController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ILogger<GameRulesController> _logger;

    public GameRulesController(ISender sender, ILogger<GameRulesController> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    /// <summary>
    /// Upload a game rulebook PDF and ingest it into the RAG vector database.
    /// The pipeline: parse PDF → segment → chunk → embed → store (Qdrant + EF Core).
    /// Re-ingestion for the same game replaces all existing chunks.
    /// </summary>
    /// <param name="gameId">Game identifier this rulebook belongs to.</param>
    /// <param name="pdfFile">PDF file (multipart form upload).</param>
    /// <param name="sectionTitles">Optional comma-separated section titles for segmentation.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{gameId:guid}/ingest")]
    [RequestSizeLimit(120 * 1024 * 1024)] // 120 MB
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> IngestGameRules(
        Guid gameId,
        IFormFile pdfFile,
        [FromQuery] string? sectionTitles = null,
        CancellationToken ct = default)
    {
        if (pdfFile == null || pdfFile.Length == 0)
            return BadRequest("No PDF file uploaded.");

        if (!pdfFile.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only PDF files are accepted.");

        _logger.LogInformation(
            "PDF upload received: GameId={GameId}, File={FileName}, Size={Size} bytes",
            gameId, pdfFile.FileName, pdfFile.Length);

        // Pass the IFormFile stream directly — no Materialize into byte[] / MemoryStream.
        // ASP.NET Core's FormFile exposes a seekable stream via OpenReadStream().
        await using var pdfStream = pdfFile.OpenReadStream();

        // Parse optional section titles from comma-separated query string
        IReadOnlyList<string>? titles = null;
        if (!string.IsNullOrWhiteSpace(sectionTitles))
        {
            titles = sectionTitles
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        var command = new IngestGameRulesCommand
        {
            GameId = gameId,
            PdfStream = pdfStream,
            SectionTitles = titles
        };

        var result = await _sender.Send(command, ct);

        return Ok(result);
    }
}
