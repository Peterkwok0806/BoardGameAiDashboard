# CLAUDE.md

此檔案提供給 Claude Code (claude.ai/code) 在此專案工作時的指導方針。

## 專案概覽

BoardGame AI Dashboard — 一個基於 RAG 的桌上型遊戲分析平台，具備 AI 聊天、勝率預測和對戰紀錄追蹤。技術棧：.NET 8 + Angular 19 + SQL Server + Qdrant + Redis + Semantic Kernel。

## 常用 Skills（依需求自動使用）

依據任務類型，主動使用 `/skill-name` 載入對應技能：

| 指令 | 適用場景 |
|------|----------|
| `/efcore-migration` | 資料庫遷移、軟刪除、查詢過濾器 |
| `/rag-pipeline` | PDF 處理、向量搜尋、RAG 開發 |
| `/angular-signals` | Angular 19 Signals、Standalone Components |
| `/jwt-auth` | JWT 認證、Refresh Token、授權 |

### Code Review Sub-Agents

| 指令 | 審查範圍 |
|------|----------|
| `/angular` | Angular/TypeScript 前端程式碼審查 |
| `/dotnet` | C# 後端、Clean Architecture 審查 |

---

## 常用指令

```bash
# 建置與執行
cd BoardGameAiDashboard
dotnet build                                    # 建置解決方案
dotnet run --project BoardGameAiDashboard.Api   # 執行 API (port 5001)
cd DashboardFrontend && npm start               # 執行 Angular 前端 (port 4200)

# 測試
dotnet test                          # 執行所有測試
dotnet test --filter "FullyQualifiedName~SoftDeleteTests"  # 執行特定測試類別
dotnet test BoardGameAiDashboard.Tests  # 執行單元測試專案

# 資料庫遷移
dotnet ef migrations add <Name> --project BoardGameAiDashboard.Infrastructure --startup-project BoardGameAiDashboard.Api
dotnet ef database update --project BoardGameAiDashboard.Infrastructure --startup-project BoardGameAiDashboard.Api

# 程式碼風格
dotnet format                                   # 格式化程式碼
dotnet build --no-restore /p:TreatWarningsAsErrors=true  # 嚴格建置
```

## 架構

### 四層 Clean Architecture

```
Domain (零依賴) ← Application (MediatR, 介面) ← Infrastructure (EF Core, Qdrant, ML.NET) ← Api (Controllers, Middleware)
```

**依賴方向**：`Domain ← Application ← Infrastructure ← Api`

各層職責：
- **Domain**：Entities、enums、value objects — 無外部 NuGet 套件
- **Application**：透過 MediatR 實作 CQRS、FluentValidation、定義基礎設施介面
- **Infrastructure**：EF Core、Qdrant、Semantic Kernel、ML.NET、Redis、Hangfire、JWT 實作
- **Api**：HTTP 端點、middleware、DI 組裝 — 不含業務邏輯

### 專案結構

```
BoardGameAiDashboard/
├── BoardGameAiDashboard.Domain/         # Entities、enums、value objects
├── BoardGameAiDashboard.Application/    # CQRS handlers、validators、介面
├── BoardGameAiDashboard.Infrastructure/  # EF Core、services、repositories
├── BoardGameAiDashboard.Api/             # Controllers、middleware、Program.cs
├── BoardGameAiDashboard.Tests/           # xUnit + Moq 單元測試
DashboardFrontend/                        # Angular 19 SPA
```

## 核心模式

### CQRS via MediatR

每個 API 操作都透過 MediatR：
- **Commands**：寫入操作（`CreateGameCommand`、`LoginUserCommand`）
- **Queries**：讀取操作（`GetGamesQuery`、`GetWinRateQuery`）
- Handlers 放置於 `Application/Features/{Feature}/Commands|Queries/{Name}/`
- FluentValidation 驗證在 MediatR pipeline 中執行（`ValidationBehavior`）

### API 回應包裝器

所有成功回應都由 `ApiResultFilter` 自動包裝：
```json
{ "success": true, "data": {...}, "message": "...", "timestamp": "..." }
```
Controllers 回傳原始資料 — 禁止手動呼叫 `ApiResult<T>.Ok()`。錯誤使用 RFC 7807 ProblemDetails。

### 軟刪除模式

所有實體都有 `IsDeleted` 並搭配 EF Core 全域查詢過濾器。查詢自動過濾 `!IsDeleted`。軟刪除時使用網域方法（例如 `game.Delete()`）。

### RAG 流程（核心功能）

```
PDF → 文字擷取 → 分塊 → 嵌入 (ITextEmbeddingGenerationService)
                                                    ↓
                                          Qdrant (向量儲存)
                                                    ↓
查詢 → 嵌入 → Qdrant 搜尋 (metadata: game_id, section_title) → 建構 Context
                                                               ↓
                                              Semantic Kernel → LLM 回應
```

關鍵檔案：
- `VectorSearchService.cs` — Qdrant 操作，含 metadata 過濾
- `DocumentIngestionService.cs` — PDF → chunks → 嵌入 → 儲存流程
- `RagService.cs` — RAG 協調（查詢重寫、Context 建構、回應）

### JWT 認證

- Access token：15 分鐘 TTL
- Refresh token：7 天 TTL，單次使用並輪換
- Refresh token 端點：`POST /api/auth/refresh`
- 除 `/api/auth/*` 外所有端點都需要 `[Authorize]`

### Redis 快取

快取模式（Cache-aside）+ TTL：
- `game:{id}` — 10 分鐘
- `winrate:{gameId}:{params}` — 5 分鐘
- `rag:{hash}` — 30 分鐘

### JSON 欄位

使用彈性屬性的實體採用 `Dictionary<string, string>` 並搭配 EF Core `ValueConverter` + `ValueComparer`：
- `GameCharacter.CustomProperties`
- `GameCard.CardProperties`
- `MatchHistory.GameFeatures`

## 設定檔

`appsettings.json` / `appsettings.Development.json` 包含：
- **ConnectionStrings:DefaultConnection** — SQL Server
- **Redis:Connection** — Redis 主機
- **Jwt:*** — JWT secret、issuer、audience、TTL
- **Ollama:Endpoint/ChatModel/EmbeddingModel** — 本地 LLM（取代 Azure OpenAI）
- **Qdrant:Endpoint/CollectionName/VectorDimension** — 向量資料庫

## 前端 (DashboardFrontend)

Angular 19 具備：
- Standalone Components（無 NgModules）
- Signals 狀態管理
- Tailwind CSS
- ng2-charts (Chart.js)
- SweetAlert2 確認對話框
- HTTP interceptor 處理 JWT + `ApiResult<T>` 解包

## 重要約束

1. **僅使用 async**：嚴禁使用 `.Result` 或 `.Wait()` — 始終使用 `async/await`
2. **薄 Controller**：Controllers 只處理 HTTP；所有邏輯放在 CQRS handlers
3. **網域例外**：使用 `NotFoundException`、`ValidationException`、`UnauthorizedException`、`ConflictException` — 禁止使用原始 `InvalidOperationException`
4. **軟刪除**：所有查詢自動過濾 `IsDeleted == false`
5. **Semantic Kernel**：LLM 編排一律使用 Semantic Kernel — 禁止直接發送 HTTP 呼叫到 OpenAI/Ollama
6. **Metadata 過濾**：所有 Qdrant 搜尋必須過濾 `game_id` 和/或 `section_title`
7. **先分塊再送 LLM**：嚴禁將整份 PDF 塞進 LLM context — 必須 chunk → embed → retrieve → answer
