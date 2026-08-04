# 勝率預測系統實作計劃（Python + Random Forest）

## Context

用戶上傳遊戲記錄至 MatchHistory Table，後端以**遊戲狀態特徵**（Hero Level、Hero Kills、Unit Kills、Gold 等）為輸入，訓練 ML Model 預測在該狀態下的勝率。

**遊戲類型**：Guards of Atlantis II（MOBA 遊戲）
**核心問題**：在什麼遊戲狀態下更容易獲勝？

**架構原則**：
- **離線訓練，線上預測** — 訓練模型和執行模型完全分開
- **職責分離** — .NET 只輸出 CSV，Python 負責特徵工程和訓練

---

## 架構總覽

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                              離線訓練流程（Python）                              │
│                                                                                 │
│  ┌─────────────────┐     CSV (原始資料)  ┌─────────────────────────────────────┐ │
│  │ .NET CSV 匯出    │ ─────────────────▶ │ Python (Random Forest)              │ │
│  │ GET /export-csv │                    │                                      │ │
│  └─────────────────┘                    │  1. 讀取 CSV                         │ │
│                                           │  2. Feature Engineering (Python)     │ │
│                                           │  3. Train Random Forest               │ │
│                                           │  4. Export ONNX                      │ │
│                                           └──────────────┬──────────────────────┘ │
│                                                          │                      │
│                                              model.onnx + feature_columns.json    │
└──────────────────────────────────────────────────────────┼──────────────────────┘
                                                           │
                              部署 ONNX 模型到共享位置       │
                              (本地資料夾 / Azure Blob / S3) │
                                                           ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│                              線上預測流程（.NET API）                            │
│                                                                                 │
│  ┌──────────────┐    ┌───────────────────┐    ┌─────────────────────────────┐ │
│  │ Prediction   │───▶│    OnnxRuntime     │───▶│ Win Probability              │ │
│  │   Request    │    │    InferenceSession│    │    Response                  │ │
│  └──────────────┘    └───────────────────┘    └─────────────────────────────┘ │
│                                                                                 │
│   model.onnx + feature_columns.json (啟動時載入或熱更新)                        │
└─────────────────────────────────────────────────────────────────────────────────┘
```

### 職責對照表

| 元件 | 職責 | 備註 |
|------|------|------|
| **.NET API** | 從資料庫讀取原始資料，輸出 CSV | 零 ML 邏輯 |
| **Python** | 特徵工程、訓練、匯出 ONNX | Random Forest |
| **.NET 預測服務** | 載入 ONNX，執行推論 | 使用 OnnxRuntime |

| 演算法 | scikit-learn Random Forest | 穩定、易解釋、效能良好 |
| **模型效能** | 良好 | Python 生態豐富，可持續優化 |
| **特徵工程** | Pandas 強大靈活 | 完全由 Python 負責 |
| **部署複雜度** | 需 ONNX 轉換 | 簡單（只輸出 CSV） |

**結論**：Random Forest 是平衡效能和可解釋性的最佳選擇，Python 訓練可獲得更好的模型品質。

---

## 2. 資料來源

### 2.1 MatchHistory 實體

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

### 2.2 GameFeatures JSON

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

## 2. CSV 格式定義（原始資料）

### 2.1 匯出格式（.NET 只輸出原始欄位）

CSV 包含所有原始欄位，特徵工程完全由 Python 負責：

```csv
player_count,hour_of_day,day_of_week,hero_level,hero_kills,deaths,unit_kills,total_gold,highest_atk,highest_def,highest_speed,atk_range,is_winner
5,14,3,15,5,3,30,5000,120,80,350,150,1
5,14,3,12,2,5,20,3500,100,60,320,150,0
```

### 2.2 欄位對照

| CSV 欄位 | 說明 | Python 會產生的衍生特徵 |
|----------|------|------------------------|
| player_count | 比賽人數 | death_ratio |
| hour_of_day | 遊戲時間（小時） | - |
| day_of_week | 星期幾 | - |
| hero_level | 英雄等級 | gold_per_level, atk_per_level, def_per_level, speed_per_level |
| hero_kills | 英雄殺 | kd_ratio, total_kills, gold_efficiency |
| deaths | 死亡次數 | kd_ratio, death_ratio |
| unit_kills | 小兵殺 | total_kills |
| total_gold | 總金幣 | gold_per_level, gold_efficiency |
| highest_atk | 最高攻擊 | atk_per_level |
| highest_def | 最高防禦 | def_per_level |
| highest_speed | 最高速度 | speed_per_level |
| atk_range | 攻擊範圍 | - |
| is_winner | 是否勝利（1/0，Label） | - |

---

## 4. 專案結構

### 4.1 完整架構

```
BoardGameAiDashboard/
├── ml_trainer/                              # ⭐ Python 訓練腳本目錄
│   ├── requirements.txt                     # Python 依賴
│   ├── train.py                             # 主訓練腳本
│   ├── feature_engineering.py               # 特徵工程（Python 完全負責）
│   ├── models/
│   │   └── random_forest_model.py           # RandomForest 模型
│   └── export_onnx.py                       # ONNX 導出
│
├── BoardGameAiDashboard.Application/        # 只保留預測相關
│   └── Features/
│       └── ML/
│           ├── Models/
│           │   ├── GameStatePredictionInput.cs
│           │   ├── GameStatePredictionResult.cs
│           │   └── ModelComparisonResult.cs
│           ├── Interfaces/
│           │   └── IWinRatePredictionService.cs
│           └── Commands/
│               ├── ExportCsv/
│               │   ├── ExportCsvCommand.cs
│               │   └── ExportCsvHandler.cs
│               └── PredictWinRate/
│                   ├── PredictWinRateQuery.cs
│                   └── PredictWinRateHandler.cs
│
├── BoardGameAiDashboard.Infrastructure/
│   └── Services/
│       └── ML/
│           ├── WinRatePredictionService.cs  # OnnxRuntime 預測
│           ├── CsvExportService.cs          # CSV 匯出（只輸出原始欄位）
│           └── FeatureEngineeringService.cs # 預測時的特徵工程（與 Python 一致）
│
└── BoardGameAiDashboard.Api/
    └── Controllers/
        ├── PredictionsController.cs
        └── CsvExportController.cs
```

---

## 5. Python 訓練腳本

### 5.1 依賴檔案

```text
# ml_trainer/requirements.txt
pandas>=2.0.0
numpy>=1.24.0
scikit-learn>=1.3.0
onnxruntime>=1.16.0
onnx>=1.14.0
skl2onnx>=1.16.0
joblib>=1.3.0
```

### 5.2 特徵工程

```python
# ml_trainer/feature_engineering.py
import pandas as pd
import numpy as np
from typing import List


class FeatureEngineering:
    """特徵工程：將原始特徵轉換為衍生特徵"""

    # 原始 → 衍生特徵對照表
    FEATURE_MAPPING = {
        'player_count': 'player_count',
        'hour_of_day': 'hour_of_day',
        'day_of_week': 'day_of_week',
        'hero_level': 'hero_level',
        'hero_kills': 'hero_kills',
        'deaths': 'deaths',
        'unit_kills': 'unit_kills',
        'total_gold': 'total_gold',
        'highest_atk': 'highest_atk',
        'highest_def': 'highest_def',
        'highest_speed': 'highest_speed',
        'atk_range': 'atk_range',
    }

    def __init__(self):
        self.feature_columns_: List[str] = []

    def fit_transform(self, df: pd.DataFrame) -> pd.DataFrame:
        """擬合並轉換資料"""
        df = df.copy()

        # 衍生特徵
        df['gold_per_level'] = df['total_gold'] / (df['hero_level'] + 1)
        df['atk_per_level'] = df['highest_atk'] / (df['hero_level'] + 1)
        df['def_per_level'] = df['highest_def'] / (df['hero_level'] + 1)
        df['speed_per_level'] = df['highest_speed'] / (df['hero_level'] + 1)

        # KDA
        df['kd_ratio'] = df['hero_kills'] / (df['deaths'] + 1)
        df['total_kills'] = df['hero_kills'] + df['unit_kills']

        # 經濟效率
        df['gold_efficiency'] = df['total_gold'] / (df['hero_kills'] + 1)
        df['death_ratio'] = df['deaths'] / (df['player_count'] + 1)

        # 數值穩定性（避免除零）
        df = df.fillna(0)
        df = df.replace([np.inf, -np.inf], 0)

        # 記錄特徵欄位
        self.feature_columns_ = [
            'player_count', 'hour_of_day', 'day_of_week',
            'hero_level', 'hero_kills', 'deaths', 'unit_kills',
            'total_gold', 'highest_atk', 'highest_def', 'highest_speed', 'atk_range',
            'gold_per_level', 'atk_per_level', 'def_per_level', 'speed_per_level',
            'kd_ratio', 'total_kills', 'gold_efficiency', 'death_ratio'
        ]

        return df

    def transform(self, df: pd.DataFrame) -> pd.DataFrame:
        """只轉換資料（用於預測時）"""
        return self.fit_transform(df)

    def get_feature_columns(self) -> List[str]:
        """取得特徵欄位名稱"""
        return self.feature_columns_
```

### 5.3 主訓練腳本

```python
# ml_trainer/train.py
#!/usr/bin/env python3
"""
ML 訓練腳本：Random Forest 訓練 + ONNX 匯出

職責分離：
- .NET 只輸出 CSV（原始資料）
- Python 負責特徵工程、訓練、ONNX 匯出

使用方法:
    python train.py --input training_data.csv --output ./models
    python train.py --input training_data.csv --output ./models --folds 5
"""

import argparse
import json
import os
import sys
from datetime import datetime
from pathlib import Path

import pandas as pd
import numpy as np
from sklearn.model_selection import StratifiedKFold
from sklearn.metrics import (
    accuracy_score, precision_score, recall_score,
    f1_score, log_loss, roc_auc_score
)

from feature_engineering import FeatureEngineering
from models.random_forest_model import RandomForestModel


class TrainingResult:
    """訓練結果"""

    def __init__(self):
        self.model_name = "RandomForest"
        self.metrics = {}
        self.feature_columns = []
        self.timestamp = datetime.utcnow().isoformat()

    def to_dict(self) -> dict:
        return {
            'model_name': self.model_name,
            'metrics': self.metrics,
            'feature_columns': self.feature_columns,
            'timestamp': self.timestamp
        }


def load_data(csv_path: str) -> pd.DataFrame:
    """載入 CSV 資料"""
    print(f"載入資料: {csv_path}")
    df = pd.read_csv(csv_path)

    # 檢查必要欄位
    required_columns = ['player_count', 'hero_level', 'hero_kills', 'deaths', 'is_winner']
    missing = set(required_columns) - set(df.columns)
    if missing:
        raise ValueError(f"缺少必要欄位: {missing}")

    print(f"已載入 {len(df)} 筆記錄")
    return df


def cross_validate(model, X: pd.DataFrame, y: pd.Series, folds: int = 5) -> dict:
    """執行交叉驗證"""
    print(f"執行 {folds}-Fold 交叉驗證...")

    skf = StratifiedKFold(n_splits=folds, shuffle=True, random_state=42)

    metrics_list = []
    for fold, (train_idx, val_idx) in enumerate(skf.split(X, y), 1):
        X_train, X_val = X.iloc[train_idx], X.iloc[val_idx]
        y_train, y_val = y.iloc[train_idx], y.iloc[val_idx]

        # 訓練
        model.fit(X_train, y_train)

        # 預測
        y_pred = model.predict(X_val)
        y_prob = model.predict_proba(X_val)[:, 1]

        # 計算指標
        metrics = {
            'fold': fold,
            'accuracy': accuracy_score(y_val, y_pred),
            'precision': precision_score(y_val, y_pred, zero_division=0),
            'recall': recall_score(y_val, y_pred, zero_division=0),
            'f1': f1_score(y_val, y_pred, zero_division=0),
            'log_loss': log_loss(y_val, y_prob),
            'auc': roc_auc_score(y_val, y_prob)
        }
        metrics_list.append(metrics)

        print(f"  Fold {fold}: Accuracy={metrics['accuracy']:.4f}, AUC={metrics['auc']:.4f}")

    # 彙總
    avg_metrics = {
        'accuracy': np.mean([m['accuracy'] for m in metrics_list]),
        'accuracy_std': np.std([m['accuracy'] for m in metrics_list]),
        'precision': np.mean([m['precision'] for m in metrics_list]),
        'recall': np.mean([m['recall'] for m in metrics_list]),
        'f1': np.mean([m['f1'] for m in metrics_list]),
        'log_loss': np.mean([m['log_loss'] for m in metrics_list]),
        'auc': np.mean([m['auc'] for m in metrics_list]),
        'per_fold': metrics_list
    }

    return avg_metrics


def main():
    parser = argparse.ArgumentParser(description='ML 訓練腳本 (Random Forest)')
    parser.add_argument('--input', '-i', required=True, help='輸入 CSV 檔案路徑')
    parser.add_argument('--output', '-o', default='./models', help='輸出目錄')
    parser.add_argument('--folds', '-k', type=int, default=5, help='交叉驗證折數')
    parser.add_argument('--min-samples', '-m', type=int, default=20, help='最少訓練樣本數')
    parser.add_argument('--n-estimators', '-n', type=int, default=100, help='決策樹數量')
    parser.add_argument('--max-depth', '-d', type=int, default=10, help='最大深度')
    args = parser.parse_args()

    # 檢查檔案
    if not os.path.exists(args.input):
        print(f"錯誤: 檔案不存在 - {args.input}")
        sys.exit(1)

    # 載入資料
    df = load_data(args.input)

    if len(df) < args.min_samples:
        print(f"錯誤: 訓練資料不足（需要 {args.min_samples}，實際 {len(df)}）")
        sys.exit(1)

    # Feature Engineering（Python 完全負責）
    print("\n執行特徵工程...")
    fe = FeatureEngineering()
    df_transformed = fe.fit_transform(df)

    # 準備特徵和標籤
    feature_columns = fe.get_feature_columns()
    X = df_transformed[feature_columns]
    y = df_transformed['is_winner']

    print(f"原始特徵: 12")
    print(f"衍生特徵: {len(feature_columns) - 12}")
    print(f"總特徵數量: {len(feature_columns)}")
    print(f"特徵欄位: {feature_columns}")
    print(f"正樣本比例: {y.mean():.2%}")

    # 初始化模型
    model = RandomForestModel(
        n_estimators=args.n_estimators,
        max_depth=args.max_depth
    )

    # 交叉驗證
    print("\n" + "="*50)
    print("Random Forest 交叉驗證")
    print("="*50)
    metrics = cross_validate(model, X, y, args.folds)

    print(f"\n平均準確率: {metrics['accuracy']:.4f} (±{metrics['accuracy_std']:.4f})")
    print(f"平均 AUC: {metrics['auc']:.4f}")
    print(f"平均 F1: {metrics['f1']:.4f}")

    # 在全部資料上訓練最終模型
    print("\n在全部資料上訓練最終模型...")
    model.fit(X, y)

    # 儲存結果
    result = TrainingResult()
    result.metrics = metrics
    result.feature_columns = feature_columns

    output_dir = Path(args.output)
    output_dir.mkdir(parents=True, exist_ok=True)

    timestamp = datetime.utcnow().strftime('%Y%m%d%H%M%S')
    model_path = output_dir / f'winrate_model_{timestamp}.onnx'
    report_path = output_dir / f'training_report_{timestamp}.json'
    feature_path = output_dir / f'feature_columns_{timestamp}.json'

    # 匯出 ONNX
    print(f"\n匯出 ONNX 模型: {model_path}")
    model.export_onnx(model_path, feature_columns)

    # 儲存報告
    with open(report_path, 'w', encoding='utf-8') as f:
        json.dump(result.to_dict(), f, indent=2, ensure_ascii=False)
    print(f"訓練報告已儲存: {report_path}")

    # 儲存特徵欄位（供預測時使用）
    with open(feature_path, 'w', encoding='utf-8') as f:
        json.dump(feature_columns, f, indent=2)
    print(f"特徵欄位已儲存: {feature_path}")

    print("\n" + "="*50)
    print("訓練完成!")
    print(f"模型路徑: {model_path}")
    print("="*50)


if __name__ == '__main__':
    main()
```

### 5.5 RandomForest 模型

```python
# ml_trainer/models/random_forest_model.py
"""
RandomForest 分類模型

使用 scikit-learn 的 RandomForestClassifier，
並透過 skl2onnx 匯出為 ONNX 格式供 .NET 使用。
"""

import joblib
from sklearn.ensemble import RandomForestClassifier
from skl2onnx.common.data_types import FloatTensorType
from skl2onnx import convert_sklearn


class RandomForestModel:
    """RandomForest 分類模型"""

    def __init__(self, n_estimators: int = 100, max_depth: int = 10, min_samples_leaf: int = 5):
        self.model = RandomForestClassifier(
            n_estimators=n_estimators,
            max_depth=max_depth,
            min_samples_leaf=min_samples_leaf,
            random_state=42,
            n_jobs=-1  # 使用所有 CPU 核心
        )
        self.feature_columns_ = None

    def fit(self, X, y):
        """訓練模型"""
        self.feature_columns_ = list(X.columns)
        self.model.fit(X, y)
        return self

    def predict(self, X):
        """預測類別"""
        return self.model.predict(X)

    def predict_proba(self, X):
        """預測機率"""
        return self.model.predict_proba(X)

    def get_feature_importance(self) -> dict:
        """取得特徵重要性"""
        if self.feature_columns_ is None:
            return {}
        return dict(zip(self.feature_columns_, self.model.feature_importances_))

    def export_onnx(self, output_path: str, feature_columns: list):
        """匯出為 ONNX 格式"""
        # 定義輸入類型（浮點數陣列，長度等於特徵數量）
        initial_type = [('float_input', FloatTensorType([None, len(feature_columns)]))]

        # 轉換為 ONNX
        onnx_model = convert_sklearn(
            self.model,
            initial_types=initial_type,
            target_opset=12  # ONNX Runtime 1.16 支援 opSet 12
        )

        # 儲存 ONNX 模型
        with open(output_path, 'wb') as f:
            f.write(onnx_model.SerializeToString())

        print(f"ONNX 模型已匯出: {output_path}")
        print(f"輸入名稱: float_input, 形狀: [?, {len(feature_columns)}]")
        print(f"輸出名稱: output_label, output_probability")

        # 同時儲存特徵欄位名稱（供 .NET 使用）
        import json
        feature_info_path = output_path.replace('.onnx', '_features.json')
        feature_info = {
            'feature_columns': feature_columns,
            'feature_count': len(feature_columns)
        }
        with open(feature_info_path, 'w', encoding='utf-8') as f:
            json.dump(feature_info, f, indent=2)
        print(f"特徵資訊已儲存: {feature_info_path}")
```

### 5.6 執行訓練

```bash
# 安裝依賴
cd ml_trainer
pip install -r requirements.txt

# 執行訓練
python train.py --input ../training_data.csv --output ./models --folds 5

# 或自訂參數
python train.py \
    --input ../training_data.csv \
    --output ./models \
    --n-estimators 200 \
    --max-depth 15 \
    --folds 10
```

---

## 6. API 專案：CSV 匯出功能

### 6.1 CSV 匯出服務

```csharp
// Infrastructure/Services/ML/CsvExportService.cs
public interface ICsvExportService
{
    Task<string> ExportToCsvAsync(
        Guid? gameId = null,
        int? limit = null,
        CancellationToken ct = default);
}

public class CsvExportService : ICsvExportService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CsvExportService> _logger;

    public CsvExportService(IUnitOfWork unitOfWork, ILogger<CsvExportService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<string> ExportToCsvAsync(
        Guid? gameId = null,
        int? limit = null,
        CancellationToken ct = default)
    {
        var matches = await _unitOfWork.MatchHistories
            .FindAsync(
                filter: m => !m.IsDeleted && (gameId == null || m.GameId == gameId),
                orderBy: q => q.OrderByDescending(m => m.PlayedAt),
                limit: limit,
                ct: ct);

        var rows = new List<CsvRow>();
        foreach (var match in matches)
        {
            var features = ParseGameFeatures(match.GameFeatures);

            rows.Add(new CsvRow
            {
                player_count = match.PlayerCount,
                hour_of_day = match.PlayedAt.Hour,
                day_of_week = (int)match.PlayedAt.DayOfWeek,
                hero_level = features.HeroLevel,
                hero_kills = features.HeroKills,
                deaths = features.Deaths,
                unit_kills = features.UnitKills,
                total_gold = features.TotalGold,
                highest_atk = features.HighestAtk,
                highest_def = features.HighestDef,
                highest_speed = features.HighestSpeed,
                atk_range = features.AtkRange,
                is_winner = match.IsWinner ? 1 : 0
            });
        }

        _logger.LogInformation("匯出 {Count} 筆記錄到 CSV", rows.Count);

        // 使用 CsvHelper 產生 CSV（小寫欄位名稱）
        using var memoryStream = new MemoryStream();
        using var writer = new StreamWriter(memoryStream);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        csv.WriteRecords(rows);
        await writer.FlushAsync();

        return Convert.ToBase64String(memoryStream.ToArray());
    }

    private ParsedFeatures ParseGameFeatures(Dictionary<string, string> features)
        => new()
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

    private class CsvRow
    {
        public int player_count { get; set; }
        public int hour_of_day { get; set; }
        public int day_of_week { get; set; }
        public int hero_level { get; set; }
        public int hero_kills { get; set; }
        public int deaths { get; set; }
        public int unit_kills { get; set; }
        public int total_gold { get; set; }
        public int highest_atk { get; set; }
        public int highest_def { get; set; }
        public int highest_speed { get; set; }
        public int atk_range { get; set; }
        public int is_winner { get; set; }
    }
}
```

### 6.2 CQRS Handler

```csharp
// Application/Features/ML/Commands/ExportCsv/ExportCsvHandler.cs
public sealed class ExportCsvHandler : IRequestHandler<ExportCsvCommand, ExportCsvResult>
{
    private readonly ICsvExportService _csvExportService;

    public ExportCsvHandler(ICsvExportService csvExportService)
    {
        _csvExportService = csvExportService;
    }

    public async Task<ExportCsvResult> Handle(ExportCsvCommand request, CancellationToken ct)
    {
        var base64Content = await _csvExportService.ExportToCsvAsync(
            request.GameId,
            request.Limit,
            ct);

        return new ExportCsvResult
        {
            FileName = string.IsNullOrEmpty(request.GameId)
                ? $"training_data_all_{DateTime.UtcNow:yyyyMMddHHmmss}.csv"
                : $"training_data_{request.GameId}_{DateTime.UtcNow:yyyyMMddHHmmss}.csv",
            ContentBase64 = base64Content
        };
    }
}

public sealed record ExportCsvCommand : IRequest<ExportCsvResult>
{
    public Guid? GameId { get; init; }
    public int? Limit { get; init; }
}

public sealed class ExportCsvResult
{
    public string FileName { get; set; } = string.Empty;
    public string ContentBase64 { get; set; } = string.Empty;
}
```

### 6.3 CSV 匯出端點

```csharp
// Api/Controllers/CsvExportController.cs
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class CsvExportController : ControllerBase
{
    private readonly ISender _sender;

    public CsvExportController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// 匯出訓練資料為 CSV（用於 ML 訓練）
    /// </summary>
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
    /// 直接下載 CSV 檔案
    /// </summary>
    [HttpGet("download")]
    [Authorize(Roles = "Admin")]
    [Produces("text/csv")]
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
```

---

## 7. API 專案：ONNX 預測服務

### 7.1 預測服務介面

```csharp
// Application/Features/ML/Interfaces/IWinRatePredictionService.cs
public interface IWinRatePredictionService
{
    /// <summary>預測勝率</summary>
    Task<GameStatePredictionResult> PredictWinRateAsync(
        GameStatePredictionInput input,
        CancellationToken ct = default);

    /// <summary>檢查模型是否已載入</summary>
    bool IsModelLoaded { get; }

    /// <summary>重新載入模型</summary>
    Task ReloadModelAsync(CancellationToken ct = default);
}
```

### 7.2 OnnxRuntime 預測服務

```csharp
// Infrastructure/Services/ML/WinRatePredictionService.cs
using Microsoft.ML;
using Microsoft.Extensions.Options;

public class WinRatePredictionService : IWinRatePredictionService, IDisposable
{
    private readonly IFeatureEngineeringService _featureEngineering;
    private readonly string _modelPath;
    private readonly string _featureColumnsPath;
    private readonly ILogger<WinRatePredictionService> _logger;

    private InferenceSession? _session;
    private string[]? _featureColumns;
    private readonly object _lock = new();

    public WinRatePredictionService(
        IFeatureEngineeringService featureEngineering,
        IOptions<MLPredictionOptions> options,
        ILogger<WinRatePredictionService> logger)
    {
        _featureEngineering = featureEngineering;
        _modelPath = options.Value.ModelPath;
        _featureColumnsPath = options.Value.FeatureColumnsPath;
        _logger = logger;

        InitializeModel();
    }

    public bool IsModelLoaded => _session != null;

    public async Task ReloadModelAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("重新載入 ONNX 模型: {Path}", _modelPath);
        InitializeModel();
        await Task.CompletedTask;
    }

    public async Task<GameStatePredictionResult> PredictWinRateAsync(
        GameStatePredictionInput input,
        CancellationToken ct = default)
    {
        if (_session == null || _featureColumns == null)
        {
            throw new NotFoundException("ML 模型尚未載入。請確認模型檔案存在於設定路徑。");
        }

        // 特徵工程
        var features = _featureEngineering.TransformToFeatureVector(input);

        // ONNX Runtime 預測
        var probability = await PredictWithOnnxAsync(features);

        // 產生關鍵因素和建議
        var keyFactors = GenerateKeyFactors(features, input);

        return new GameStatePredictionResult
        {
            WinProbability = probability,
            ConfidenceScore = probability > 0.5f ? probability : 1 - probability,
            KeyFactors = keyFactors,
            Recommendation = GenerateRecommendation(keyFactors, probability)
        };
    }

    private async Task<float> PredictWithOnnxAsync(float[] features)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                // 準備輸入（ONNX 需要 named inputs）
                var inputs = new[]
                {
                    NamedOnnxValue.CreateFromTensor(
                        "float_input",
                        TensorFloat.CreateFrom(features))
                };

                // 執行推論
                using var results = _session!.Run(inputs);
                var output = results.FirstOrDefault();

                if (output == null)
                {
                    throw new InvalidOperationException("ONNX 模型輸出為空");
                }

                // 取得機率（取第二個輸出，即勝率）
                var probabilities = output.AsEnumerable<float>().ToArray();
                return probabilities.Length > 1 ? probabilities[1] : probabilities[0];
            }
        });
    }

    private void InitializeModel()
    {
        if (!File.Exists(_modelPath))
        {
            _logger.LogWarning("ONNX 模型檔案不存在: {Path}，預測功能暫時無法使用", _modelPath);
            return;
        }

        try
        {
            // 建立 ONNX Runtime Session
            var sessionOptions = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
            };

            _session = new InferenceSession(_modelPath, sessionOptions);

            // 載入特徵欄位
            if (File.Exists(_featureColumnsPath))
            {
                var json = File.ReadAllText(_featureColumnsPath);
                _featureColumns = JsonSerializer.Deserialize<string[]>(json);
            }
            else
            {
                // 使用預設特徵欄位
                _featureColumns = new[]
                {
                    "player_count", "hour_of_day", "day_of_week",
                    "hero_level", "hero_kills", "deaths", "unit_kills",
                    "total_gold", "highest_atk", "highest_def", "highest_speed", "atk_range",
                    "gold_per_level", "atk_per_level", "def_per_level", "speed_per_level",
                    "kd_ratio", "total_kills", "gold_efficiency", "death_ratio"
                };
            }

            _logger.LogInformation("ONNX 模型已成功載入: {Path}", _modelPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "載入 ONNX 模型失敗: {Path}", _modelPath);
            throw;
        }
    }

    private List<FeatureImpact> GenerateKeyFactors(float[] features, GameStatePredictionInput input)
    {
        var factors = new List<FeatureImpact>();

        // 根據特徵值產生關鍵因素
        var heroLevel = features[3];  // hero_level 索引
        var kdRatio = features[16];   // kd_ratio 索引
        var goldPerLevel = features[12]; // gold_per_level 索引

        if (heroLevel > 10)
            factors.Add(new FeatureImpact { FeatureName = "HeroLevel", ImpactScore = 0.1f, Description = $"等級 {heroLevel:F0}，高於平均" });

        if (kdRatio > 1.0f)
            factors.Add(new FeatureImpact { FeatureName = "KdRatio", ImpactScore = 0.15f, Description = $"KDA {kdRatio:F2}" });
        else if (kdRatio < 0.5f)
            factors.Add(new FeatureImpact { FeatureName = "KdRatio", ImpactScore = -0.1f, Description = $"KDA {kdRatio:F2}，偏低" });

        if (goldPerLevel > 300)
            factors.Add(new FeatureImpact { FeatureName = "GoldPerLevel", ImpactScore = 0.08f, Description = $"等均金幣 {goldPerLevel:F0}" });

        return factors.OrderByDescending(f => Math.Abs(f.ImpactScore)).Take(5).ToList();
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

    public void Dispose()
    {
        _session?.Dispose();
    }
}

// 設定選項
public class MLPredictionOptions
{
    public string ModelPath { get; set; } = "./models/winrate_model.onnx";
    public string FeatureColumnsPath { get; set; } = "./models/winrate_model_features.pkl";
}
```

### 7.3 特徵工程服務

```csharp
// Infrastructure/Services/ML/FeatureEngineeringService.cs
public interface IFeatureEngineeringService
{
    /// <summary>
    /// 將輸入轉換為 ML 特徵向量（與 Python 端一致）
    /// </summary>
    float[] TransformToFeatureVector(GameStatePredictionInput input);
}

public class FeatureEngineeringService : IFeatureEngineeringService
{
    public float[] TransformToFeatureVector(GameStatePredictionInput input)
    {
        var heroLevel = (float)input.HeroLevel;
        var heroKills = (float)input.HeroKills;
        var deaths = (float)input.Deaths;
        var totalGold = (float)input.TotalGold;

        return new float[]
        {
            // 原始特徵
            input.PlayerCount,
            input.HourOfDay,
            input.DayOfWeek,
            heroLevel,
            heroKills,
            deaths,
            (float)input.UnitKills,
            totalGold,
            (float)input.HighestAtk,
            (float)input.HighestDef,
            (float)input.HighestSpeed,
            (float)input.AtkRange,
            // 衍生特徵
            SafeDivide(totalGold, heroLevel + 1),        // gold_per_level
            SafeDivide((float)input.HighestAtk, heroLevel + 1),  // atk_per_level
            SafeDivide((float)input.HighestDef, heroLevel + 1),   // def_per_level
            SafeDivide((float)input.HighestSpeed, heroLevel + 1), // speed_per_level
            SafeDivide(heroKills, deaths + 1),           // kd_ratio
            heroKills + (float)input.UnitKills,          // total_kills
            SafeDivide(totalGold, heroKills + 1),        // gold_efficiency
            SafeDivide(deaths, input.PlayerCount + 1)    // death_ratio
        };
    }

    private float SafeDivide(float numerator, float denominator)
        => denominator > 0 ? numerator / denominator : 0;
}
```

---

## 8. CQRS Handlers

### 8.1 PredictWinRateHandler

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
    private readonly IWinRatePredictionService _predictionService;

    public PredictionsController(
        ISender sender,
        IWinRatePredictionService predictionService)
    {
        _sender = sender;
        _predictionService = predictionService;
    }

    /// <summary>
    /// 預測遊戲狀態的勝率
    /// </summary>
    [HttpPost("predict")]
    [ProducesResponseType(typeof(GameStatePredictionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PredictWinRate(
        [FromBody] GameStatePredictionInput input,
        CancellationToken ct)
    {
        if (!_predictionService.IsModelLoaded)
        {
            return NotFound(new { message = "ML 模型尚未載入，請先執行訓練。" });
        }

        var query = new PredictWinRateQuery { Input = input };
        var result = await _sender.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// 分析特定英雄 Level 對勝率的影響
    /// </summary>
    [HttpGet("analyze/{gameId:guid}")]
    [ProducesResponseType(typeof(Dictionary<string, List<float>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AnalyzeWinRateByLevel(
        Guid gameId,
        [FromQuery] int heroLevel = 15,
        [FromQuery] int heroKills = 5,
        [FromQuery] int deaths = 3,
        [FromQuery] int totalGold = 5000,
        CancellationToken ct = default)
    {
        if (!_predictionService.IsModelLoaded)
        {
            return NotFound(new { message = "ML 模型尚未載入。" });
        }

        var predictions = new Dictionary<string, List<float>>();
        predictions["heroLevels"] = new();
        predictions["winProbabilities"] = new();

        for (int level = Math.Max(1, heroLevel - 5); level <= heroLevel + 5; level++)
        {
            var input = new GameStatePredictionInput
            {
                GameId = gameId,
                PlayerCount = 5,
                HeroLevel = level,
                HeroKills = heroKills,
                Deaths = deaths,
                TotalGold = totalGold + (level - heroLevel) * 100,
                HighestAtk = 50 + level * 5,
                HighestDef = 30 + level * 3,
                HighestSpeed = 300 + level * 2,
                AtkRange = 150,
                HourOfDay = DateTime.UtcNow.Hour,
                DayOfWeek = (int)DateTime.UtcNow.DayOfWeek
            };

            var result = await _sender.Send(new PredictWinRateQuery { Input = input }, ct);
            predictions["heroLevels"].Add(level);
            predictions["winProbabilities"].Add(result.WinProbability);
        }

        return Ok(predictions);
    }

    /// <summary>
    /// 重新載入 ML 模型
    /// </summary>
    [HttpPost("reload-model")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ReloadModel(CancellationToken ct)
    {
        await _predictionService.ReloadModelAsync(ct);
        return Ok(new { message = "模型已重新載入" });
    }

    /// <summary>
    /// 檢查模型狀態
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetModelStatus()
    {
        return Ok(new
        {
            modelLoaded = _predictionService.IsModelLoaded,
            timestamp = DateTime.UtcNow
        });
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

    // ── ML Prediction Services ────────────────────────────────────
    services.AddScoped<IFeatureEngineeringService, FeatureEngineeringService>();
    services.AddScoped<ICsvExportService, CsvExportService>();

    // ML 預測選項
    services.Configure<MLPredictionOptions>(options =>
    {
        options.ModelPath = configuration["ML:ModelPath"] ?? "./models/winrate_model.onnx";
        options.FeatureColumnsPath = configuration["ML:FeatureColumnsPath"] ?? "./models/winrate_model_features.pkl";
    });

    // OnnxRuntime InferenceSession 是執行緒安全的，使用 Singleton
    services.AddSingleton<IWinRatePredictionService, WinRatePredictionService>();

    return services;
}
```

---

## 11. 設定檔

### 11.1 API appsettings.json

```json
{
  "ML": {
    "ModelPath": "./models/winrate_model.onnx",
    "FeatureColumnsPath": "./models/winrate_model_features.pkl"
  }
}
```

---

## 12. 模型部署流程

```
┌──────────────────────────────────────────────────────────────────────────────────┐
│                            模型訓練與部署流程                                      │
└──────────────────────────────────────────────────────────────────────────────────┘

  1. API 匯出 CSV
     ┌────────────────────────────────────────────────────────────────────────────┐
     │ GET /api/csvexport/download?gameId=xxx&limit=10000                        │
     │                                                                            │
     │ Response: CSV 檔案                                                          │
     │ 保存至: training_data.csv                                                   │
     └────────────────────────────────────────────────────────────────────────────┘

  2. Python 訓練
     ┌────────────────────────────────────────────────────────────────────────────┐
     │ cd ml_trainer                                                              │
     │ pip install -r requirements.txt                                            │
     │ python train.py --input ../training_data.csv --output ./models             │
     │                                                                            │
     │ 輸出:                                                                       │
     │   models/winrate_model_20260804120000.onnx                                 │
     │   models/winrate_model_20260804120000_features.pkl                         │
     │   models/training_report_20260804120000.json                               │
     └────────────────────────────────────────────────────────────────────────────┘

  3. 部署 ONNX 模型（任選其一）
     ┌────────────────────────────────────────────────────────────────────────────┐
     │ 方式 A：本地檔案共享                                                         │
     │   複製 .onnx 和 .pkl 到 API 伺服器的 ./models/ 目錄                         │
     │                                                                            │
     │ 方式 B：雲端儲存                                                            │
     │   上傳至 Azure Blob / AWS S3 / GCS                                         │
     │   API 從 URL 載入模型                                                       │
     │                                                                            │
     │ 方式 C：自動化部署                                                          │
     │   CI/CD Pipeline 自動部署新模型到伺服器                                     │
     └────────────────────────────────────────────────────────────────────────────┘

  4. 熱更新（可選）
     ┌────────────────────────────────────────────────────────────────────────────┐
     │ POST /api/predictions/reload-model                                         │
     │ （無需重啟 API，動態重新載入模型）                                           │
     └────────────────────────────────────────────────────────────────────────────┘
```

---

## 13. NuGet 套件

```xml
<!-- BoardGameAiDashboard.Infrastructure/BoardGameAiDashboard.Infrastructure.csproj -->
<PackageReference Include="Microsoft.ML" Version="3.0.1" />
<PackageReference Include="CsvHelper" Version="31.0.0" />
<PackageReference Include="Microsoft.ML.OnnxRuntime" Version="1.16.0" />
```

---

## 14. 實作步驟

| 步驟 | 任務 | 檔案 | 說明 |
|------|------|------|------|
| 1 | 建立 Python 訓練目錄 | `ml_trainer/` | Python 腳本，零 .NET 依賴 |
| 2 | 建立 Python 依賴 | `ml_trainer/requirements.txt` | scikit-learn, onnxruntime |
| 3 | 建立特徵工程腳本 | `ml_trainer/feature_engineering.py` | 衍生特徵計算 |
| 4 | 建立模型腳本 | `ml_trainer/models/random_forest_model.py` | RandomForest 模型 |
| 5 | 建立主訓練腳本 | `ml_trainer/train.py` | 交叉驗證、ONNX 導出 |
| 6 | 建立 CSV 匯出服務 | `Infrastructure/Services/ML/CsvExportService.cs` | MatchHistory → CSV |
| 7 | 建立 CSV 匯出端點 | `Api/Controllers/CsvExportController.cs` | `/api/csvexport/*` |
| 8 | 建立 OnnxRuntime 預測服務 | `Infrastructure/Services/ML/WinRatePredictionService.cs` | ONNX 推論 |
| 9 | 建立特徵工程服務 | `Infrastructure/Services/ML/FeatureEngineeringService.cs` | 預測時的特徵工程 |
| 10 | 更新 DI 註冊 | `Infrastructure/DependencyInjection.cs` | 加入服務註冊 |
| 11 | 設定 appsettings | `appsettings.json` | ML:ModelPath |

---

## 15. 驗證方式

```bash
# 1. 安裝 Python 依賴
cd ml_trainer
pip install -r requirements.txt

# 2. API 建置
dotnet build

# 3. API 匯出 CSV
curl -o training_data.csv "http://localhost:5001/api/csvexport/download?limit=1000"

# 4. Python 訓練
cd ml_trainer
python train.py --input ../training_data.csv --output ./models

# 5. 測試 API
# 檢查模型狀態
GET http://localhost:5001/api/predictions/status

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
  "atkRange": 150,
  "hourOfDay": 14,
  "dayOfWeek": 1
}

# 重新載入模型
POST http://localhost:5001/api/predictions/reload-model
```

---

## 16. 架構優勢總結

| 優勢 | 說明 |
|------|------|
| **訓練靈活性** | Python 生態豐富，RandomForest 穩定高效 |
| **模型效能** | Python 訓練的模型通常比 ML.NET 更好 |
| **完全解耦** | Python 訓練腳本不需要任何 .NET 依賴 |
| **跨平台部署** | ONNX 是跨平台標準，任何環境都能載入 |
| **高效推論** | OnnxRuntime 經過優化，推論速度快 |
| **版本控制** | CSV 可纳入 Git 追蹤，模型版本化部署 |
| **除錯友好** | Python 端可用 Jupyter Notebook 分析 |
| **特徵工程隔離** | .NET 只輸出原始資料，Python 完全負責特徵工程，確保訓練和預測一致 |
