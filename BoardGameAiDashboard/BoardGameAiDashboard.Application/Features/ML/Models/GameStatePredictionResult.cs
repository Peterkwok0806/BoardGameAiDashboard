namespace BoardGameAiDashboard.Application.Features.ML.Models;

/// <summary>
/// Result of win rate prediction.
/// </summary>
public sealed class GameStatePredictionResult
{
    /// <summary>
    /// Win probability (0.0 - 1.0).
    /// </summary>
    public float WinProbability { get; init; }

    /// <summary>
    /// Confidence score based on prediction probability distance from 0.5.
    /// Higher value = more confident prediction.
    /// </summary>
    public float ConfidenceScore { get; init; }

    /// <summary>
    /// Key factors influencing the prediction.
    /// </summary>
    public List<FeatureImpact> KeyFactors { get; init; } = new();

    /// <summary>
    /// Strategic recommendation based on the prediction.
    /// </summary>
    public string Recommendation { get; init; } = string.Empty;
}

/// <summary>
/// Represents a feature's impact on the prediction.
/// </summary>
public sealed class FeatureImpact
{
    /// <summary>Feature name.</summary>
    public string FeatureName { get; init; } = string.Empty;

    /// <summary>Impact score (-1.0 to 1.0).</summary>
    public float ImpactScore { get; init; }

    /// <summary>Human-readable description.</summary>
    public string Description { get; init; } = string.Empty;
}
