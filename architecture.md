# 🎲 BoardGame AI Dashboard — Architecture Document

> **Version**: 1.0 | **Last Updated**: 2026-07-20  
> **Stack**: .NET 8 + Angular 19 + SQL Server + Qdrant + Redis + Azure OpenAI  
> **Pattern**: Clean Architecture + CQRS (MediatR) + RAG + ML.NET

---

## 1. Project Overview

### 🎯 Vision
A **RAG-powered board game dashboard** that allows users to ask board game rule questions in natural language (including Cantonese), view AI-driven win-rate predictions, and explore game analytics — all through a modern, responsive UI.

### 🏗 Core Features

| Feature | Description | Key Technology |
|---------|-------------|----------------|
| 🤖 AI Chat (RAG) | Ask board game rules in natural language, get answers with source citations | Semantic Kernel + Qdrant + Azure OpenAI |
| 📊 Win Rate Prediction | Predict win probability based on player count, character, deck composition | ML.NET + ONNX |
| 🔍 Vector Search | Semantic search across rulebook chunks | Qdrant + Embeddings |
| 📈 Game Analytics | Match history analysis, win-rate trends, character meta | SQL Server + Chart.js |
| ⚡ Real-time Updates | Live chat streaming, live prediction updates | SignalR |
| 👤 User Management | Registration, login, JWT auth with refresh tokens | ASP.NET Core Identity + JWT |

### 👥 Target Users
- Board game enthusiasts who want quick rule lookups
- Competitive players seeking meta analysis
- Game store owners tracking match statistics

---

## 2. System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        CLIENT LAYER                             │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │  Angular 19 SPA (Tailwind CSS + ng2-charts + SweetAlert2)│  │
│  └──────────────────────┬────────────────────────────────────┘  │
│                         │ HTTP / WebSocket (SignalR)             │
└─────────────────────────┼───────────────────────────────────────┘
                          │
┌─────────────────────────┼───────────────────────────────────────┐
│                     API LAYER (.NET 8)                           │
│  ┌──────────┐  ┌──────────────┐  ┌────────────┐  ┌──────────┐  │
│  │Controllers│  │SignalR Hub   │  │Middleware  │  │Filters   │  │
│  └─────┬────┘  └──────┬───────┘  └─────┬──────┘  └────┬─────┘  │
│        └───────────────┴────────────────┴──────────────┘        │
│                              │                                   │
│  ┌───────────────────────────▼───────────────────────────────┐  │
│  │              APPLICATION LAYER (MediatR CQRS)             │  │
│  │  ┌─────────────┐  ┌──────────────┐  ┌────────────────┐   │  │
│  │  │Commands/     │  │Queries/      │  │Validators      │   │  │
│  │  │Handlers      │  │Handlers      │  │(FluentValidation│   │  │
│  │  └──────┬──────┘  └──────┬───────┘  └────────────────┘   │  │
│  └─────────┼────────────────┼────────────────────────────────┘  │
│            └────────────────┘                                   │
│                              │                                   │
│  ┌───────────────────────────▼───────────────────────────────┐  │
│  │              INFRASTRUCTURE LAYER                          │  │
│  │  ┌─────────┐ ┌────────┐ ┌───────┐ ┌───────┐ ┌────────┐  │  │
│  │  │EF Core  │ │Qdrant  │ │ML.NET │ │Redis  │ │Hangfire│  │  │
│  │  │(SQL Svr)│ │Vector  │ │Predict│ │Cache  │ │Jobs    │  │  │
│  │  └─────────┘ └────────┘ └───────┘ └───────┘ └────────┘  │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                          │
┌─────────────────────────┼───────────────────────────────────────┐
│                   EXTERNAL SERVICES                             │
│  ┌──────────────┐  ┌──────────────┐  ┌─────────────────────┐   │
│  │Azure OpenAI  │  │Azure SQL     │  │Qdrant Cloud/Local   │   │
│  │(Embeddings + │  │Database      │  │(Vector Database)    │   │
│  │ Chat)        │  │              │  │                     │   │
│  └──────────────┘  └──────────────┘  └─────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

---

## 3. Solution Structure

```
BoardGameAiDashboard/                        ← Solution Root
├── BoardGameAiDashboard.sln                 ← Solution File
│
├── BoardGameAiDashboard.Domain/             ← 🟢 Domain Layer (Core)
│   ├── BoardGameAiDashboard.Domain.csproj
│   ├── Entities/                            ← Domain Entities
│   │   ├── Game.cs                          ← Board game master table
│   │   ├── GameCard.cs                      ← Cards with JSON properties
│   │   ├── GameCharacter.cs                 ← Characters with JSON properties
│   │   ├── GameRuleChunk.cs                 ← Rule text chunks (links to Qdrant)
│   │   └── MatchHistory.cs                  ← Match records with JSON features
│   ├── Enums/                               ← Domain enumerations
│   ├── ValueObjects/                        ← Value objects
│   └── Events/                              ← Domain events (future)
│
├── BoardGameAiDashboard.Application/        ← 🟡 Application Layer (Use Cases)
│   ├── BoardGameAiDashboard.Application.csproj
│   ├── IApplicationAssemblyMarker.cs        ← MediatR assembly scan marker
│   ├── Common/
│   │   ├── Interfaces/                      ← Repository & service interfaces
│   │   │   ├── IGenericRepository.cs
│   │   │   ├── IUnitOfWork.cs
│   │   │   └── IDateTimeProvider.cs
│   │   ├── Behaviors/                       ← MediatR pipeline behaviors
│   │   │   ├── ValidationBehavior.cs
│   │   │   └── LoggingBehavior.cs
│   │   ├── Models/                          ← Shared models
│   │   │   ├── ApiResult.cs
│   │   │   └── PaginatedList.cs
│   │   └── Exceptions/                      ← Custom exceptions
│   │       └── ValidationException.cs
│   └── Features/                            ← CQRS Features (by domain)
│       ├── Auth/
│       │   ├── Commands/
│       │   │   ├── RegisterUser/
│       │   │   └── LoginUser/
│       │   └── Queries/
│       │       └── GetCurrentUser/
│       ├── Games/
│       │   ├── Commands/
│       │   │   ├── CreateGame/
│       │   │   ├── UpdateGame/
│       │   │   └── DeleteGame/
│       │   └── Queries/
│       │       ├── GetGames/
│       │       └── GetGameById/
│       ├── Chat/
│       │   ├── Commands/
│       │   │   └── SendChatMessage/
│       │   └── Queries/
│       │       └── GetChatHistory/
│       ├── Predictions/
│       │   ├── Queries/
│       │   │   ├── GetWinRate/
│       │   │   └── GetChurnPrediction/
│       │   └── Commands/
│       │       └── TrainModel/
│       └── MatchHistory/
│           ├── Commands/
│           │   └── RecordMatch/
│           └── Queries/
│               └── GetMatchHistory/
│
├── BoardGameAiDashboard.Infrastructure/    ← 🔵 Infrastructure Layer
│   ├── BoardGameAiDashboard.Infrastructure.csproj
│   ├── Persistence/
│   │   ├── ApplicationDbContext.cs           ← EF Core DbContext
│   │   ├── Configurations/                  ← Entity Fluent API configs
│   │   │   ├── GameConfiguration.cs
│   │   │   ├── GameCardConfiguration.cs
│   │   │   ├── GameCharacterConfiguration.cs
│   │   │   ├── GameRuleChunkConfiguration.cs
│   │   │   └── MatchHistoryConfiguration.cs
│   │   └── Migrations/                      ← EF Core Migrations
│   ├── Repositories/                        ← Repository implementations
│   │   ├── GenericRepository.cs
│   │   └── UnitOfWork.cs
│   ├── Services/                            ← Infrastructure services
│   │   ├── VectorSearch/
│   │   │   └── QdrantVectorSearchService.cs ← Vector DB operations
│   │   ├── Rag/
│   │   │   ├── RagOrchestratorService.cs    ← RAG pipeline orchestrator
│   │   │   ├── EmbeddingService.cs          ← Embedding generation
│   │   │   └── RuleIngestionService.cs      ← PDF → chunks → embed → store
│   │   ├── Ai/
│   │   │   └── SemanticKernelService.cs     ← LLM chat completions
│   │   ├── Ml/
│   │   │   ├── WinRatePredictionService.cs  ← ML.NET win prediction
│   │   │   ├── ChurnPredictionService.cs    ← ML.NET churn prediction
│   │   │   └── FeatureEngineering.cs        ← Data → ML features
│   │   ├── Cache/
│   │   │   └── RedisCacheService.cs         ← Distributed caching
│   │   ├── Auth/
│   │   │   └── JwtTokenService.cs           ← JWT generation/validation
│   │   └── Email/
│   │       └── EmailService.cs              ← Email notifications
│   ├── BackgroundJobs/
│   │   ├── ModelTrainingJob.cs              ← Hangfire: retrain models
│   │   └── CacheRefreshJob.cs              ← Hangfire: refresh caches
│   └── DependencyInjection.cs               ← Infrastructure DI registration
│
├── BoardGameAiDashboard.Api/               ← 🔴 API Layer (Entry Point)
│   ├── BoardGameAiDashboard.Api.csproj
│   ├── Program.cs                           ← App builder + DI setup
│   ├── Controllers/                         ← API Controllers
│   │   ├── AuthController.cs
│   │   ├── GamesController.cs
│   │   ├── ChatController.cs
│   │   ├── PredictionsController.cs
│   │   └── MatchHistoryController.cs
│   ├── Hubs/
│   │   └── ChatHub.cs                       ← SignalR Hub for live chat
│   ├── Middleware/
│   │   ├── ExceptionHandlingMiddleware.cs   ← Global exception handler
│   │   └── RequestLoggingMiddleware.cs      ← Request/response logging
│   ├── Filters/
│   │   └── ApiResultFilter.cs               ← Uniform API response wrapper
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   └── Properties/
│       └── launchSettings.json
│
└── BoardGameAiDashboard.Tests/             ← 🟣 Tests (Future)
    ├── UnitTests/
    │   └── Features/
    └── E2ETests/
        └── Pages/
```

---

## 4. NuGet Packages by Layer

### Domain Layer (Minimal Dependencies)
| Package | Purpose |
|---------|---------|
| *(none — pure domain)* | Entities, Enums, ValueObjects have zero dependencies |

### Application Layer
| Package | Purpose |
|---------|---------|
| `MediatR` 14.x | CQRS Command/Query dispatching |
| `FluentValidation` 11.x | DTO validation |
| `AutoMapper` 13.x | Entity ↔ DTO mapping (optional) |

### Infrastructure Layer
| Package | Purpose |
|---------|---------|
| `Microsoft.EntityFrameworkCore.SqlServer` 8.0 | SQL Server ORM |
| `Microsoft.EntityFrameworkCore.Tools` 8.0 | EF Core migrations |
| `Microsoft.SemanticKernel` 1.x | LLM orchestration (Azure OpenAI) |
| `Qdrant.Client` 1.x | Vector database operations |
| `Microsoft.ML` 5.0 | ML.NET prediction pipelines |
| `Microsoft.ML.OnnxRuntime` | ONNX model inference |
| `Microsoft.Extensions.Caching.StackExchangeRedis` | Redis distributed cache |
| `Hangfire.Core` | Background job scheduling |
| `Hangfire.SqlServer` | Hangfire storage |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | JWT auth |

### API Layer
| Package | Purpose |
|---------|---------|
| `Swashbuckle.AspNetCore` | Swagger/OpenAPI |
| `Microsoft.AspNetCore.SignalR` | Real-time hubs |
| `Serilog.AspNetCore` | Structured logging |
| `Serilog.Sinks.Seq` | Centralized log aggregation |

---

## 5. Domain Entities & Relationships

### Entity Relationship Diagram

```
┌──────────────────────┐
│        Game          │
├──────────────────────┤
│ Id (PK, Guid)        │
│ Name (string)        │
│ Description (string) │
│ MinPlayers (int)     │
│ MaxPlayers (int)     │
└──────────┬───────────┘
           │ 1:N
     ┌─────┼──────────────────┬──────────────────┐
     │     │                  │                  │
     ▼     ▼                  ▼                  ▼
┌──────────────┐  ┌───────────────┐  ┌───────────────┐  ┌────────────────┐
│GameRuleChunk │  │GameCharacter  │  │  GameCard     │  │ MatchHistory   │
├──────────────┤  ├───────────────┤  ├───────────────┤  ├────────────────┤
│Id (PK, Guid) │  │Id (PK, Guid)  │  │Id (PK, Guid)  │  │Id (PK, Guid)   │
│GameId (FK)   │  │GameId (FK)    │  │GameId (FK)    │  │GameId (FK)     │
│Content (text)│  │CodeName       │  │CodeName       │  │PlayerCount     │
│SectionTitle  │  │Name           │  │Name           │  │IsWinner        │
│QdrantPointId │  │SkillDesc      │  │Description    │  │PlayedAt        │
│              │  │CustomProps    │  │CardProperties │  │GameFeatures    │
│              │  │  (JSON)       │  │  (JSON)       │  │  (JSON)        │
└──────────────┘  └───────────────┘  └───────────────┘  └────────────────┘
        │                                                     │
        │ QdrantPointId                              GameFeatures
        ▼                                                     ▼
  ┌──────────┐                                        ┌──────────┐
  │ Qdrant   │                                        │  ML.NET  │
  │ Vectors  │                                        │ Pipeline │
  └──────────┘                                        └──────────┘
```

### JSON Columns Design

The project uses **SQL Server JSON columns** for game-specific flexible data:

| Entity | JSON Column | Purpose | Example |
|--------|-------------|---------|---------|
| `GameCharacter` | `CustomProperties` | Character-specific stats | `{"health": "4", "attack": "3"}` |
| `GameCard` | `CardProperties` | Card-specific attributes | `{"cost": "2", "type": "weapon"}` |
| `MatchHistory` | `GameFeatures` | ML features per match | `{"faction": "red", "rounds": "8"}` |

**EF Core JSON Conversion** is handled via `ValueConverter` and `ValueComparer` in `ApplicationDbContext`.

---

## 6. Layer Responsibilities

### 🟢 Domain Layer — `BoardGameAiDashboard.Domain`
> **Zero external dependencies.** The heart of the business.

- **Entities**: Pure business objects with private setters
- **Enums**: Domain-specific enumerations
- **ValueObjects**: Immutable value types
- **Events**: Domain events for future extensibility

**Rules:**
- ❌ No references to Infrastructure, Application, or Api
- ❌ No NuGet packages
- ✅ Only `System.*` dependencies

### 🟡 Application Layer — `BoardGameAiDashboard.Application`
> **Use cases and business workflows.** Orchestrates domain objects.

- **CQRS Features**: Commands (write) + Queries (read) per domain feature
- **DTOs**: Data Transfer Objects for API contracts
- **Interfaces**: `IGenericRepository`, `IUnitOfWork`, service contracts
- **Behaviors**: MediatR pipeline (validation, logging, performance)
- **FluentValidation**: Input validation rules

**Rules:**
- ❌ No references to Infrastructure or Api
- ✅ References Domain only
- ✅ Defines interfaces that Infrastructure implements

### 🔵 Infrastructure Layer — `BoardGameAiDashboard.Infrastructure`
> **Technical implementation.** Implements Application interfaces.

- **Persistence**: EF Core DbContext, configurations, migrations
- **Repositories**: Generic Repository + Unit of Work
- **AI Services**: Semantic Kernel, Qdrant, Embeddings
- **ML Services**: ML.NET prediction pipelines
- **Caching**: Redis distributed cache
- **Background Jobs**: Hangfire scheduled tasks
- **Auth**: JWT token generation

**Rules:**
- ✅ References Domain and Application
- ✅ Implements interfaces defined in Application
- ❌ No references to Api

### 🔴 API Layer — `BoardGameAiDashboard.Api`
> **Entry point.** HTTP endpoints, middleware, DI wiring.

- **Controllers**: REST API endpoints
- **Hubs**: SignalR real-time communication
- **Middleware**: Exception handling, request logging
- **Program.cs**: DI container setup, app pipeline

**Rules:**
- ✅ References all layers
- ❌ No business logic — delegate to Application layer

---

## 7. Key Design Patterns

### 7.1 CQRS via MediatR

```
HTTP Request → Controller → MediatR.Send() → Handler → Domain/Infrastructure → Response
```

**Example:**
```csharp
// Command (Write)
public record CreateGameCommand(string Name, string Description, int Min, int Max) : IRequest<ApiResult<Guid>>;

// Handler
public class CreateGameHandler : IRequestHandler<CreateGameCommand, ApiResult<Guid>>
{
    public async Task<ApiResult<Guid>> Handle(CreateGameCommand request, CancellationToken ct)
    {
        var game = new Game(request.Name, request.Description, request.Min, request.Max);
        await _repository.AddAsync(game, ct);
        return ApiResult<Guid>.Success(game.Id);
    }
}
```

### 7.2 Generic Repository Pattern

```csharp
public interface IGenericRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task<T> AddAsync(T entity, CancellationToken ct = default);
    Task UpdateAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(T entity, CancellationToken ct = default);
    // Soft delete: all queries automatically filter !IsDeleted
}
```

### 7.3 RAG Pipeline

```
User Question (Cantonese/English)
        │
        ▼
┌─────────────────┐
│ Embed Question   │  Azure OpenAI Embedding API
│ (text → vector)  │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Qdrant Search    │  Hybrid: vector similarity + metadata filter
│ (top-K results)  │  Filter by: GameId, SectionTitle
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Build Context    │  Combine retrieved chunks into prompt
│ (chunks → text)  │  Max tokens: ~3000
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ LLM Completion   │  Semantic Kernel → Azure OpenAI Chat
│ (context+query)  │  Language: Cantonese/Chinese/English
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Response +       │  Answer with source citations
│ Source Citations │  (SectionTitle + GameName)
└─────────────────┘
```

### 7.4 ML.NET Prediction Pipeline

```
Match History Data (SQL Server)
        │
        ▼
┌─────────────────┐
│ Feature           │  Extract: playerCount, character, deck,
│ Engineering       │  faction, rounds, winStreak, etc.
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Train/Load Model  │  ML.NET Binary Classification (Win/Loss)
│ (ML.NET)          │  or Regression (Win Probability)
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Predict           │  Input: match features → Output: win %
│ (Real-time)       │  Cache result in Redis (TTL: 5 min)
└─────────────────┘
```

### 7.5 API Response Wrapper

```csharp
// Success
{
    "success": true,
    "data": { ... },
    "message": "Game created successfully",
    "timestamp": "2026-07-20T12:00:00Z"
}

// Error (RFC 7807 ProblemDetails)
{
    "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
    "title": "Validation Error",
    "status": 400,
    "errors": {
        "Name": ["Name is required"]
    }
}
```

---

## 8. Data Flow Diagrams

### 8.1 RAG Chat Flow (Main Feature)

```
┌────────┐     ┌────────────┐     ┌─────────────┐     ┌──────────┐
│ Angular │────▶│ Controller │────▶│ MediatR     │────▶│ RAG      │
│ Chat UI │     │            │     │ Handler     │     │ Service  │
└────────┘     └────────────┘     └─────────────┘     └────┬─────┘
                                                            │
                     ┌──────────────────────────────────────┘
                     │
          ┌──────────┼──────────┐
          ▼          ▼          ▼
   ┌──────────┐ ┌────────┐ ┌─────────┐
   │ Azure    │ │ Qdrant │ │ Redis   │
   │ OpenAI   │ │ Search │ │ Cache   │
   │ (Embed + │ │        │ │         │
   │  Chat)   │ │        │ │         │
   └──────────┘ └────────┘ └─────────┘
```

### 8.2 Rule Ingestion Flow

```
┌──────────┐     ┌────────────┐     ┌────────────┐     ┌──────────┐
│ PDF      │────▶│ Chunking   │────▶│ Embedding  │────▶│ Qdrant   │
│ Rulebook │     │ Service    │     │ Service    │     │ Store    │
└──────────┘     │ (semantic) │     │ (Azure AI) │     └──────────┘
                 └────────────┘     └────────────┘          │
                                                            │
                     ┌──────────────────────────────────────┘
                     ▼
              ┌────────────┐
              │ EF Core    │
              │ Store      │
              │ QdrantPoint│
              │ Id in      │
              │ GameRuleChunk│
              └────────────┘
```

### 8.3 Win Rate Prediction Flow

```
┌──────────┐     ┌────────────┐     ┌────────────┐     ┌──────────┐
│ User     │────▶│ Controller │────▶│ ML.NET     │────▶│ Redis    │
│ Request  │     │            │     │ Prediction │     │ Cache    │
│ (gameId, │     │            │     │ Service    │     │ (5 min)  │
│  params) │     │            │     │            │     └──────────┘
└──────────┘     └────────────┘     └────────────┘
                                        │
                                        ▼
                                 ┌────────────┐
                                 │ SQL Server │
                                 │ MatchHistory│
                                 │ (training  │
                                 │  data)     │
                                 └────────────┘
```

---

## 9. API Conventions

### 9.1 RESTful Endpoints

| Method | Endpoint | Description | CQRS |
|--------|----------|-------------|------|
| `POST` | `/api/auth/register` | Register new user | Command |
| `POST` | `/api/auth/login` | Login & get JWT | Command |
| `GET` | `/api/games` | List all games | Query |
| `GET` | `/api/games/{id}` | Get game detail | Query |
| `POST` | `/api/games` | Create new game | Command |
| `PUT` | `/api/games/{id}` | Update game | Command |
| `DELETE` | `/api/games/{id}` | Soft delete game | Command |
| `POST` | `/api/chat/ask` | Ask RAG question | Command |
| `GET` | `/api/chat/history` | Get chat history | Query |
| `GET` | `/api/predictions/winrate/{gameId}` | Win rate prediction | Query |
| `GET` | `/api/predictions/churn/{userId}` | Churn prediction | Query |
| `POST` | `/api/matches` | Record match result | Command |
| `GET` | `/api/matches/{gameId}` | Match history | Query |

### 9.2 Pagination Pattern

```csharp
// Request
GET /api/games?page=1&pageSize=20&search=catan

// Response
{
    "success": true,
    "data": {
        "items": [...],
        "totalCount": 45,
        "pageNumber": 1,
        "pageSize": 20,
        "totalPages": 3
    }
}
```

### 9.3 Soft Delete Filtering

All queries automatically filter `!IsDeleted`:
```csharp
// In GenericRepository
public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct)
{
    return await _context.Set<T>()
        .Where(e => EF.Property<bool>(e, "IsDeleted") == false)
        .ToListAsync(ct);
}
```

---

## 10. Frontend Architecture (Planned)

```
┌─────────────────────────────────────────────────┐
│                  Angular 19 SPA                  │
├─────────────────────────────────────────────────┤
│  Core Module                                    │
│  ├── Auth Guard / Interceptor (JWT)             │
│  ├── HTTP Service (API calls)                   │
│  └── SignalR Service (real-time)                │
├─────────────────────────────────────────────────┤
│  Feature Modules                                │
│  ├── Dashboard (overview stats, charts)         │
│  ├── Games (CRUD, game detail)                  │
│  ├── Chat (RAG chatbot interface)               │
│  ├── Predictions (win rate, churn)              │
│  └── Match History (record & view matches)      │
├─────────────────────────────────────────────────┤
│  Shared Components                              │
│  ├── Navbar, Sidebar, Footer                    │
│  ├── Data Table, Pagination                     │
│  ├── Loading Spinner, Error Alert               │
│  └── Confirmation Dialog (SweetAlert2)          │
├─────────────────────────────────────────────────┤
│  Styling: Tailwind CSS                          │
│  Charts: Chart.js + ng2-charts                  │
│  Forms: Reactive Forms + Validators             │
└─────────────────────────────────────────────────┘
```

### Angular Services → API Mapping

| Angular Service | API Endpoints | Purpose |
|----------------|---------------|---------|
| `AuthService` | `/api/auth/*` | Login, Register, Token refresh |
| `GameService` | `/api/games/*` | Game CRUD |
| `ChatService` | `/api/chat/*` | RAG chat + SignalR stream |
| `PredictionService` | `/api/predictions/*` | Win rate, churn |
| `MatchService` | `/api/matches/*` | Match history |

---

## 11. Database Schema

### SQL Server Tables

```sql
-- Games (主表)
CREATE TABLE Games (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    Description NVARCHAR(1000),
    MinPlayers INT NOT NULL,
    MaxPlayers INT NOT NULL,
    IsDeleted BIT DEFAULT 0,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2
);

-- GameRuleChunks (RAG 知識庫)
CREATE TABLE GameRuleChunks (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    GameId UNIQUEIDENTIFIER NOT NULL,
    Content NVARCHAR(MAX) NOT NULL,
    SectionTitle NVARCHAR(200),
    QdrantPointId NVARCHAR(50) NOT NULL,
    IsDeleted BIT DEFAULT 0,
    FOREIGN KEY (GameId) REFERENCES Games(Id) ON DELETE CASCADE
);

-- GameCharacters (角色)
CREATE TABLE GameCharacters (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    GameId UNIQUEIDENTIFIER NOT NULL,
    CodeName NVARCHAR(100) NOT NULL,
    Name NVARCHAR(100) NOT NULL,
    SkillDescription NVARCHAR(MAX),
    CustomProperties NVARCHAR(MAX), -- JSON
    IsDeleted BIT DEFAULT 0,
    FOREIGN KEY (GameId) REFERENCES Games(Id) ON DELETE CASCADE
);

-- GameCards (卡牌)
CREATE TABLE GameCards (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    GameId UNIQUEIDENTIFIER NOT NULL,
    CodeName NVARCHAR(100) NOT NULL,
    Name NVARCHAR(100) NOT NULL,
    Description NVARCHAR(MAX),
    CardProperties NVARCHAR(MAX), -- JSON
    IsDeleted BIT DEFAULT 0,
    FOREIGN KEY (GameId) REFERENCES Games(Id) ON DELETE CASCADE
);

-- MatchHistories (對戰紀錄)
CREATE TABLE MatchHistories (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    GameId UNIQUEIDENTIFIER NOT NULL,
    PlayerCount INT NOT NULL,
    IsWinner BIT NOT NULL,
    PlayedAt DATETIME2 NOT NULL,
    GameFeatures NVARCHAR(MAX), -- JSON
    IsDeleted BIT DEFAULT 0,
    FOREIGN KEY (GameId) REFERENCES Games(Id) ON DELETE CASCADE
);
```

### Qdrant Collection

```
Collection: "game_rules"
Vector Size: 1536 (Azure OpenAI text-embedding-3-small)
Distance: Cosine

Payload Fields:
  - game_id (UUID)        ← FK to Games table
  - game_name (string)    ← Denormalized for display
  - section_title (string) ← Rule section name
  - content (string)       ← Original text chunk
  - chunk_index (int)      ← Order within document
```

---

## 12. Security Architecture

### JWT Authentication Flow

```
┌────────┐     ┌────────────┐     ┌────────────┐
│ Client │────▶│ /api/auth/ │────▶│ Validate   │
│        │     │ login      │     │ Credentials│
└────────┘     └────────────┘     └─────┬──────┘
                                        │
                   ┌────────────────────┘
                   ▼
            ┌────────────┐
            │ Return:    │
            │ - Access   │  (15 min TTL)
            │   Token    │
            │ - Refresh  │  (7 day TTL)
            │   Token    │
            └────────────┘

Subsequent Requests:
  Header: Authorization: Bearer <access_token>
  
Token Refresh:
  POST /api/auth/refresh
  Body: { refreshToken: "..." }
  → New access + refresh token pair
```

### Security Rules
- Access Token: **15 minutes** TTL
- Refresh Token: **7 days** TTL, single-use (rotation)
- All API endpoints require `[Authorize]` except `/api/auth/*`
- Soft Delete ensures data is never truly lost
- SQL injection prevented by parameterized EF Core queries

---

## 13. Caching Strategy

### Redis Cache Layout

| Key Pattern | TTL | Purpose |
|-------------|-----|---------|
| `game:{id}` | 10 min | Game detail cache |
| `winrate:{gameId}:{params}` | 5 min | ML prediction cache |
| `rag:{hash(query+gameId)}` | 30 min | RAG response cache |
| `leaderboard:{gameId}` | 15 min | Win-rate leaderboard |
| `user:{userId}:token` | 7 days | Refresh token store |

### Cache-Aside Pattern
```csharp
public async Task<Game?> GetGameAsync(Guid id)
{
    // 1. Check Redis
    var cached = await _redis.GetStringAsync($"game:{id}");
    if (cached != null)
        return JsonSerializer.Deserialize<Game>(cached);
    
    // 2. Query SQL Server
    var game = await _repository.GetByIdAsync(id);
    
    // 3. Store in Redis
    if (game != null)
        await _redis.SetStringAsync($"game:{id}", 
            JsonSerializer.Serialize(game),
            TimeSpan.FromMinutes(10));
    
    return game;
}
```

---

## 14. Deployment Architecture

### Local Development (Docker Compose)

```
┌─────────────────────────────────────────┐
│           Docker Compose                │
│  ┌───────────┐  ┌───────────┐          │
│  │ SQL Server│  │  Qdrant   │          │
│  │  (1433)   │  │  (6333)   │          │
│  └───────────┘  └───────────┘          │
│  ┌───────────┐  ┌───────────┐          │
│  │   Redis   │  │   Seq     │          │
│  │  (6379)   │  │  (5341)   │          │
│  └───────────┘  └───────────┘          │
└─────────────────────────────────────────┘
```

### Azure Cloud Deployment (Future)

```
┌─────────────────────────────────────────────┐
│              Azure Cloud                     │
│  ┌──────────────┐  ┌──────────────────┐    │
│  │ Azure App    │  │ Azure SQL        │    │
│  │ Service      │  │ Database         │    │
│  │ (.NET 8)     │  │                  │    │
│  └──────────────┘  └──────────────────┘    │
│  ┌──────────────┐  ┌──────────────────┐    │
│  │ Azure OpenAI │  │ Qdrant Cloud     │    │
│  │ (GPT-4 +     │  │ (Vector DB)      │    │
│  │  Embeddings) │  │                  │    │
│  └──────────────┘  └──────────────────┘    │
│  ┌──────────────┐  ┌──────────────────┐    │
│  │ Azure Redis  │  │ Azure Blob       │    │
│  │ Cache        │  │ Storage (PDFs)   │    │
│  └──────────────┘  └──────────────────┘    │
│  ┌──────────────┐  ┌──────────────────┐    │
│  │ Azure SignalR│  │ Key Vault        │    │
│  │ Service      │  │ (Secrets)        │    │
│  └──────────────┘  └──────────────────┘    │
└─────────────────────────────────────────────┘
```

---

## 15. Development Phases (Roadmap)

| Phase | Focus | Status |
|-------|-------|--------|
| **Phase 0** | Project Setup & Structure | 🔄 In Progress |
| **Phase 1** | Domain Entities & Database | ⏳ Pending |
| **Phase 2** | Application Layer (CQRS, Validation) | ⏳ Pending |
| **Phase 3** | Repository & Unit of Work | ⏳ Pending |
| **Phase 4** | API Controllers & Middleware | ⏳ Pending |
| **Phase 5** | Auth (JWT + Refresh Token) | ⏳ Pending |
| **Phase 6** | RAG Pipeline (Qdrant + Semantic Kernel) | ⏳ Pending |
| **Phase 7** | ML.NET Predictions | ⏳ Pending |
| **Phase 8** | Redis Caching | ⏳ Pending |
| **Phase 9** | Hangfire Background Jobs | ⏳ Pending |
| **Phase 10** | SignalR Real-time | ⏳ Pending |
| **Phase 11** | Angular Frontend | ⏳ Pending |
| **Phase 12** | Testing (xUnit + Playwright) | ⏳ Pending |
| **Phase 13** | Azure Deployment | ⏳ Pending |

---

## 16. Coding Conventions Summary

| Rule | Standard |
|------|----------|
| Architecture | Clean Architecture (4-layer) |
| CQRS | MediatR Commands + Queries |
| Validation | FluentValidation |
| DB Access | EF Core + Generic Repository |
| Soft Delete | `!IsDeleted` filter on all queries |
| Async | Always async/await, never `.Result` |
| API Response | `ApiResult<T>` wrapper |
| Error Handling | RFC 7807 ProblemDetails |
| Logging | Serilog structured logging |
| Testing | xUnit + Moq (unit), Playwright (E2E) |
| Frontend | Angular 19 + Tailwind + Signals |

---

*This document serves as the single source of truth for the BoardGame AI Dashboard architecture. All implementation decisions should align with the patterns and conventions described here.*
