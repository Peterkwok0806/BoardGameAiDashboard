namespace BoardGameAiDashboard.Application.Features.ML.Interfaces;

/// <summary>
/// Service for exporting MatchHistory data to CSV format for ML training.
/// </summary>
public interface ICsvExportService
{
    /// <summary>
    /// Exports MatchHistory records to CSV format.
    /// </summary>
    /// <param name="gameId">Optional game ID filter.</param>
    /// <param name="limit">Maximum number of records to export.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Base64-encoded CSV content.</returns>
    Task<string> ExportToCsvAsync(
        Guid? gameId = null,
        int? limit = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the count of exportable records.
    /// </summary>
    Task<int> GetExportableCountAsync(
        Guid? gameId = null,
        CancellationToken ct = default);
}
