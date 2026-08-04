# Python ML Training Skill

## 目的
使用 Python 訓練機器學習模型並匯出為 ONNX 格式，供 .NET OnnxRuntime 推理使用。

## 適用場景
- 新增或更新 ML 模型
- 調整模型超參數
- 測試新的特徵工程
- 模型效能評估

## 專案結構
```
ml_trainer/
├── train.py                      # 主訓練腳本
├── models/
│   ├── winrate_model.onnx        # ONNX 模型檔
│   ├── winrate_model_features.json
│   └── training_report.json
├── sample_training_data.csv      # 訓練資料
├── feature_engineering.py        # 特徵工程
└── models/
    └── random_forest_model.py    # RandomForest 包裝類別
```

## 訓練流程

### 1. 準備訓練資料
CSV 格式，包含原始特徵 + 衍生特徵 + 標籤：
```csv
player_count,hour_of_day,day_of_week,hero_level,hero_kills,deaths,unit_kills,total_gold,highest_atk,highest_def,highest_speed,atk_range,gold_per_level,atk_per_level,def_per_level,speed_per_level,kd_ratio,total_kills,gold_efficiency,death_ratio,is_winner
5,20,4,15,8,3,45,6000,150,100,380,150,375,10,6,24,2,53,667,0.5,1
```

必需欄位：
- 20 個特徵欄位（見上方）
- `is_winner`: 目標標籤 (0=失敗, 1=勝利)

### 2. 執行訓練
```bash
cd ml_trainer
python train.py --input training_data.csv --output ./models -n 100 -d 10
```

參數說明：
| 參數 | 預設值 | 說明 |
|------|--------|------|
| `-n, --n-estimators` | 100 | 決策樹數量 |
| `-d, --max-depth` | 10 | 樹的最大深度 |
| `-m, --min-samples` | 20 | 最小訓練樣本數 |
| `-k, --folds` | 5 | 交叉驗證折數 |
| `--no-validate` | false | 跳過輸入驗證 |

### 3. ONNX 匯出設定
**重要**：必須使用 `zipmap=False` 確保輸出為標準 Tensor：

```python
# ml_trainer/models/random_forest_model.py
onnx_model = convert_sklearn(
    clf,
    initial_types=initial_type,
    target_opset=12,
    options={'zipmap': False}  # 關鍵設定！
)
```

輸出格式：
- `output_label`: 預測類別 (int64)
- `output_probability`: 機率張量 (float, shape [?, 2])

### 4. 複製模型到 API
```bash
# 複製最新模型到 API 專案
cp ml_trainer/models/winrate_model_TIMESTAMP.onnx \
   BoardGameAiDashboard/BoardGameAiDashboard.Api/ml_trainer/models/winrate_model.onnx

cp ml_trainer/models/winrate_model_TIMESTAMP_features.json \
   BoardGameAiDashboard/BoardGameAiDashboard.Api/ml_trainer/models/winrate_model_features.json
```

## 特徵工程

### 原始特徵 (12)
| 欄位 | 說明 | 範圍 |
|------|------|------|
| player_count | 玩家數量 | 2-8 |
| hour_of_day | 遊戲時間（小時） | 0-23 |
| day_of_week | 星期幾 | 0-6 |
| hero_level | 英雄等級 | 1-20 |
| hero_kills | 英雄擊殺數 | 0-20 |
| deaths | 死亡次數 | 0-15 |
| unit_kills | 單位擊殺數 | 0-100 |
| total_gold | 總金幣 | 1000-15000 |
| highest_atk | 最高攻擊力 | 50-300 |
| highest_def | 最高防禦力 | 30-200 |
| highest_speed | 最高速度 | 200-500 |
| atk_range | 攻擊範圍 | 100-250 |

### 衍生特徵 (8)
| 欄位 | 公式 | 說明 |
|------|------|------|
| gold_per_level | total_gold / (hero_level + 1) | 每級金幣 |
| atk_per_level | highest_atk / (hero_level + 1) | 每級攻擊 |
| def_per_level | highest_def / (hero_level + 1) | 每級防禦 |
| speed_per_level | highest_speed / (hero_level + 1) | 每級速度 |
| kd_ratio | hero_kills / (deaths + 1) | KDA 比率 |
| total_kills | hero_kills + unit_kills | 總擊殺數 |
| gold_efficiency | total_gold / (hero_kills + 1) | 金幣效率 |
| death_ratio | deaths / (player_count + 1) | 死亡比率 |

## 驗證模型輸出

```python
import onnxruntime as ort
import numpy as np

session = ort.InferenceSession('model.onnx')

# 檢查輸出格式
for out in session.get_outputs():
    print(f"{out.name}: {out.shape}, {out.type}")

# 測試推理
test_input = np.array([[...20 features...]], dtype=np.float32)
result = session.run(None, {'float_input': test_input})
# result[1] 應該是 shape (1, 2) 的機率陣列
# result[1][0] = [P(class=0), P(class=1)]
```

## 常見問題

### Q: 模型輸出仍是 seq(map(...)) 格式？
A: 確認使用 `options={'zipmap': False}` 而非 `{clf: {'zipmap': False}}`

### Q: 訓練準確度過低？
A: 可能原因：
- 訓練資料不足（建議 >1000 筆）
- 特徵沒有預測能力（需要真實資料）
- 模型過擬合（減少 max_depth 或 n_estimators）

### Q: 如何從 .NET 匯出訓練資料？
A: 使用 API endpoint：
```bash
curl -H "Authorization: Bearer $TOKEN" \
  "http://localhost:5032/api/predictions/export?limit=1000" \
  -o training_data.csv
```
