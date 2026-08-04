using System.Text.Json;
using BoardGameAiDashboard.Application.Common.Exceptions;
using BoardGameAiDashboard.Application.Features.ML.Interfaces;
using BoardGameAiDashboard.Application.Features.ML.Models;
using BoardGameAiDashboard.Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace BoardGameAiDashboard.Infrastructure.Services.ML;

/// <summary>
/// Win rate prediction service using ONNX Runtime.
/// Loads and executes a pre-trained Random Forest model exported from Python.
/// </summary>
public sealed class WinRatePredictionService : IWinRatePredictionService, IDisposable
{
    private readonly IFeatureEngineeringService _featureEngineering;
    private readonly MLSettings _settings;
    private readonly ILogger<WinRatePredictionService> _logger;

    private InferenceSession? _session;
    private string[]? _featureColumns;
    private readonly object _lock = new();

    public WinRatePredictionService(
        IFeatureEngineeringService featureEngineering,
        IOptions<MLSettings> settings,
        ILogger<WinRatePredictionService> logger)
    {
        _featureEngineering = featureEngineering;
        _settings = settings.Value;
        _logger = logger;

        InitializeModel();
    }

    /// <inheritdoc />
    public bool IsModelLoaded => _session != null;

    /// <inheritdoc />
    public string? ModelPath => _settings.ModelPath;

    /// <inheritdoc />
    public async Task ReloadModelAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Reloading ONNX model: {Path}", _settings.ModelPath);

        // Dispose existing session
        _session?.Dispose();
        _session = null;

        InitializeModel();
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<GameStatePredictionResult> PredictWinRateAsync(
        GameStatePredictionInput input,
        CancellationToken ct = default)
    {
        if (_session == null)
        {
            throw new PredictionException("ONNX model is not loaded. Call ReloadModelAsync first.", "ModelNotLoaded");
        }

        // Transform input to feature vector (matches Python feature engineering exactly)
        var features = _featureEngineering.TransformToFeatureVector(input);

        // Run ONNX inference
        var probability = await PredictWithOnnxAsync(features, ct);

        // Generate insights
        var keyFactors = GenerateKeyFactors(features);
        var recommendation = GenerateRecommendation(keyFactors, probability);

        return new GameStatePredictionResult
        {
            WinProbability = probability,
            ConfidenceScore = CalculateConfidence(probability),
            KeyFactors = keyFactors,
            Recommendation = recommendation
        };
    }

    /// <inheritdoc />
    public async Task<BatchPredictionResult> BatchPredictAsync(
        IReadOnlyList<GameStatePredictionInput> inputs,
        CancellationToken ct = default)
    {
        if (_session == null)
        {
            throw new PredictionException("ONNX model is not loaded. Call ReloadModelAsync first.", "ModelNotLoaded");
        }

        var predictions = new List<BatchPredictionItem>();

        foreach (var input in inputs)
        {
            ct.ThrowIfCancellationRequested();

            // Transform input to feature vector
            var features = _featureEngineering.TransformToFeatureVector(input);

            // Run ONNX inference
            var probability = await PredictWithOnnxAsync(features, ct);

            // Generate insights
            var keyFactors = GenerateKeyFactors(features);
            var recommendation = GenerateRecommendation(keyFactors, probability);

            predictions.Add(new BatchPredictionItem
            {
                Index = predictions.Count,
                Input = input,
                WinProbability = probability,
                ConfidenceScore = CalculateConfidence(probability),
                Recommendation = recommendation
            });
        }

        return new BatchPredictionResult { Predictions = predictions };
    }

    /// <summary>
    /// Runs ONNX inference on the feature vector.
    /// </summary>
    private async Task<float> PredictWithOnnxAsync(float[] features, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                _logger.LogDebug("Feature vector length: {Count}", features.Length);

                // Create input tensor (shape: [1, 20])
                var inputTensor = new DenseTensor<float>(features, new[] { 1, features.Length });

                // Log input tensor info
                _logger.LogDebug("Input tensor shape: [{Dim0}, {Dim1}]", inputTensor.Dimensions[0], inputTensor.Dimensions[1]);

                // Get model input/output names
                var inputNames = _session!.InputNames;
                var outputNames = _session.OutputNames;
                _logger.LogDebug("ONNX model input names: [{Names}]", string.Join(", ", inputNames));
                _logger.LogDebug("ONNX model output names: [{Names}]", string.Join(", ", outputNames));

                // Create named input
                var inputName = inputNames.FirstOrDefault() ?? "float_input";
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor(inputName, inputTensor)
                };

                // Run inference
                using var results = _session.Run(inputs);

                // With zipmap=False, the second output is a tensor with shape [?, 2]
                // containing [P(class0), P(class1)]
                // First output is the predicted label (int64)
                float winProbability;

                if (outputNames.Count >= 2)
                {
                    // Second output is the probability tensor
                    var probOutput = results.ElementAt(1);
                    var tensor = probOutput.AsTensor<float>();
                    var values = tensor.ToArray();

                    var dims = string.Join(",", tensor.Dimensions.ToArray());
                    _logger.LogDebug("Probability tensor shape: [{Dims}], values: [{V0}, {V1}]",
                        dims, values[0], values[1]);

                    // Class 1 = win (second value in the array)
                    winProbability = values[1];
                }
                else
                {
                    _logger.LogError("Expected at least 2 outputs from ONNX model");
                    throw new PredictionException("Invalid ONNX model output format.", "InvalidOutput");
                }

                _logger.LogInformation("Prediction result: WinProb={Prob}", winProbability);

                // Return probability of class 1 (win)
                return winProbability;
            }
        }, ct);
    }

    /// <summary>
    /// Initializes the ONNX model from disk.
    /// </summary>
    private void InitializeModel()
    {
        if (!File.Exists(_settings.ModelPath))
        {
            _logger.LogWarning(
                "ONNX model file not found: {Path}. ML prediction will be unavailable until a model is deployed.",
                _settings.ModelPath);
            return;
        }

        try
        {
            // Configure session options for optimal performance
            var sessionOptions = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
            };

            // Create inference session
            _session = new InferenceSession(_settings.ModelPath, sessionOptions);

            // Load feature columns from JSON if available
            if (File.Exists(_settings.FeatureColumnsPath))
            {
                var json = File.ReadAllText(_settings.FeatureColumnsPath);
                // JSON format: { "feature_columns": [...], ... }
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("feature_columns", out var featureColumnsElement))
                {
                    _featureColumns = JsonSerializer.Deserialize<string[]>(featureColumnsElement.GetRawText());
                }
                else
                {
                    _logger.LogWarning(
                        "Feature columns file missing 'feature_columns' property: {Path}. Using default.",
                        _settings.FeatureColumnsPath);
                    _featureColumns = _featureEngineering.FeatureColumns.ToArray();
                }
            }
            else
            {
                _logger.LogWarning(
                    "Feature columns file not found: {Path}. Using default feature columns.",
                    _settings.FeatureColumnsPath);
                _featureColumns = _featureEngineering.FeatureColumns.ToArray();
            }

            _logger.LogInformation(
                "ONNX model loaded successfully: {Path}, Features: {FeatureCount}",
                _settings.ModelPath,
                _featureColumns.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load ONNX model: {Path}", _settings.ModelPath);
            throw;
        }
    }

    /// <summary>
    /// Generates key factors based on feature values.
    /// This provides human-readable insights into why the prediction is what it is.
    /// </summary>
    private List<FeatureImpact> GenerateKeyFactors(float[] features)
    {
        var factors = new List<FeatureImpact>();

        // Feature indices (must match FeatureEngineeringService):
        // 3=hero_level, 4=hero_kills, 5=deaths, 6=unit_kills, 7=total_gold
        // 12=gold_per_level, 13=atk_per_level, 14=def_per_level
        // 16=kd_ratio, 17=total_kills, 18=gold_efficiency

        var heroLevel = features[3];
        var heroKills = features[4];
        var deaths = features[5];
        var unitKills = features[6];
        var totalGold = features[7];
        var goldPerLevel = features[12];
        var kdRatio = features[16];
        var totalKills = features[17];
        var goldEfficiency = features[18];

        // Hero level impact
        if (heroLevel > 12)
        {
            factors.Add(new FeatureImpact
            {
                FeatureName = "HeroLevel",
                ImpactScore = 0.1f,
                Description = $"高等級英雄 (Lv.{heroLevel:F0}) 通常具有優勢"
            });
        }
        else if (heroLevel > 0 && heroLevel < 6)
        {
            factors.Add(new FeatureImpact
            {
                FeatureName = "HeroLevel",
                ImpactScore = -0.08f,
                Description = $"英雄等級較低 (Lv.{heroLevel:F0})，需要更多發育"
            });
        }

        // KDA impact
        if (kdRatio > 2.0f)
        {
            factors.Add(new FeatureImpact
            {
                FeatureName = "KdRatio",
                ImpactScore = 0.15f,
                Description = $"優秀的 KDA ({kdRatio:F2}) 顯示戰鬥優勢"
            });
        }
        else if (kdRatio > 1.0f)
        {
            factors.Add(new FeatureImpact
            {
                FeatureName = "KdRatio",
                ImpactScore = 0.08f,
                Description = $"正向 KDA ({kdRatio:F2})"
            });
        }
        else if (kdRatio < 0.5f && deaths > 2)
        {
            factors.Add(new FeatureImpact
            {
                FeatureName = "KdRatio",
                ImpactScore = -0.12f,
                Description = $"死亡過多 ({deaths:F0}) 影響發育"
            });
        }

        // Gold efficiency impact
        if (goldPerLevel > 350)
        {
            factors.Add(new FeatureImpact
            {
                FeatureName = "GoldPerLevel",
                ImpactScore = 0.07f,
                Description = $"優秀的經濟效率 (每級 {goldPerLevel:F0} 金)"
            });
        }

        // Total kills impact
        if (totalKills > 30)
        {
            factors.Add(new FeatureImpact
            {
                FeatureName = "TotalKills",
                ImpactScore = 0.1f,
                Description = $"高擊殺數 ({totalKills:F0}) 顯示進攻優勢"
            });
        }

        return factors
            .OrderByDescending(f => Math.Abs(f.ImpactScore))
            .Take(5)
            .ToList();
    }

    /// <summary>
    /// Generates a strategic recommendation based on the prediction.
    /// </summary>
    private static string GenerateRecommendation(List<FeatureImpact> factors, float probability)
    {
        var recommendations = new List<string>();

        // Overall strategy based on win probability
        if (probability > 0.75f)
            recommendations.Add("局面大優，建議穩健推進，擴大經濟差距");
        else if (probability > 0.6f)
            recommendations.Add("局面略優，建議穩定發育，尋找击杀机会");
        else if (probability > 0.4f)
            recommendations.Add("局面膠著，建議積累資源，等待時機");
        else if (probability > 0.25f)
            recommendations.Add("局面劣勢，建議保守發育，避免團戰");
        else
            recommendations.Add("局面大劣，建議團隊配合，尋找反打機會");

        // Add specific advice based on negative factors
        var lowFactor = factors
            .Where(f => f.ImpactScore < 0)
            .OrderBy(f => f.ImpactScore)
            .FirstOrDefault();

        if (lowFactor != null)
        {
            var advice = lowFactor.FeatureName switch
            {
                "HeroLevel" => "提升英雄等級以獲得屬性優勢",
                "KdRatio" => "減少不必要死亡，提升 KDA",
                "GoldPerLevel" => "专注经济发展，提高金币获取效率",
                "TotalKills" => "积极参与击杀，建立击杀优势",
                _ => $"改善 {lowFactor.FeatureName}"
            };
            recommendations.Add(advice);
        }

        return string.Join("；", recommendations);
    }

    /// <summary>
    /// Calculates confidence score based on probability distance from 0.5.
    /// </summary>
    private static float CalculateConfidence(float probability)
    {
        // Confidence is higher when probability is further from 0.5
        // Max confidence = 1.0 (probability = 0 or 1)
        // Min confidence = 0.0 (probability = 0.5)
        var distance = Math.Abs(probability - 0.5f);
        return Math.Min(1.0f, distance * 2f);
    }

    /// <summary>
    /// Disposes the ONNX inference session.
    /// </summary>
    public void Dispose()
    {
        _session?.Dispose();
        _session = null;
    }
}
