using BoardGameAiDashboard.Application.Features.ML.Commands.BatchPredict;
using BoardGameAiDashboard.Application.Features.ML.Commands.ExportCsv;
using BoardGameAiDashboard.Application.Features.ML.Commands.PredictWinRate;
using BoardGameAiDashboard.Application.Features.ML.Interfaces;
using BoardGameAiDashboard.Application.Features.ML.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoardGameAiDashboard.Api.Controllers;

/// <summary>
/// ML Prediction endpoints for win rate prediction and CSV export.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class PredictionsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IWinRatePredictionService _predictionService;

    public PredictionsController(
        ISender sender,
        IWinRatePredictionService predictionService)
    {
        _sender = sender;
        _predictionService = predictionService;
    }

    // ========================================================================
    // Win Rate Prediction Endpoints
    // ========================================================================

    /// <summary>
    /// Predicts win probability based on game state features.
    /// </summary>
    /// <param name="input">Game state features.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Prediction result with probability and insights.</returns>
    [HttpPost("predict")]
    [ProducesResponseType(typeof(GameStatePredictionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PredictWinRate(
        [FromBody] GameStatePredictionInput input,
        CancellationToken ct = default)
    {
        if (!_predictionService.IsModelLoaded)
        {
            return NotFound(new { message = "ML 模型尚未載入。請先部署 ONNX 模型。" });
        }

        var command = new PredictWinRateCommand { Input = input };
        var result = await _sender.Send(command, ct);
        return Ok(result);
    }

    /// <summary>
    /// Analyzes win rate across different hero levels.
    /// Useful for understanding level progression impact.
    /// </summary>
    /// <param name="gameId">Game identifier for tracking.</param>
    /// <param name="heroLevel">Base hero level (center of analysis range).</param>
    /// <param name="heroKills">Hero kills count.</param>
    /// <param name="deaths">Deaths count.</param>
    /// <param name="totalGold">Total gold amount.</param>
    /// <param name="unitKills">Unit kills count.</param>
    /// <param name="highestAtk">Highest attack stat.</param>
    /// <param name="highestDef">Highest defense stat.</param>
    /// <param name="highestSpeed">Highest speed stat.</param>
    /// <param name="playerCount">Number of players.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Dictionary with hero levels and corresponding win probabilities.</returns>
    [HttpGet("analyze-level")]
    [ProducesResponseType(typeof(Dictionary<string, List<float>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AnalyzeWinRateByLevel(
        [FromQuery] Guid? gameId = null,
        [FromQuery] float heroLevel = 15,
        [FromQuery] float heroKills = 5,
        [FromQuery] float deaths = 3,
        [FromQuery] float totalGold = 5000,
        [FromQuery] float unitKills = 30,
        [FromQuery] float highestAtk = 120,
        [FromQuery] float highestDef = 80,
        [FromQuery] float highestSpeed = 350,
        [FromQuery] float playerCount = 5,
        CancellationToken ct = default)
    {
        if (!_predictionService.IsModelLoaded)
        {
            return NotFound(new { message = "ML 模型尚未載入。" });
        }

        // Build batch of inputs (11 levels: heroLevel-5 to heroLevel+5)
        var inputs = new List<GameStatePredictionInput>();
        for (int level = Math.Max(1, (int)heroLevel - 5); level <= heroLevel + 5; level++)
        {
            inputs.Add(new GameStatePredictionInput
            {
                GameId = gameId,
                PlayerCount = playerCount,
                HeroLevel = level,
                HeroKills = heroKills,
                Deaths = deaths,
                TotalGold = totalGold + (level - (int)heroLevel) * 100,
                UnitKills = unitKills,
                HighestAtk = highestAtk + (level - (int)heroLevel) * 5,
                HighestDef = highestDef + (level - (int)heroLevel) * 3,
                HighestSpeed = highestSpeed + (level - (int)heroLevel) * 2,
                AtkRange = 150,
                HourOfDay = DateTime.UtcNow.Hour,
                DayOfWeek = (int)DateTime.UtcNow.DayOfWeek
            });
        }

        // Use batch prediction for efficiency
        var command = new BatchPredictCommand { Inputs = inputs };
        var result = await _sender.Send(command, ct);

        var predictions = new Dictionary<string, List<float>>
        {
            ["heroLevels"] = result.Predictions.Select(p => (float)p.Input.HeroLevel).ToList(),
            ["winProbabilities"] = result.Predictions.Select(p => p.WinProbability).ToList()
        };

        return Ok(predictions);
    }

    /// <summary>
    /// Gets the current ML model status.
    /// </summary>
    /// <returns>Model loading status and metadata.</returns>
    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetModelStatus()
    {
        return Ok(new
        {
            modelLoaded = _predictionService.IsModelLoaded,
            modelPath = _predictionService.ModelPath,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Reloads the ONNX model from disk.
    /// Use this to hot-reload a new model without restarting the application.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success message.</returns>
    [HttpPost("reload-model")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ReloadModel(CancellationToken ct)
    {
        try
        {
            await _predictionService.ReloadModelAsync(ct);
            return Ok(new { message = "模型已重新載入", modelPath = _predictionService.ModelPath });
        }
        catch
        {
            return StatusCode(500, new { message = "模型載入失敗" });
        }
    }

    // ========================================================================
    // CSV Export Endpoints
    // ========================================================================

    /// <summary>
    /// Exports MatchHistory data as Base64-encoded CSV.
    /// Used for generating training data for ML model training.
    /// </summary>
    /// <param name="gameId">Optional game ID filter.</param>
    /// <param name="limit">Maximum number of records to export.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Export result with Base64 content.</returns>
    [HttpGet("export")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ExportCsvResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] Guid? gameId = null,
        [FromQuery] int? limit = null,
        CancellationToken ct = default)
    {
        var command = new ExportCsvCommand { GameId = gameId, Limit = limit };
        var result = await _sender.Send(command, ct);
        return Ok(result);
    }

    /// <summary>
    /// Downloads MatchHistory data as a CSV file.
    /// </summary>
    /// <param name="gameId">Optional game ID filter.</param>
    /// <param name="limit">Maximum number of records to export.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>CSV file download.</returns>
    [HttpGet("download")]
    [Authorize(Roles = "Admin")]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadCsv(
        [FromQuery] Guid? gameId = null,
        [FromQuery] int? limit = null,
        CancellationToken ct = default)
    {
        var command = new ExportCsvCommand { GameId = gameId, Limit = limit };
        var result = await _sender.Send(command, ct);
        var csvBytes = Convert.FromBase64String(result.ContentBase64);

        return File(csvBytes, "text/csv", result.FileName);
    }
}
