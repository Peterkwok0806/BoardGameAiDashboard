# .NET ML Inference Skill

## 目的
在 .NET 應用程式中使用 OnnxRuntime 載入並執行 ONNX 模型進行預測。

## 適用場景
- 整合 ML 預測到 API endpoint
- 即時遊戲狀態勝率預測
- 批次預測分析
- 模型熱重載

## 專案結構
```
BoardGameAiDashboard/
├── BoardGameAiDashboard.Application/
│   └── Features/ML/
│       ├── Commands/              # MediatR commands
│       │   ├── PredictWinRate/
│       │   └── BatchPredict/
│       ├── Interfaces/
│       │   └── IWinRatePredictionService.cs
│       └── Models/
│           ├── GameStatePredictionInput.cs
│           └── GameStatePredictionResult.cs
│
└── BoardGameAiDashboard.Infrastructure/
    └── Services/ML/
        ├── WinRatePredictionService.cs   # ONNX 推理實作
        ├── FeatureEngineeringService.cs  # 特徵轉換
        └── Settings/
            └── MLSettings.cs
```

## 設定檔

### appsettings.json
```json
{
  "ML": {
    "ModelPath": "./ml_trainer/models/winrate_model.onnx",
    "FeatureColumnsPath": "./ml_trainer/models/winrate_model_features.json",
    "TrainingReportPath": "./ml_trainer/models/training_report.json",
    "EnablePrediction": true
  }
}
```

| 設定項 | 說明 |
|--------|------|
| ModelPath | ONNX 模型檔路徑（相對或絕對） |
| FeatureColumnsPath | 特徵欄位 JSON 檔路徑 |
| EnablePrediction | 是否啟用預測功能 |

## 核心服務

### IWinRatePredictionService
```csharp
public interface IWinRatePredictionService
{
    bool IsModelLoaded { get; }
    string? ModelPath { get; }

    Task<GameStatePredictionResult> PredictWinRateAsync(
        GameStatePredictionInput input,
        CancellationToken ct = default);

    Task<BatchPredictionResult> BatchPredictAsync(
        IReadOnlyList<GameStatePredictionInput> inputs,
        CancellationToken ct = default);

    Task ReloadModelAsync(CancellationToken ct = default);
}
```

### 輸入模型
```csharp
public class GameStatePredictionInput
{
    public Guid? GameId { get; set; }
    public float PlayerCount { get; set; }
    public float HeroLevel { get; set; }
    public float HeroKills { get; set; }
    public float Deaths { get; set; }
    public float UnitKills { get; set; }
    public float TotalGold { get; set; }
    public float HighestAtk { get; set; }
    public float HighestDef { get; set; }
    public float HighestSpeed { get; set; }
    public float AtkRange { get; set; }
    public float HourOfDay { get; set; }
    public float DayOfWeek { get; set; }
}
```

### 輸出模型
```csharp
public class GameStatePredictionResult
{
    public float WinProbability { get; set; }       // 0.0 - 1.0
    public float ConfidenceScore { get; set; }      // 信心度
    public List<FeatureImpact> KeyFactors { get; set; }
    public string Recommendation { get; set; }
}

public class FeatureImpact
{
    public string FeatureName { get; set; }
    public float ImpactScore { get; set; }
    public string Description { get; set; }
}
```

## API Endpoints

### 1. 單筆預測
```http
POST /api/predictions/predict
Content-Type: application/json

{
  "playerCount": 5,
  "heroLevel": 15,
  "heroKills": 8,
  "deaths": 3,
  "totalGold": 6000,
  "unitKills": 45,
  "highestAtk": 150,
  "highestDef": 100,
  "highestSpeed": 380,
  "atkRange": 150,
  "hourOfDay": 20,
  "dayOfWeek": 4
}
```

### 2. 等級影響分析
```http
GET /api/predictions/analyze-level?heroLevel=15&heroKills=8&deaths=3&totalGold=6000&playerCount=5
```

### 3. 模型狀態
```http
GET /api/predictions/status
```

### 4. 熱重載模型
```http
POST /api/predictions/reload-model
Authorization: Admin
```

## ONNX 推理實作

### 關鍵程式碼 (WinRatePredictionService.cs)
```csharp
private async Task<float> PredictWithOnnxAsync(float[] features, CancellationToken ct)
{
    return await Task.Run(() =>
    {
        lock (_lock)  // 執行緒安全
        {
            // 1. 建立輸入張量 (shape: [1, 20])
            var inputTensor = new DenseTensor<float>(features, new[] { 1, features.Length });

            // 2. 建立輸入 NamedOnnxValue
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("float_input", inputTensor)
            };

            // 3. 執行推理
            using var results = _session!.Run(inputs);

            // 4. 解析輸出 (zipmap=False 時為標準 Tensor)
            // output[0] = 預測標籤 (int64)
            // output[1] = 機率陣列 (float[], shape [?, 2])
            var probOutput = results.ElementAt(1);
            var tensor = probOutput.AsTensor<float>();
            var values = tensor.ToArray();

            // 5. 回傳勝率 (class=1 的機率)
            return values[1];  // P(win)
        }
    }, ct);
}
```

## 特徵轉換 (FeatureEngineeringService)

必須與 Python 端的特徵工程完全一致：

```csharp
public float[] TransformToFeatureVector(GameStatePredictionInput input)
{
    var heroLevel = input.HeroLevel;
    var deaths = input.Deaths;

    return new float[]
    {
        // 原始特徵 (12)
        input.PlayerCount,      // 0
        input.HourOfDay,        // 1
        input.DayOfWeek,        // 2
        heroLevel,              // 3
        input.HeroKills,        // 4
        deaths,                 // 5
        input.UnitKills,        // 6
        input.TotalGold,        // 7
        input.HighestAtk,       // 8
        input.HighestDef,       // 9
        input.HighestSpeed,     // 10
        input.AtkRange,         // 11

        // 衍生特徵 (8)
        SafeDivide(input.TotalGold, heroLevel + 1),     // 12: gold_per_level
        SafeDivide(input.HighestAtk, heroLevel + 1),    // 13: atk_per_level
        SafeDivide(input.HighestDef, heroLevel + 1),    // 14: def_per_level
        SafeDivide(input.HighestSpeed, heroLevel + 1),  // 15: speed_per_level
        SafeDivide(input.HeroKills, deaths + 1),        // 16: kd_ratio
        input.HeroKills + input.UnitKills,              // 17: total_kills
        SafeDivide(input.TotalGold, input.HeroKills + 1), // 18: gold_efficiency
        SafeDivide(deaths, input.PlayerCount + 1)       // 19: death_ratio
    };
}
```

## 依賴套件

```xml
<PackageReference Include="Microsoft.ML.OnnxRuntime" Version="1.17.0" />
```

## DI 註冊

```csharp
// Infrastructure/DependencyInjection.cs
services.Configure<MLSettings>(config.GetSection(MLSettings.SectionName));
services.AddSingleton<IFeatureEngineeringService, FeatureEngineeringService>();
services.AddSingleton<IWinRatePredictionService, WinRatePredictionService>();
```

## 常見問題

### Q: 模型載入失敗？
A: 檢查：
1. ONNX 檔案是否存在於指定路徑
2. 模型格式是否正確（使用 `python -c "import onnxruntime..."` 驗證）
3. 執行目錄是否正確（相對路徑的基準）

### Q: 預測結果始終相同？
A: 可能原因：
1. 訓練資料缺乏真實訊號（隨機資料無法預測）
2. 特徵值未正確標準化
3. 模型過擬合

### Q: 如何處理批次預測效能？
A:
- 使用 `BatchPredictAsync` 而非多次呼叫 `PredictWinRateAsync`
- 內部已實作 lock，未來可優化為批次推理

### Q: 如何新增新特徵？
A:
1. Python 端：在 `feature_engineering.py` 新增
2. C# 端：在 `FeatureEngineeringService.cs` 新增對應計算
3. 更新 `appsettings.json` 中的模型路徑
4. 重新訓練模型
