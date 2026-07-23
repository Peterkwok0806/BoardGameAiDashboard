using System.Text;
using BoardGameAiDashboard.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace BoardGameAiDashboard.Infrastructure.Services;

/// <summary>
/// PDF text extraction using PdfPig library.
/// Supports file path, stream, and byte array inputs.
/// </summary>
public sealed class PdfPigPdfParser : IPdfParser
{
    private readonly ILogger<PdfPigPdfParser> _logger;

    public PdfPigPdfParser(ILogger<PdfPigPdfParser> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<string> ExtractTextAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Extracting text from PDF file: {FilePath}", filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"PDF file not found: {filePath}");
        }

        // Open a FileStream (seekable, avoids loading entire file into byte[])
        using var fileStream = File.OpenRead(filePath);
        return ExtractTextFromStreamAsync(fileStream, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> ExtractTextAsync(
        Stream pdfStream,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Extracting text from PDF stream (Length={Length}, CanSeek={CanSeek})",
            pdfStream.CanSeek ? pdfStream.Length : (long?)null,
            pdfStream.CanSeek);

        // PdfPig requires a seekable stream; buffer if necessary
        if (!pdfStream.CanSeek)
        {
            using var ms = new MemoryStream();
            await pdfStream.CopyToAsync(ms, cancellationToken);
            ms.Position = 0;
            return await ExtractTextFromStreamAsync(ms, cancellationToken);
        }

        return await ExtractTextFromStreamAsync(pdfStream, cancellationToken);
    }

    private Task<string> ExtractTextFromStreamAsync(
        Stream seekableStream,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sb = new StringBuilder();

        using var document = PdfDocument.Open(seekableStream);

        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var text = page.Text;
            if (!string.IsNullOrWhiteSpace(text))
            {
                sb.AppendLine(text);
                sb.AppendLine(); // blank line between pages
            }
        }

        var result = sb.ToString().Trim();

        _logger.LogInformation(
            "PDF extraction complete: {CharCount} characters from {PageCount} page(s)",
            result.Length, document.GetPages().Count());

        return Task.FromResult(result);
    }
}
