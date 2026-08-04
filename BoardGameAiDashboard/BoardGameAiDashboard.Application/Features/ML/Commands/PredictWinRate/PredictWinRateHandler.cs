using BoardGameAiDashboard.Application.Common.Exceptions;
using BoardGameAiDashboard.Application.Features.ML.Interfaces;
using BoardGameAiDashboard.Application.Features.ML.Models;
using MediatR;

namespace BoardGameAiDashboard.Application.Features.ML.Commands.PredictWinRate;

/// <summary>
/// Handler for PredictWinRateCommand.
/// </summary>
public sealed class PredictWinRateHandler
    : IRequestHandler<PredictWinRateCommand, GameStatePredictionResult>
{
    private readonly IWinRatePredictionService _predictionService;

    public PredictWinRateHandler(IWinRatePredictionService predictionService)
    {
        _predictionService = predictionService;
    }

    public async Task<GameStatePredictionResult> Handle(
        PredictWinRateCommand request,
        CancellationToken ct)
    {
        if (!_predictionService.IsModelLoaded)
        {
            throw new PredictionException("ONNX model not loaded. ML prediction is unavailable.", "ModelNotLoaded");
        }

        return await _predictionService.PredictWinRateAsync(request.Input, ct);
    }
}
