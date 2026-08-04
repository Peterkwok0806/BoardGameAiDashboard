using MediatR;

namespace BoardGameAiDashboard.Application.Features.ML.Commands.ExportCsv;

/// <summary>
/// Command to export MatchHistory data to CSV for ML training.
/// </summary>
public sealed record ExportCsvCommand : IRequest<ExportCsvResult>
{
    /// <summary>Optional game ID filter.</summary>
    public Guid? GameId { get; init; }

    /// <summary>Maximum number of records to export.</summary>
    public int? Limit { get; init; }
}

/// <summary>
/// Result of CSV export operation.
/// </summary>
public sealed class ExportCsvResult
{
    /// <summary>Generated filename.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>CSV content encoded as Base64.</summary>
    public string ContentBase64 { get; set; } = string.Empty;

    /// <summary>Number of records exported.</summary>
    public int RecordCount { get; set; }
}
