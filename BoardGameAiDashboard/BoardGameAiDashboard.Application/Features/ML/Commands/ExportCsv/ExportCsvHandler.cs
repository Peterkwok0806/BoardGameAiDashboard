using BoardGameAiDashboard.Application.Features.ML.Interfaces;
using MediatR;

namespace BoardGameAiDashboard.Application.Features.ML.Commands.ExportCsv;

/// <summary>
/// Handler for ExportCsvCommand.
/// </summary>
public sealed class ExportCsvHandler : IRequestHandler<ExportCsvCommand, ExportCsvResult>
{
    private readonly ICsvExportService _csvExportService;

    public ExportCsvHandler(ICsvExportService csvExportService)
    {
        _csvExportService = csvExportService;
    }

    public async Task<ExportCsvResult> Handle(ExportCsvCommand request, CancellationToken ct)
    {
        var base64Content = await _csvExportService.ExportToCsvAsync(
            request.GameId,
            request.Limit,
            ct);

        var count = await _csvExportService.GetExportableCountAsync(request.GameId, ct);

        return new ExportCsvResult
        {
            FileName = string.IsNullOrEmpty(request.GameId.ToString())
                ? $"training_data_all_{DateTime.UtcNow:yyyyMMddHHmmss}.csv"
                : $"training_data_{request.GameId}_{DateTime.UtcNow:yyyyMMddHHmmss}.csv",
            ContentBase64 = base64Content,
            RecordCount = Math.Min(count, request.Limit ?? count)
        };
    }
}
