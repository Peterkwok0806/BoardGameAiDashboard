# BoardGame AI Dashboard

一個基於 **RAG (Retrieval-Augmented Generation)** 的桌上型遊戲分析平台，整合 AI 聊天、勝率預測與對戰紀錄追蹤功能。

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)
![Angular 19](https://img.shields.io/badge/Angular-19-DD0031?style=flat-square&logo=angular)
![SQL Server](https://img.shields.io/badge/SQL%20Server-2022-CC2927?style=flat-square&logo=microsoftsqlserver)
![Qdrant](https://img.shields.io/badge/Vector%20DB-Qdrant-Red?style=flat-square)

## 主要功能

| 功能 | 描述 |
|------|------|
| **AI 遊戲規則助手** | 基於 RAG 技術的遊戲規則問答，可上傳 PDF 規則手冊並進行智慧搜尋 |
| **勝率預測** | 使用 ML.NET 訓練的 ONNX 模型，根據遊戲狀態預測勝率 |
| **對戰紀錄** | 追蹤玩家對戰歷史，分析勝率趨勢 |
| **遊戲管理** | 完整的桌上型遊戲資料管理（角色、卡片、規則） |
| **多輪對話** | 支援對話上下文，讓 AI 更了解你的問題 |
| **JWT 認證** | 安全的用戶認證系統，包含 Access Token 與 Refresh Token |

## 技術架構

### 四層 Clean Architecture

```
+-----------------------------------------------------------------+
|                        Api Layer                                 |
|    Controllers - Middleware - Swagger - Exception Handling      |
+-----------------------------------------------------------------+
|                    Infrastructure Layer                          |
|    EF Core - Qdrant - Redis - ML.NET - Semantic Kernel - JWT   |
+-----------------------------------------------------------------+
|                     Application Layer                            |
|         MediatR - CQRS - FluentValidation - DTOs               |
+-----------------------------------------------------------------+
|                       Domain Layer                               |
|          Entities - Enums - Value Objects - Exceptions          |
+-----------------------------------------------------------------+
```

### 技術棧

| 類別 | 技術 |
|------|------|
| **後端框架** | .NET 8 + ASP.NET Core |
| **前端框架** | Angular 19 (Standalone Components + Signals) |
| **資料庫** | SQL Server 2022 + Entity Framework Core |
| **向量資料庫** | Qdrant (用於 RAG 語義搜尋) |
| **快取** | Redis |
| **AI/LLM** | Semantic Kernel + Ollama (本地 LLM) |
| **機器學習** | ML.NET + ONNX Runtime |
| **任務排程** | Hangfire |
| **日誌** | Serilog |
| **驗證** | JWT Bearer Token |

### RAG 流程

```
+--------+    +----------+    +--------+    +--------+
|  PDF   |--->|  文字擷取 |--->|  分塊  |--->|  嵌入  |
+--------+    +----------+    +--------+    +----+---+
                                                  |
                                                  v
+--------+    +---------+    +-----------------------------+
|  LLM   |<---| 建構 Context |<--| Qdrant 語義搜尋 + Metadata 過濾 |
+--------+    +---------+    +-----------------------------+
```

## 專案結構

```
BoardGameAiDashboard/
+-- BoardGameAiDashboard.Domain/           # 網域層：Entities、Enums、Value Objects
+-- BoardGameAiDashboard.Application/       # 應用層：CQRS Handlers、介面、DTOs
+-- BoardGameAiDashboard.Infrastructure/   # 基礎設施層：EF Core、Qdrant、ML.NET
+-- BoardGameAiDashboard.Api/              # API 層：Controllers、Middleware
+-- BoardGameAiDashboard.Tests/            # 單元測試 (xUnit + Moq)
+-- DashboardFrontend/                     # Angular 19 前端
    +-- src/app/
        +-- core/                          # 核心服務、模型、Guards、Interceptors
        +-- features/                      # 功能模組 (Auth、Chat、Dashboard、Games、Prediction)
        +-- shared/                        # 共用元件 (Navbar、Sidebar、DataTable)
```

## 快速開始

### 前置需求

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 18+](https://nodejs.org/)
- [SQL Server](https://www.microsoft.com/sql-server/sql-server-downloads)
- [Redis](https://redis.io/download)
- [Qdrant](https://qdrant.tech/documentation/quick-start/) (向量資料庫)
- [Ollama](https://ollama.ai/) (本地 LLM)

### 安裝步驟

1. **複製專案**
```bash
git clone <repository-url>
cd AiBoardgameDashboard
```

2. **設定資料庫連線**
```bash
# 編輯 appsettings.Development.json
# 確認 SQL Server、Redis、Qdrant、Ollama 連線資訊
```

3. **執行資料庫遷移**
```bash
cd BoardGameAiDashboard
dotnet ef database update \
  --project BoardGameAiDashboard.Infrastructure \
  --startup-project BoardGameAiDashboard.Api
```

4. **啟動後端 API**
```bash
dotnet run --project BoardGameAiDashboard.Api
# API 運行於 http://localhost:5001
# Swagger UI: http://localhost:5001/swagger
```

5. **啟動前端**
```bash
cd DashboardFrontend
npm install
npm start
# 前端運行於 http://localhost:4200
```

## API 端點

### 認證 (Auth)

| 方法 | 端點 | 描述 |
|------|------|------|
| POST | `/api/auth/register` | 註冊新用戶 |
| POST | `/api/auth/login` | 登入並取得 JWT Token |
| POST | `/api/auth/refresh` | 刷新 Access Token |
| GET | `/api/auth/me` | 取得當前用戶資訊 |

### 遊戲 (Games)

| 方法 | 端點 | 描述 |
|------|------|------|
| GET | `/api/games` | 取得遊戲列表 (分頁) |
| GET | `/api/games/{id}` | 取得單一遊戲 |
| POST | `/api/games` | 建立新遊戲 |
| PUT | `/api/games/{id}` | 更新遊戲 |
| DELETE | `/api/games/{id}` | 軟刪除遊戲 |

### AI 聊天 (Chat)

| 方法 | 端點 | 描述 |
|------|------|------|
| POST | `/api/chat/send` | 發送訊息並取得 AI 回覆 |
| GET | `/api/chat/conversation/{id}` | 取得對話歷史 |
| GET | `/api/chat/history/{userId}` | 取得用戶所有對話 |

### 預測 (Predictions)

| 方法 | 端點 | 描述 |
|------|------|------|
| POST | `/api/predictions/predict` | 預測勝率 |
| GET | `/api/predictions/analyze-level` | 分析不同等级的勝率 |
| GET | `/api/predictions/status` | 取得 ML 模型狀態 |
| POST | `/api/predictions/reload-model` | 重新載入 ONNX 模型 |
| GET | `/api/predictions/export` | 匯出訓練資料 (CSV) |

### 遊戲規則 (GameRules)

| 方法 | 端點 | 描述 |
|------|------|------|
| POST | `/api/gamerules/ingest` | 上傳並索引遊戲規則 PDF |

## 開發工具

### 程式碼格式化
```bash
dotnet format
```

### 執行測試
```bash
dotnet test                              # 所有測試
dotnet test --filter "FullyQualifiedName~SoftDeleteTests"  # 特定測試
```

### 建置
```bash
dotnet build --no-restore /p:TreatWarningsAsErrors=true  # 嚴格建置
```

## 重要約定

1. **僅使用 async/await**：禁止使用 `.Result` 或 `.Wait()`
2. **薄 Controller**：HTTP 處理放在 Controller，業務邏輯放在 CQRS Handlers
3. **軟刪除**：所有實體都有 `IsDeleted` 欄位，查詢自動過濾已刪除資料
4. **RAG 流程**：必須先分塊 -> 嵌入 -> 檢索 -> 回答，嚴禁直接傳送整份 PDF 給 LLM
5. **Metadata 過濾**：Qdrant 搜尋時支援依 `game_id` 進行 Metadata 過濾

## 相關文檔

- [CLAUDE.md](CLAUDE.md) - Claude Code 開發指南
- [skills.md](skills.md) - 可用技能列表
- [architecture.md](architecture.md) - 架構詳細說明

## 授權

此專案僅供學習與研究使用。
