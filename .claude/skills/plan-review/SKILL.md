---
name: plan-review
description: 驗證實作計劃（plan-*.md）的可行性，包括依賴套件、代碼結構、架構一致性、邏輯正確性和潛在性能衝突。
---

## 使用時機

- 使用者要求審查 `plan-*.md` 文件
- 驗證新功能實作計劃的可行性
- 檢查計劃與現有架構的相容性


## 執行步驟

### 1. 讀取計劃文件

使用 Read tool 讀取計劃文件（通常是 `plan-*.md`）。

### 2. 分析專案現況

並行檢查以下項目：

| 檢查項目 | 方法 |
|----------|------|
| 現有 .csproj 檔案 | `Glob` 找 `**/*.csproj` |
| Domain 實體 | `Glob` 找 `Domain/Entities/*.cs` |
| 現有介面 | `Glob` 找 `Application/Common/Interfaces/*.cs` |
| Infrastructure 服務 | `Glob` 找 `Infrastructure/Services/*.cs` |
| DI 註冊 | `Read` `Infrastructure/DependencyInjection.cs` |

### 3. 驗證檢查清單

對每個計劃執行以下檢查：

#### 3.1 Package/依賴檢查

```
□ NuGet 套件是否已存在？
  - 讀取對應 .csproj 檢查 PackageReference
  - 若不存在，列出需要新增的套件及版本

□ 版本相容性
  - .NET 8 → 確認套件支援 net8.0
  - Microsoft.ML 建議 3.0+
  - Semantic Kernel 建議 1.0+
```

#### 3.2 現有程式碼覆蓋檢查

```
□ Domain 實體
  - 計劃中的實體定義是否與現有實體相符？
  - 欄位名稱、類型是否匹配？

□ 現有介面
  - 計劃中定義的介面是否已存在？
  - 方法簽名是否相容？

□ 現有 CQRS handlers
  - 是否有同名的 Command/Query 已存在？
  - 若存在，是否為 placeholder 需要實作？
```

#### 3.3 架構一致性檢查

```
□ Clean Architecture 約束
  - Domain 層是否零依賴？（無外部 NuGet）
  - Application 層是否只依賴 Domain + 基礎設施介面？
  - Infrastructure 層是否實作 Application 定義的介面？

□ 命名空間一致性
  - Domain: BoardGameAiDashboard.Domain.*
  - Application: BoardGameAiDashboard.Application.*
  - Infrastructure: BoardGameAiDashboard.Infrastructure.*

□ DI 註冊模式
  - Services.AddScoped / Services.AddSingleton 使用是否正確？
  - Singleton 用於無狀態服務或昂貴初始化（如 MLContext）
```

#### 3.4 邏輯正確性檢查

```
□ 命名規範
  - C#: PascalCase for types/methods, _camelCase for private fields
  - 介面以 I 開頭（IWinRatePredictionService）
  - Command/Query 以 Command/Query 結尾

□ 例外處理
  - 使用 NotFoundException, ValidationException 等網域例外
  - 禁止直接使用 InvalidOperationException

□ async/await
  - 所有 I/O 操作必須是 async
  - 禁止 .Result 或 .Wait()
```

#### 3.5 性能衝突檢查

```
□ ML.NET 特定
  - MLContext 應該是 Singleton（昂貴資源）
  - 模型預測引擎 PredictionEngine 需要每次 Create？
  - 訓練是否放在 Background Job？

□ 記憶體
  - 大型資料集是否分批處理？
  - 模型檔案儲存位置是否正確？

□ 併發
  - 多個訓練請求是否需要排隊？
  - 模型 cache 是否執行緒安全？
```

### 4. 產出驗證報告

使用以下格式輸出驗證結果：

```markdown
# 計劃可行性驗證報告

## 基本資訊
- **計劃檔案**: plan-xxx.md
- **驗證日期**: {date}
- **驗證結果**: ✅ 可行 / ⚠️ 需要修改 / ❌ 不可行

## 1. Package/依賴驗證

| 套件 | 現有版本 | 需要版本 | 狀態 |
|------|----------|----------|------|
| Microsoft.ML | 5.0.0 | 5.0.0 | ✅ 已存在 |

### 需要新增的套件
- (列出需要新增的套件)

## 2. 現有程式碼覆蓋

### 2.1 Domain 實體
- MatchHistory: ✅ 存在，欄位匹配
- Game: ✅ 存在

### 2.2 現有 CQRS
- GetWinRateQuery: ⚠️ 存在 placeholder
- TrainWinRateModelCommand: ❌ 不存在，需要新建

## 3. 架構一致性

| 檢查項 | 狀態 | 說明 |
|--------|------|------|
| Domain 無外部依賴 | ✅ | |
| DI 註冊模式 | ⚠️ | WinRatePredictionService 應為 Singleton |
| 命名空間 | ✅ | 符合規範 |

## 4. 邏輯正確性

| 檢查項 | 狀態 | 說明 |
|--------|------|------|
| async/await | ✅ | |
| 例外處理 | ⚠️ | 需使用 ValidationException 而非直接拋字串 |
| 命名規範 | ✅ | |

## 5. 性能衝突

| 風險 | 等級 | 建議 |
|------|------|------|
| MLContext 作為 Singleton | 🟡 中 | 建議使用 DI Singleton |
| 訓練阻塞 HTTP 請求 | 🔴 高 | 考慮使用 Hangfire BackgroundJob |

## 6. 修改建議

### 高優先順序
1. [具體建議]

### 中優先順序
1. [具體建議]

### 低優先順序
1. [具體建議]

## 7. 實作步驟建議

| 順序 | 任務 | 檔案 |
|------|------|------|
| 1 | 加入 NuGet 套件 | Infrastructure.csproj |
| 2 | 建立 ML 模型類別 | Application/Features/ML/Models/*.cs |
| ... | ... | ... |

## 8. 總結

[總結建議]
```

## 輸出要求

- 驗證結果分為四個等級：✅ 可行、⚠️ 需要修改、❌ 不可行、ℹ️ 資訊
- 每個問題都必須有具體的修改建議
- 最後提供清晰的實作順序建議
- 若發現重大問題（❌），說明原因並提供替代方案
