using BoardGameAiDashboard.Application.Features.ML.Models;

namespace BoardGameAiDashboard.Application.Features.ML.Interfaces;

/// <summary>
/// Service for feature engineering.
/// Must match Python feature_engineering.py exactly.
/// </summary>
public interface IFeatureEngineeringService
{
    /// <summary>
    /// Transforms input features into ML feature vector.
    /// This MUST match the Python feature engineering exactly.
    /// </summary>
    /// <param name="input">Raw input features.</param>
    /// <returns>Feature vector (20 elements) for ONNX model.</returns>
    float[] TransformToFeatureVector(GameStatePredictionInput input);

    /// <summary>
    /// Gets the expected feature column names (for validation).
    /// Order must match the feature vector.
    /// </summary>
    IReadOnlyList<string> FeatureColumns { get; }
}
