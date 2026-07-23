namespace BoardGameAiDashboard.Application.Common.Interfaces;

/// <summary>
/// Extracts text content from PDF files.
/// Implementation lives in the Infrastructure layer.
/// </summary>
public interface IPdfParser
{
    /// <summary>
    /// Extract all text from a PDF file at the given path.
    /// </summary>
    /// <param name="filePath">Absolute or relative path to the PDF file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Full extracted text content.</returns>
    Task<string> ExtractTextAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extract text from a PDF stream.
    /// The stream must be readable and, ideally, seekable.
    /// If not seekable, the implementation will buffer into a MemoryStream internally.
    /// </summary>
    /// <param name="pdfStream">Readable PDF stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Full extracted text content.</returns>
    Task<string> ExtractTextAsync(
        Stream pdfStream,
        CancellationToken cancellationToken = default);
}
