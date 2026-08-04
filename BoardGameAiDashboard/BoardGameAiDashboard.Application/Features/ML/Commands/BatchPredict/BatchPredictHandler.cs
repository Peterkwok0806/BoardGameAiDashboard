using BoardGameAiDashboard.Application.Common.Exceptions;
using BoardGameAiDashboard.Application.Features.ML.Interfaces;
using BoardGameAiDashboard.Application.Features.ML.Models;
using MediatR;

namespace BoardGameAiDashboard.Application.Features.ML.Commands.BatchPredict;

/// <summary>
/// Handler for BatchPredictCommand.
/// </summary>
public sealed class BatchPredictHandler
    : IRequestHandler<BatchPredictCommand, BatchPredictionResult>
{
    private readonly IWinRatePredictionService _predictionService;

    public BatchPredictHandler(IWinRatePredictionService predictionService)
    {
        _predictionService = predictionService;
    }

    public async Task<BatchPredictionResult> Handle(
        BatchPredictCommand request,
        CancellationToken ct)
    {
        if (!_predictionService.IsModelLoaded)
        {
            throw new PredictionException("ONNX model not loaded. ML prediction is unavailable.", "ModelNotLoaded");
        }

        return await _predictionService.BatchPredictAsync(request.Inputs, ct);
    }
}
