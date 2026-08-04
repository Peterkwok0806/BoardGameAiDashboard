namespace BoardGameAiDashboard.Infrastructure.Settings;

/// <summary>
/// Strongly-typed settings for ML prediction services.
/// Bound from appsettings.json section "ML".
/// </summary>
public sealed class MLSettings
{
    /// <summary>Configuration section key.</summary>
    public const string SectionName = "ML";

    /// <summary>
    /// Path to the ONNX model file.
    /// Can be absolute or relative to the application root.
    /// </summary>
    public string ModelPath { get; set; } = "./ml_trainer/models/winrate_model.onnx";

    /// <summary>
    /// Path to the feature columns JSON file.
    /// Generated during Python training alongside the ONNX model.
    /// </summary>
    public string FeatureColumnsPath { get; set; } = "./ml_trainer/models/winrate_model_features.json";

    /// <summary>
    /// Path to the training report JSON file.
    /// Contains model metrics and metadata.
    /// </summary>
    public string TrainingReportPath { get; set; } = "./ml_trainer/models/training_report.json";

    /// <summary>
    /// Whether to enable the ML prediction endpoint.
    /// Set to false if no model is deployed.
    /// </summary>
    public bool EnablePrediction { get; set; } = true;
}
