# ML.NET 勝率預測系統實作計劃

## Context

用戶上傳遊戲記錄至 MatchHistory Table，後端以**遊戲狀態特徵**（Hero Level、Hero Kills、Unit Kills、Gold 等）為輸入，訓練 ML Model 預測在該狀態下的勝率。

**遊戲類型**：Guards of Atlantis II（MOBA 遊戲）
**核心問題**：在什麼遊戲狀態下更容易獲勝？

---

## 1. 資料來源

### 1.1 MatchHistory 實體

```csharp
public class MatchHistory : BaseEntity
{
    public Guid GameId { get; private set; }
    public int PlayerCount { get; private set; }
    public bool IsWinner { get; private set; }      // Label
    public DateTime PlayedAt { get; private set; }
    public Dictionary<string, string> GameFeatures { get; private set; }
}
```

### 1.2 GameFeatures JSON（每筆記錄一組狀態）

```json
{
  "player": "Player001",
  "hero": "ZhangFei",
  "hero_level": 15,
  "hero_killed": 3,
  "death": 5,
  "unit_killed": 25,
  "total_gold": 5000,
  "highest_atk": 120,
  "highest_def": 80,
  "highest_speed": 350,
  "atk_range": 150
}
```

---

## 2. Feature Engineering（狀態特徵設計）

### 2.1 原始 → 統計特徵對照表

| 原始欄位 | 轉換後特徵 | 理由 |
|----------|------------|------|
| `hero_level` | `hero_level` | 直接使用，等級越高優勢越大 |
| `hero_killed` | `hero_kills` | 直接使用，殺敵越多優勢越大 |
| `death` | `deaths` | 直接使用，死亡多劣勢越大 |
| `unit_killed` | `unit_kills` | 直接使用，補兵/小兵殺越多優勢 |
| `total_gold` | `gold`, `gold_per_level` | 直接金幣 + 等級化金幣（去除等級影響） |
| `highest_atk` | `atk_per_level` | 等級化攻擊（去除等級影響） |
| `highest_def` | `def_per_level` | 等級化防禦 |
| `highest_speed` | `speed_per_level` | 等級化速度 |
| `atk_range` | `atk_range` | 直接使用，遠程優勢 |
| `hero_killed` + `death` | `kd_ratio` | 戰績比（英雄殺/死亡+1） |
| `hero_killed` + `unit_killed` | `total_kills` | 總殺敵數 |
| `player_count` | `player_count` | 比賽人數影響 |

### 2.2 衍生特徵（增強預測能力）

| 衍生特徵 | 計算公式 | 說明 |
|----------|----------|------|
| `gold_per_level` | `total_gold / (hero_level + 1)` | 等級化經濟領先 |
| `atk_per_level` | `highest_atk / (hero_level + 1)` | 等級化攻擊力落後 |
| `def_per_level` | `highest_def / (hero_level + 1)` | 等級化防禦力 |
| `speed_per_level` | `highest_speed / (hero_level + 1)` | 等級化速度 |
| `kd_ratio` | `hero_kills / (deaths + 1)` | 戰績比 |
| `total_kills` | `hero_kills + unit_kills` | 總殺敵 |
| `gold_efficiency` | `total_gold / (hero_kills + 1)` | 每殺一人金幣效率 |
| `death_ratio` | `deaths / player_count` | 平均分擔死亡 |

---

## 3. ML 訓練資料結構

### 3.1 訓練資料類別

```csharp
// Application/Features/ML/Models/GameStateTrainingData.cs
public class GameStateTrainingData
{
    // 比賽環境
    [LoadColumn(0)]  public float PlayerCount { get; set; }
    [LoadColumn(1)]  public float HourOfDay { get; set; }
    [LoadColumn(2)]  public float DayOfWeek { get; set; }

    // 核心戰績
    [LoadColumn(3)]  public float HeroLevel { get; set; }
    [LoadColumn(4)]  public float HeroKills { get; set; }
    [LoadColumn(5)]  public float Deaths { get; set; }
    [LoadColumn(6)]  public float UnitKills { get; set; }

    // 經濟數值
    [LoadColumn(7)]  public float TotalGold { get; set; }
    [LoadColumn(8)]  public float GoldPerLevel { get; set; }

    // 屬性數值（等級化）
    [LoadColumn(9)]  public float AtkPerLevel { get; set; }
    [LoadColumn(10)] public float DefPerLevel { get; set; }
    [LoadColumn(11)] public float SpeedPerLevel { get; set; }
    [LoadColumn(12)] public float AtkRange { get; set; }

    // 衍生特徵
    [LoadColumn(13)] public float KdRatio { get; set; }
    [LoadColumn(14)] public float TotalKills { get; set; }
    [LoadColumn(15)] public float GoldEfficiency { get; set; }

    // Label
    [LoadColumn(16)] public bool IsWinner { get; set; }
}
```

### 3.2 預測輸入

```csharp
// Application/Features/ML/Models/GameStatePredictionInput.cs
public class GameStatePredictionInput
{
    public Guid GameId { get; set; }

    // 比賽環境
    public int PlayerCount { get; set; }
    public int HourOfDay { get; set; }
    public int DayOfWeek { get; set; }

    // 核心戰績
    public int HeroLevel { get; set; }
    public int HeroKills { get; set; }
    public int Deaths { get; set; }
    public int UnitKills { get; set; }

    // 經濟數值
    public int TotalGold { get; set; }

    // 屬性數值
    public int HighestAtk { get; set; }
    public int HighestDef { get; set; }
    public int HighestSpeed { get; set; }
    public int AtkRange { get; set; }
}
```

### 3.3 預測結果

```csharp
// Application/Features/ML/Models/GameStatePredictionResult.cs
public class GameStatePredictionResult
{
    /// <summary>勝利機率 (0-1)</summary>
    public float WinProbability { get; set; }

    /// <summary>預測信心度 (0-1)</summary>
    public float ConfidenceScore { get; set; }

    /// <summary>影響勝率的關鍵因素</summary>
    public List<FeatureImpact> KeyFactors { get; set; } = new();

    /// <summary>建議策略</summary>
    public string Recommendation { get; set; } = string.Empty;
}

public class FeatureImpact
{
    public string FeatureName { get; set; } = string.Empty;
    public float ImpactScore { get; set; }  // 正值=有利, 負值=不利
    public string Description { get; set; } = string.Empty;
}
```

---

## 4. 交叉驗證結果結構

```csharp
// Application/Features/ML/Models/ModelComparisonResult.cs
public class CrossValidationResult
{
    public string ModelName { get; set; } = string.Empty;
    public double Accuracy { get; set; }
    public double Precision { get; set; }
    public double Recall { get; set; }
    public double F1Score { get; set; }
    public double LogLoss { get; set; }
    public double AreaUnderRocCurve { get; set; }
    public CrossValidationMetrics PerFoldMetrics { get; set; } = new();
}

public class CrossValidationMetrics
{
    public List<double> AccuracyPerFold { get; set; } = new();
    public List<double> LogLossPerFold { get; set; } = new();
    public double MeanAccuracy { get; set; }
    public double StdDevAccuracy { get; set; }
}

public class ModelComparisonResult
{
    public CrossValidationResult FastForestResult { get; set; } = new();
    public CrossValidationResult FastTreeResult { get; set; } = new();
    public string BestModel { get; set; } = string.Empty;
    public double ImprovementPercentage { get; set; }
}
```

---

## 5. 專案結構

```
BoardGameAiDashboard/
├── BoardGameAiDashboard.Application/
│   └── Features/
│       └── ML/
│           ├── Models/
│           │   ├── GameStateTrainingData.cs
│           │   ├── GameStatePredictionInput.cs
│           │   ├── GameStatePredictionResult.cs
│           │   └── ModelComparisonResult.cs
│           ├── Interfaces/
│           │   ├── IWinRatePredictionService.cs
│           │   └── IGameStateFeatureEngineering.cs
│           └── Commands/
│               ├── TrainWinRateModel/
│               │   ├── TrainWinRateModelCommand.cs
│               │   └── TrainWinRateModelHandler.cs
│               └── PredictWinRate/
│                   ├── PredictWinRateQuery.cs
│                   └── PredictWinRateHandler.cs
│
└── BoardGameAiDashboard.Infrastructure/
    └── Services/
        └── ML/
            ├── WinRatePredictionService.cs
            ├── GameStateFeatureEngineering.cs
            └── MLModels/           # 模型儲存目錄
```

---

## 6. Feature Engineering 服務

```csharp
// Application/Features/ML/Interfaces/IGameStateFeatureEngineering.cs
public interface IGameStateFeatureEngineering
{
    /// <summary>
    /// 將 MatchHistory 轉換為 ML 訓練資料
    /// </summary>
    GameStateTrainingData TransformToTrainingData(MatchHistory match);

    /// <summary>
    /// 批次轉換
    /// </summary>
    IEnumerable<GameStateTrainingData> BatchTransform(IEnumerable<MatchHistory> matches);

    /// <summary>
    /// 將原始輸入轉換為 ML 格式
    /// </summary>
    GameStateTrainingData TransformInput(GameStatePredictionInput input);
}

// Infrastructure/Services/ML/GameStateFeatureEngineering.cs
public class GameStateFeatureEngineering : IGameStateFeatureEngineering
{
    public GameStateTrainingData TransformToTrainingData(MatchHistory match)
    {
        var features = ParseGameFeatures(match.GameFeatures);

        var heroLevel = (float)features.HeroLevel;
        var heroKills = (float)features.HeroKills;
        var deaths = (float)features.Deaths;
        var totalGold = (float)features.TotalGold;

        return new GameStateTrainingData
        {
            // 比賽環境
            PlayerCount = match.PlayerCount,
            HourOfDay = match.PlayedAt.Hour,
            DayOfWeek = (int)match.PlayedAt.DayOfWeek,

            // 核心戰績
            HeroLevel = heroLevel,
            HeroKills = heroKills,
            Deaths = deaths,
            UnitKills = (float)features.UnitKills,

            // 經濟
            TotalGold = totalGold,
            GoldPerLevel = SafeDivide(totalGold, heroLevel + 1),

            // 屬性（等級化）
            AtkPerLevel = SafeDivide((float)features.HighestAtk, heroLevel + 1),
            DefPerLevel = SafeDivide((float)features.HighestDef, heroLevel + 1),
            SpeedPerLevel = SafeDivide((float)features.HighestSpeed, heroLevel + 1),
            AtkRange = (float)features.AtkRange,

            // 衍生特徵
            KdRatio = SafeDivide(heroKills, deaths + 1),
            TotalKills = heroKills + (float)features.UnitKills,
            GoldEfficiency = SafeDivide(totalGold, heroKills + 1),

            // Label
            IsWinner = match.IsWinner
        };
    }

    public IEnumerable<GameStateTrainingData> BatchTransform(
        IEnumerable<MatchHistory> matches)
    {
        return matches.Select(TransformToTrainingData);
    }

    public GameStateTrainingData TransformInput(GameStatePredictionInput input)
    {
        var heroLevel = (float)input.HeroLevel;
        var heroKills = (float)input.HeroKills;
        var deaths = (float)input.Deaths;
        var totalGold = (float)input.TotalGold;

        return new GameStateTrainingData
        {
            PlayerCount = input.PlayerCount,
            HourOfDay = input.HourOfDay,
            DayOfWeek = input.DayOfWeek,
            HeroLevel = heroLevel,
            HeroKills = heroKills,
            Deaths = deaths,
            UnitKills = (float)input.UnitKills,
            TotalGold = totalGold,
            GoldPerLevel = SafeDivide(totalGold, heroLevel + 1),
            AtkPerLevel = SafeDivide((float)input.HighestAtk, heroLevel + 1),
            DefPerLevel = SafeDivide((float)input.HighestDef, heroLevel + 1),
            SpeedPerLevel = SafeDivide((float)input.HighestSpeed, heroLevel + 1),
            AtkRange = (float)input.AtkRange,
            KdRatio = SafeDivide(heroKills, deaths + 1),
            TotalKills = heroKills + (float)input.UnitKills,
            GoldEfficiency = SafeDivide(totalGold, heroKills + 1)
        };
    }

    private ParsedFeatures ParseGameFeatures(Dictionary<string, string> features)
    {
        return new ParsedFeatures
        {
            HeroLevel = GetInt(features, "hero_level"),
            HeroKills = GetInt(features, "hero_killed"),
            Deaths = GetInt(features, "death"),
            UnitKills = GetInt(features, "unit_killed"),
            TotalGold = GetInt(features, "total_gold"),
            HighestAtk = GetInt(features, "highest_atk"),
            HighestDef = GetInt(features, "highest_def"),
            HighestSpeed = GetInt(features, "highest_speed"),
            AtkRange = GetInt(features, "atk_range")
        };
    }

    private float SafeDivide(float numerator, float denominator)
        => denominator > 0 ? numerator / denominator : 0;

    private int GetInt(Dictionary<string, string> dict, string key)
        => int.TryParse(dict.GetValueOrDefault(key, "0"), out var v) ? v : 0;

    private record ParsedFeatures
    {
        public int HeroLevel { get; init; }
        public int HeroKills { get; init; }
        public int Deaths { get; init; }
        public int UnitKills { get; init; }
        public int TotalGold { get; init; }
        public int HighestAtk { get; init; }
        public int HighestDef { get; init; }
        public int HighestSpeed { get; init; }
        public int AtkRange { get; init; }
    }
}
```

---

## 7. ML 訓練服務（FastForest vs FastTree）

```csharp
// Infrastructure/Services/ML/WinRatePredictionService.cs
public interface IWinRatePredictionService
{
    /// <summary>訓練並比較 FastForest 和 FastTree</summary>
    Task<ModelComparisonResult> TrainAndCompareModelsAsync(
        Guid gameId,
        IEnumerable<GameStateTrainingData> trainingData,
        CancellationToken ct = default);

    /// <summary>預測勝率</summary>
    Task<GameStatePredictionResult> PredictWinRateAsync(
        GameStatePredictionInput input,
        CancellationToken ct = default);

    /// <summary>取得模型比較結果</summary>
    Task<ModelComparisonResult?> GetModelComparisonAsync(
        Guid gameId,
        CancellationToken ct = default);

    /// <summary>取得特徵重要性</summary>
    Task<Dictionary<string, float>> GetFeatureImportanceAsync(
        Guid gameId,
        CancellationToken ct = default);
}

public class WinRatePredictionService : IWinRatePredictionService
{
    private readonly MLContext _mlContext;
    private readonly string _modelDirectory;
    private readonly IGameStateFeatureEngineering _featureEngineering;

    private readonly Dictionary<Guid, (ITransformer Model, string Name)> _models = new();
    private readonly Dictionary<Guid, ModelComparisonResult> _comparisons = new();
    private readonly Dictionary<Guid, float[]> _featureImportances = new();

    public WinRatePredictionService(IGameStateFeatureEngineering featureEngineering)
    {
        _mlContext = new MLContext(seed: 42);
        _featureEngineering = featureEngineering;
        _modelDirectory = Path.Combine(AppContext.BaseDirectory, "MLModels");
        Directory.CreateDirectory(_modelDirectory);
    }

    public async Task<ModelComparisonResult> TrainAndCompareModelsAsync(
        Guid gameId,
        IEnumerable<GameStateTrainingData> trainingData,
        CancellationToken ct = default)
    {
        var dataList = trainingData.ToList();
        if (dataList.Count < 20)
        {
            throw new ValidationException($"訓練資料不足，至少需要 20 筆，目前有 {dataList.Count} 筆。");
        }

        var dataView = _mlContext.Data.LoadFromEnumerable(dataList);

        // 定義特徵管線
        var featureColumns = new[]
        {
            nameof(GameStateTrainingData.PlayerCount),
            nameof(GameStateTrainingData.HourOfDay),
            nameof(GameStateTrainingData.DayOfWeek),
            nameof(GameStateTrainingData.HeroLevel),
            nameof(GameStateTrainingData.HeroKills),
            nameof(GameStateTrainingData.Deaths),
            nameof(GameStateTrainingData.UnitKills),
            nameof(GameStateTrainingData.TotalGold),
            nameof(GameStateTrainingData.GoldPerLevel),
            nameof(GameStateTrainingData.AtkPerLevel),
            nameof(GameStateTrainingData.DefPerLevel),
            nameof(GameStateTrainingData.SpeedPerLevel),
            nameof(GameStateTrainingData.AtkRange),
            nameof(GameStateTrainingData.KdRatio),
            nameof(GameStateTrainingData.TotalKills),
            nameof(GameStateTrainingData.GoldEfficiency)
        };

        var featurePipeline = _mlContext.Transforms.Concatenate("Features", featureColumns);

        // FastForest 訓練器
        var fastForestTrainer = _mlContext.BinaryClassification.Trainers.FastForest(
            labelColumnName: nameof(GameStateTrainingData.IsWinner),
            featureColumnName: "Features",
            numLeaves: 31,
            numTrees: 100,
            minDataSamplesInLeaf: 5);

        // FastTree 訓練器
        var fastTreeTrainer = _mlContext.BinaryClassification.Trainers.FastTree(
            labelColumnName: nameof(GameStateTrainingData.IsWinner),
            featureColumnName: "Features",
            numLeaves: 31,
            numIterations: 100,
            minDataInLeaf: 5,
            learningRate: 0.1);

        // 執行 5-Fold 交叉驗證
        const int numberOfFolds = 5;

        var fastForestResult = await RunCrossValidationAsync(
            dataView, featurePipeline, fastForestTrainer, numberOfFolds, "FastForest", ct);

        var fastTreeResult = await RunCrossValidationAsync(
            dataView, featurePipeline, fastTreeTrainer, numberOfFolds, "FastTree", ct);

        // 比較結果
        var comparison = GenerateComparison(fastForestResult, fastTreeResult);

        // 保存最佳模型
        var bestTrainer = comparison.BestModel == "FastForest" ? fastForestTrainer : fastTreeTrainer;
        await SaveModelAsync(gameId, dataView, featurePipeline.Append(bestTrainer), ct);

        // 提取特徵重要性
        await ExtractFeatureImportanceAsync(gameId, dataView, featurePipeline, ct);

        _comparisons[gameId] = comparison;
        return comparison;
    }

    private Task<CrossValidationResult> RunCrossValidationAsync(
        IDataView dataView,
        IEstimator<ITransformer> featurePipeline,
        ITrainer trainer,
        int numberOfFolds,
        string modelName,
        CancellationToken ct)
    {
        return Task.Run(() =>
        {
            var trainingPipeline = featurePipeline.Append(trainer);

            var cvResults = _mlContext.BinaryClassification.CrossValidate(
                dataView,
                trainingPipeline,
                numberOfFolds: numberOfFolds,
                labelColumnName: nameof(GameStateTrainingData.IsWinner));

            var metrics = cvResults.Select(r => r.Metrics).ToList();

            return new CrossValidationResult
            {
                ModelName = modelName,
                Accuracy = metrics.Average(m => m.Accuracy),
                Precision = metrics.Average(m => m.PositivePrecision),
                Recall = metrics.Average(m => m.PositiveRecall),
                F1Score = metrics.Average(m => m.F1Score),
                LogLoss = metrics.Average(m => m.LogLoss),
                AreaUnderRocCurve = metrics.Average(m => m.AreaUnderRocCurve),
                PerFoldMetrics = new CrossValidationMetrics
                {
                    AccuracyPerFold = metrics.Select(m => m.Accuracy).ToList(),
                    LogLossPerFold = metrics.Select(m => m.LogLoss).ToList(),
                    MeanAccuracy = metrics.Average(m => m.Accuracy),
                    StdDevAccuracy = CalculateStdDev(metrics.Select(m => m.Accuracy).ToList())
                }
            };
        }, ct);
    }

    public async Task<GameStatePredictionResult> PredictWinRateAsync(
        GameStatePredictionInput input,
        CancellationToken ct = default)
    {
        if (!_models.TryGetValue(input.GameId, out var modelInfo))
        {
            await LoadModelAsync(input.GameId, ct);
            modelInfo = _models[input.GameId];
        }

        // 轉換輸入為訓練資料格式
        var trainingData = _featureEngineering.TransformInput(input);

        var predictionEngine = _mlContext.Model.CreatePredictionEngine
            <GameStateTrainingData, BinaryPrediction>(modelInfo.Model);

        var prediction = predictionEngine.Predict(trainingData);

        // 取得特徵重要性並生成關鍵因素
        var keyFactors = await GenerateKeyFactorsAsync(input.GameId, trainingData, ct);

        return new GameStatePredictionResult
        {
            WinProbability = prediction.Probability,
            ConfidenceScore = prediction.Probability > 0.5f
                ? prediction.Probability
                : 1 - prediction.Probability,
            KeyFactors = keyFactors,
            Recommendation = GenerateRecommendation(keyFactors, prediction.Probability)
        };
    }

    private async Task<List<FeatureImpact>> GenerateKeyFactorsAsync(
        Guid gameId,
        GameStateTrainingData data,
        CancellationToken ct)
    {
        var factors = new List<FeatureImpact>();

        if (_featureImportances.TryGetValue(gameId, out var importance))
        {
            var featureNames = new[]
            {
                "HeroLevel", "HeroKills", "Deaths", "UnitKills",
                "TotalGold", "GoldPerLevel", "AtkPerLevel", "DefPerLevel",
                "SpeedPerLevel", "AtkRange", "KdRatio", "TotalKills"
            };

            for (int i = 0; i < Math.Min(featureNames.Length, importance.Length); i++)
            {
                factors.Add(new FeatureImpact
                {
                    FeatureName = featureNames[i],
                    ImpactScore = importance[i],
                    Description = GetFeatureDescription(featureNames[i], GetFeatureValue(data, featureNames[i]))
                });
            }
        }

        return factors.OrderByDescending(f => Math.Abs(f.ImpactScore)).Take(5).ToList();
    }

    private float GetFeatureValue(GameStateTrainingData data, string name)
    {
        return name switch
        {
            "HeroLevel" => data.HeroLevel,
            "HeroKills" => data.HeroKills,
            "Deaths" => data.Deaths,
            "UnitKills" => data.UnitKills,
            "TotalGold" => data.TotalGold,
            "GoldPerLevel" => data.GoldPerLevel,
            "AtkPerLevel" => data.AtkPerLevel,
            "DefPerLevel" => data.DefPerLevel,
            "SpeedPerLevel" => data.SpeedPerLevel,
            "AtkRange" => data.AtkRange,
            "KdRatio" => data.KdRatio,
            "TotalKills" => data.TotalKills,
            _ => 0
        };
    }

    private string GetFeatureDescription(string name, float value)
    {
        return name switch
        {
            "HeroLevel" => $"等級 {value:F0}，{"高於平均".Replace("", value > 10 ? "" : "低於平均")}",
            "HeroKills" => $"英雄殺 {value:F0}",
            "KdRatio" => $"KDA {value:F2}",
            "GoldPerLevel" => $"等均金幣 {value:F0}",
            _ => $"{name} = {value:F2}"
        };
    }

    private string GenerateRecommendation(List<FeatureImpact> factors, float probability)
    {
        var recommendations = new List<string>();

        if (probability > 0.7f)
            recommendations.Add("局面大優，建議穩健推進");
        else if (probability > 0.5f)
            recommendations.Add("局面略優，建議擴大優勢");
        else if (probability > 0.3f)
            recommendations.Add("局面劣勢，建議保守發育");
        else
            recommendations.Add("局面大劣，建議團隊配合尋找機會");

        var lowFactor = factors.Where(f => f.ImpactScore < 0).OrderBy(f => f.ImpactScore).FirstOrDefault();
        if (lowFactor != null)
        {
            recommendations.Add($"注意提升 {lowFactor.FeatureName}");
        }

        return string.Join("；", recommendations);
    }

    private ModelComparisonResult GenerateComparison(
        CrossValidationResult fastForest,
        CrossValidationResult fastTree)
    {
        var best = fastForest.Accuracy >= fastTree.Accuracy
            ? ("FastForest", fastForest, fastTree)
            : ("FastTree", fastTree, fastForest);

        return new ModelComparisonResult
        {
            FastForestResult = fastForest,
            FastTreeResult = fastTree,
            BestModel = best.Item1,
            ImprovementPercentage = best.Item2.Accuracy > 0
                ? ((best.Item2.Accuracy - best.Item3.Accuracy) / best.Item3.Accuracy) * 100
                : 0
        };
    }

    private static double CalculateStdDev(IEnumerable<double> values)
    {
        var list = values.ToList();
        var avg = list.Average();
        var sumOfSquares = list.Sum(v => Math.Pow(v - avg, 2));
        return Math.Sqrt(sumOfSquares / list.Count);
    }

    // 模型持久化方法
    private async Task SaveModelAsync(Guid gameId, IDataView dataView,
        IEstimator<ITransformer> pipeline, CancellationToken ct)
    {
        var model = pipeline.Fit(dataView);
        var path = Path.Combine(_modelDirectory, $"{gameId}_model.zip");

        _mlContext.Model.Save(model, dataView.Schema, path);
        _models[gameId] = (model, "Best");

        await Task.CompletedTask;
    }

    private async Task LoadModelAsync(Guid gameId, CancellationToken ct)
    {
        var path = Path.Combine(_modelDirectory, $"{gameId}_model.zip");
        if (!File.Exists(path))
        {
            throw new NotFoundException($"遊戲 {gameId} 的模型尚未訓練。");
        }

        var model = _mlContext.Model.Load(path, out var schema);
        _models[gameId] = (model, "Loaded");
        await Task.CompletedTask;
    }

    private async Task ExtractFeatureImportanceAsync(Guid gameId, IDataView dataView,
        IEstimator<ITransformer> pipeline, CancellationToken ct)
    {
        await Task.Run(() =>
        {
            var model = pipeline.Append(_mlContext.BinaryClassification.Trainers.FastTree(
                labelColumnName: nameof(GameStateTrainingData.IsWinner),
                featureColumnName: "Features"))
                .Fit(dataView);

            var treeParams = model.GetNodeFeatureContributions();
            _featureImportances[gameId] = treeParams;
        }, ct);
    }
}

// ML.NET 預測輸出
internal class BinaryPrediction
{
    public bool PredictedLabel { get; set; }
    public float Probability { get; set; }
    public float Score { get; set; }
}
```

---

## 8. CQRS Handlers

### 8.1 TrainWinRateModelHandler

```csharp
// Application/Features/ML/Commands/TrainWinRateModel/TrainWinRateModelHandler.cs
public sealed class TrainWinRateModelHandler
    : IRequestHandler<TrainWinRateModelCommand, ModelComparisonResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGameStateFeatureEngineering _featureEngineering;
    private readonly IWinRatePredictionService _predictionService;
    private readonly ILogger<TrainWinRateModelHandler> _logger;

    public TrainWinRateModelHandler(
        IUnitOfWork unitOfWork,
        IGameStateFeatureEngineering featureEngineering,
        IWinRatePredictionService predictionService,
        ILogger<TrainWinRateModelHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _featureEngineering = featureEngineering;
        _predictionService = predictionService;
        _logger = logger;
    }

    public async Task<ModelComparisonResult> Handle(
        TrainWinRateModelCommand request,
        CancellationToken ct)
    {
        _logger.LogInformation("開始訓練遊戲 {GameId} 的 ML 模型", request.GameId);

        // 取得該遊戲的所有 MatchHistory
        var matches = await _unitOfWork.MatchHistories
            .FindAsync(m => m.GameId == request.GameId && !m.IsDeleted,
                orderBy: q => q.OrderByDescending(m => m.PlayedAt),
                ct: ct);

        var matchList = matches.ToList();
        if (matchList.Count < 20)
        {
            throw new ValidationException(
                $"訓練資料不足。至少需要 20 筆，目前只有 {matchList.Count} 筆。");
        }

        // Feature Engineering 轉換
        var trainingData = _featureEngineering.BatchTransform(matchList).ToList();

        _logger.LogInformation("已轉換 {Count} 筆訓練資料", trainingData.Count);

        // 訓練並比較模型
        var result = await _predictionService.TrainAndCompareModelsAsync(
            request.GameId, trainingData, ct);

        _logger.LogInformation(
            "訓練完成。最佳模型: {BestModel}, 準確率: {Accuracy:F2}%",
            result.BestModel, result.FastForestResult.Accuracy * 100);

        return result;
    }
}

public sealed record TrainWinRateModelCommand : IRequest<ModelComparisonResult>
{
    public Guid GameId { get; init; }
}
```

### 8.2 PredictWinRateHandler

```csharp
// Application/Features/ML/Commands/PredictWinRate/PredictWinRateHandler.cs
public sealed class PredictWinRateHandler
    : IRequestHandler<PredictWinRateQuery, GameStatePredictionResult>
{
    private readonly IWinRatePredictionService _predictionService;

    public PredictWinRateHandler(IWinRatePredictionService predictionService)
    {
        _predictionService = predictionService;
    }

    public async Task<GameStatePredictionResult> Handle(
        PredictWinRateQuery request,
        CancellationToken ct)
    {
        return await _predictionService.PredictWinRateAsync(request.Input, ct);
    }
}

public sealed record PredictWinRateQuery : IRequest<GameStatePredictionResult>
{
    public GameStatePredictionInput Input { get; init; } = new();
}
```

---

## 9. API 端點

```csharp
// Api/Controllers/PredictionsController.cs
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class PredictionsController : ControllerBase
{
    private readonly ISender _sender;

    public PredictionsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// 訓練模型並比較 FastForest vs FastTree
    /// </summary>
    [HttpPost("train/{gameId:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ModelComparisonResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> TrainModel(
        Guid gameId,
        CancellationToken ct)
    {
        var command = new TrainWinRateModelCommand { GameId = gameId };
        var result = await _sender.Send(command, ct);
        return Ok(result);
    }

    /// <summary>
    /// 預測遊戲狀態的勝率
    /// </summary>
    [HttpPost("predict")]
    [ProducesResponseType(typeof(GameStatePredictionResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> PredictWinRate(
        [FromBody] GameStatePredictionInput input,
        CancellationToken ct)
    {
        var query = new PredictWinRateQuery { Input = input };
        var result = await _sender.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// 示範：分析特定英雄Level/殺敵數對勝率的影響
    /// </summary>
    [HttpGet("analyze/{gameId:guid}")]
    [ProducesResponseType(typeof(Dictionary<string, float[]>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AnalyzeWinRateByLevel(
        Guid gameId,
        [FromQuery] int heroLevel,
        [FromQuery] int heroKills,
        [FromQuery] int deaths,
        [FromQuery] int totalGold,
        CancellationToken ct)
    {
        // 模擬不同狀態下的勝率預測
        var inputs = new List<GameStatePredictionInput>();

        // 固定其他參數，變化 HeroLevel
        for (int level = heroLevel - 5; level <= heroLevel + 5; level++)
        {
            inputs.Add(new GameStatePredictionInput
            {
                GameId = gameId,
                PlayerCount = 5,
                HeroLevel = Math.Max(1, level),
                HeroKills = heroKills,
                Deaths = deaths,
                TotalGold = totalGold + (level - heroLevel) * 100,
                HighestAtk = 50 + level * 5,
                HighestDef = 30 + level * 3,
                HighestSpeed = 300 + level * 2,
                AtkRange = 150
            });
        }

        var predictions = new Dictionary<string, List<float>>();
        predictions["heroLevels"] = new();
        predictions["winProbabilities"] = new();

        foreach (var input in inputs)
        {
            var result = await _sender.Send(new PredictWinRateQuery { Input = input }, ct);
            predictions["heroLevels"].Add(input.HeroLevel);
            predictions["winProbabilities"].Add(result.WinProbability);
        }

        return Ok(predictions);
    }

    /// <summary>
    /// 取得模型比較結果
    /// </summary>
    [HttpGet("comparison/{gameId:guid}")]
    [ProducesResponseType(typeof(ModelComparisonResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetModelComparison(
        Guid gameId,
        CancellationToken ct)
    {
        var query = new GetModelComparisonQuery { GameId = gameId };
        var result = await _sender.Send(query, ct);
        return Ok(result);
    }
}

public sealed record GetModelComparisonQuery : IRequest<ModelComparisonResult>
{
    public Guid GameId { get; init; }
}

public sealed class GetModelComparisonHandler
    : IRequestHandler<GetModelComparisonQuery, ModelComparisonResult>
{
    private readonly IWinRatePredictionService _predictionService;

    public GetModelComparisonHandler(IWinRatePredictionService predictionService)
    {
        _predictionService = predictionService;
    }

    public async Task<ModelComparisonResult> Handle(
        GetModelComparisonQuery request,
        CancellationToken ct)
    {
        var result = await _predictionService.GetModelComparisonAsync(request.GameId, ct);
        return result ?? throw new NotFoundException("模型尚未訓練");
    }
}
```

---

## 10. DI 註冊

```csharp
// Infrastructure/DependencyInjection.cs
public static IServiceCollection AddInfrastructure(...)
{
    // ... 現有程式碼 ...

    // ── ML Services ─────────────────────────────────────────────
    services.AddScoped<IGameStateFeatureEngineering, GameStateFeatureEngineering>();
    services.AddSingleton<IWinRatePredictionService, WinRatePredictionService>();

    return services;
}
```

---

## 11. NuGet 套件

```xml
<!-- BoardGameAiDashboard.Infrastructure/BoardGameAiDashboard.Infrastructure.csproj -->
<PackageReference Include="Microsoft.ML" Version="3.0.1" />
```

---

## 12. 實作步驟

| 步驟 | 任務 | 檔案 |
|------|------|------|
| 1 | 加入 NuGet 套件 | `Infrastructure.csproj` |
| 2 | 建立 ML 模型類別 | `GameStateTrainingData.cs`, `GameStatePredictionInput.cs`, `GameStatePredictionResult.cs`, `ModelComparisonResult.cs` |
| 3 | 建立服務介面 | `IWinRatePredictionService.cs`, `IGameStateFeatureEngineering.cs` |
| 4 | 實作 Feature Engineering | `GameStateFeatureEngineering.cs` |
| 5 | 實作 ML 訓練服務 | `WinRatePredictionService.cs` |
| 6 | 實作 CQRS Handlers | `TrainWinRateModelHandler.cs`, `PredictWinRateHandler.cs`, `GetModelComparisonHandler.cs` |
| 7 | 擴展 Controller | `PredictionsController.cs` |
| 8 | 更新 DI 註冊 | `DependencyInjection.cs` |

---

## 13. 前端 Angular 整合建議

### 13.1 預測輸入表單

```typescript
// DashboardFrontend/src/app/features/prediction/prediction-form.component.ts
export class PredictionFormComponent {
  form = new FormGroup({
    heroLevel: new FormControl(10, [Validators.required, Validators.min(1), Validators.max(25)]),
    heroKills: new FormControl(3, [Validators.required, Validators.min(0)]),
    deaths: new FormControl(2, [Validators.required, Validators.min(0)]),
    unitKills: new FormControl(20, [Validators.required, Validators.min(0)]),
    totalGold: new FormControl(3000, [Validators.required, Validators.min(0)]),
    highestAtk: new FormControl(80, [Validators.required]),
    highestDef: new FormControl(50, [Validators.required]),
    highestSpeed: new FormControl(320, [Validators.required]),
    atkRange: new FormControl(150, [Validators.required]),
    playerCount: new FormControl(5, [Validators.required, Validators.min(2), Validators.max(10)])
  });

  prediction$!: Observable<GameStatePredictionResult>;

  onPredict(): void {
    this.prediction$ = this.predictionService.predict(this.form.value as GameStatePredictionInput);
  }
}
```

### 13.2 勝率分析圖表

```typescript
// 顯示 HeroLevel 對勝率的影響
onAnalyzeLevelImpact(): void {
  this.analysis$ = this.predictionService.analyzeWinRateByLevel(
    this.gameId,
    this.form.get('heroLevel')!.value!,
    this.form.get('heroKills')!.value!,
    this.form.get('deaths')!.value!,
    this.form.get('totalGold')!.value!
  );
}
```

---

## 14. 驗證方式

```bash
# 1. 建置專案
cd BoardGameAiDashboard
dotnet build

# 2. 執行單元測試
dotnet test --filter "FullyQualifiedName~ML"

# 3. 測試 API
# 訓練模型
POST http://localhost:5001/api/predictions/train/{gameId}

# 預測勝率
POST http://localhost:5001/api/predictions/predict
{
  "gameId": "...",
  "playerCount": 5,
  "heroLevel": 15,
  "heroKills": 5,
  "deaths": 3,
  "unitKills": 30,
  "totalGold": 5000,
  "highestAtk": 120,
  "highestDef": 80,
  "highestSpeed": 350,
  "atkRange": 150
}

# 分析 Level 影響
GET http://localhost:5001/api/predictions/analyze/{gameId}?heroLevel=15&heroKills=5&deaths=3&totalGold=5000
```

---

## 15. 預期輸出範例

### 模型比較結果

```json
{
  "fastForestResult": {
    "modelName": "FastForest",
    "accuracy": 0.85,
    "precision": 0.82,
    "recall": 0.88,
    "f1Score": 0.85,
    "logLoss": 0.35,
    "areaUnderRocCurve": 0.91,
    "perFoldMetrics": {
      "accuracyPerFold": [0.82, 0.88, 0.84, 0.86, 0.85],
      "meanAccuracy": 0.85,
      "stdDevAccuracy": 0.022
    }
  },
  "fastTreeResult": {
    "modelName": "FastTree",
    "accuracy": 0.83,
    "precision": 0.80,
    "recall": 0.86,
    "f1Score": 0.83,
    "logLoss": 0.38,
    "areaUnderRocCurve": 0.89
  },
  "bestModel": "FastForest",
  "improvementPercentage": 2.4
}
```

### 預測結果

```json
{
  "winProbability": 0.72,
  "confidenceScore": 0.72,
  "keyFactors": [
    { "featureName": "HeroKills", "impactScore": 0.15, "description": "英雄殺 5" },
    { "featureName": "KdRatio", "impactScore": 0.12, "description": "KDA 1.25" },
    { "featureName": "GoldPerLevel", "impactScore": 0.08, "description": "等均金幣 312" }
  ],
  "recommendation": "局面略優，建議擴大優勢；注意提升經濟效率"
}
```
