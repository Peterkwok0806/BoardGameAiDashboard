using System.Globalization;
using System.Linq;
using BoardGameAiDashboard.Application.Common.Interfaces;
using BoardGameAiDashboard.Application.Features.ML.Interfaces;
using BoardGameAiDashboard.Domain.Entities;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BoardGameAiDashboard.Infrastructure.Services.ML;

/// <summary>
/// Exports MatchHistory data to CSV format for ML training.
/// .NET only exports raw features - feature engineering is handled by Python.
/// </summary>
public sealed class CsvExportService : ICsvExportService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CsvExportService> _logger;

    // CSV header columns (lowercase, matching Python expectations)
    private static readonly string[] CsvColumns =
    [
        "player_count", "hour_of_day", "day_of_week", "hero_level",
        "hero_kills", "deaths", "unit_kills", "total_gold",
        "highest_atk", "highest_def", "highest_speed", "atk_range", "is_winner"
    ];

    public CsvExportService(IUnitOfWork unitOfWork, ILogger<CsvExportService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> ExportToCsvAsync(
        Guid? gameId = null,
        int? limit = null,
        CancellationToken ct = default)
    {
        var matches = _unitOfWork.Matches.Query();

        // Apply filters
        if (gameId.HasValue)
        {
            matches = matches.Where(m => m.GameId == gameId.Value);
        }

        matches = matches.OrderByDescending(m => m.PlayedAt);

        if (limit.HasValue)
        {
            matches = matches.Take(limit.Value);
        }

        var matchList = await matches.ToListAsync(ct);

        // Convert to CSV rows
        var rows = matchList.Select(m => ParseMatchToRow(m)).ToList();

        _logger.LogInformation(
            "Exporting {Count} MatchHistory records to CSV",
            rows.Count);

        // Generate CSV with CsvHelper
        using var memoryStream = new MemoryStream();
        using var writer = new StreamWriter(memoryStream, leaveOpen: true);
        using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true
        });

        // Write header
        foreach (var column in CsvColumns)
        {
            csv.WriteField(column);
        }
        await csv.NextRecordAsync();

        // Write data rows
        foreach (var row in rows)
        {
            csv.WriteField(row.PlayerCount);
            csv.WriteField(row.HourOfDay);
            csv.WriteField(row.DayOfWeek);
            csv.WriteField(row.HeroLevel);
            csv.WriteField(row.HeroKills);
            csv.WriteField(row.Deaths);
            csv.WriteField(row.UnitKills);
            csv.WriteField(row.TotalGold);
            csv.WriteField(row.HighestAtk);
            csv.WriteField(row.HighestDef);
            csv.WriteField(row.HighestSpeed);
            csv.WriteField(row.AtkRange);
            csv.WriteField(row.IsWinner);
            await csv.NextRecordAsync();
        }

        await writer.FlushAsync();
        return Convert.ToBase64String(memoryStream.ToArray());
    }

    /// <inheritdoc />
    public async Task<int> GetExportableCountAsync(
        Guid? gameId = null,
        CancellationToken ct = default)
    {
        var query = _unitOfWork.Matches.Query();

        if (gameId.HasValue)
        {
            query = query.Where(m => m.GameId == gameId.Value);
        }

        return await query.CountAsync(ct);
    }

    /// <summary>
    /// Parses MatchHistory GameFeatures dictionary to CSV row.
    /// GameFeatures is stored as Dictionary&lt;string, string&gt; in the database.
    /// </summary>
    private CsvRow ParseMatchToRow(MatchHistory match)
    {
        var features = match.GameFeatures;

        return new CsvRow
        {
            PlayerCount = match.PlayerCount,
            HourOfDay = match.PlayedAt.Hour,
            DayOfWeek = (int)match.PlayedAt.DayOfWeek,
            HeroLevel = GetInt(features, "hero_level"),
            HeroKills = GetInt(features, "hero_killed"),
            Deaths = GetInt(features, "death"),
            UnitKills = GetInt(features, "unit_killed"),
            TotalGold = GetInt(features, "total_gold"),
            HighestAtk = GetInt(features, "highest_atk"),
            HighestDef = GetInt(features, "highest_def"),
            HighestSpeed = GetInt(features, "highest_speed"),
            AtkRange = GetInt(features, "atk_range"),
            IsWinner = match.IsWinner ? 1 : 0
        };
    }

    /// <summary>
    /// Safely gets an integer value from the features dictionary.
    /// </summary>
    private static int GetInt(Dictionary<string, string> dict, string key)
        => dict.TryGetValue(key, out var value) && int.TryParse(value, out var result)
            ? result
            : 0;

    /// <summary>
    /// Internal record for CSV row data.
    /// </summary>
    private record CsvRow
    {
        public int PlayerCount { get; init; }
        public int HourOfDay { get; init; }
        public int DayOfWeek { get; init; }
        public int HeroLevel { get; init; }
        public int HeroKills { get; init; }
        public int Deaths { get; init; }
        public int UnitKills { get; init; }
        public int TotalGold { get; init; }
        public int HighestAtk { get; init; }
        public int HighestDef { get; init; }
        public int HighestSpeed { get; init; }
        public int AtkRange { get; init; }
        public int IsWinner { get; init; }
    }
}
