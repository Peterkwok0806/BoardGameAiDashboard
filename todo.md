# 🎲 BoardGame AI Dashboard — Development TODO

> **Reference**: `architecture.md` — Single source of truth for architecture decisions.  
> **Stack**: .NET 8 + Angular 19 + SQL Server + Qdrant + Redis + Azure OpenAI  
> **Pattern**: Clean Architecture + CQRS (MediatR) + RAG + ML.NET

---

## Phase 0: Project Setup — ✅ 100% Done

- [x] Verify solution structure matches `architecture.md` Section 3
  - [x] `BoardGameAiDashboard.Domain` ✅
  - [x] `BoardGameAiDashboard.Application` ✅
  - [x] `BoardGameAiDashboard.Infrastructure` ✅
  - [x] `BoardGameAiDashboard.Api` ✅
- [x] `BoardGameAiDashboard.Tests` ✅ (xUnit + InMemory provider)
- [x] Add all NuGet packages per layer (see `architecture.md` Section 4) ✅
  - Domain: *(none — pure domain)* ✅
  - Application: MediatR ✅, FluentValidation ✅, AutoMapper ✅
  - Infrastructure: EF Core ✅, SemanticKernel ✅, Qdrant.Client ✅, ML.NET ✅, Redis ✅, Hangfire ✅, JwtBearer ✅
  - Api: Swashbuckle ✅, Serilog ✅, Hangfire.AspNetCore ✅
- [x] Configure `Program.cs` with DI ✅
  - [x] CORS configuration ✅ (AllowAngular policy)
  - [x] Serilog configuration ✅ (ReadFrom.Configuration + Console + Seq)
  - [x] Swagger JWT Bearer setup ✅ (security definition + requirement)
  - [x] Redis, Hangfire registration ✅ (via Infrastructure.DependencyInjection)
- [x] Setup `appsettings.json` ✅
  - [x] Azure OpenAI config ✅
  - [x] Redis config ✅
  - [x] Hangfire config ✅
  - [x] JWT Secret/Issuer/Audience config ✅
- [x] Create `DependencyInjection.cs` in Infrastructure layer for service registration ✅

---

## Phase 1: Domain Entities — ✅ 100% Done

- [x] Create base entity class (`Domain/Common/BaseEntity.cs`) with `Id`, `IsDeleted`, `CreatedAt`, `UpdatedAt` ✅
- [x] Refactor `Game` to inherit `BaseEntity` ✅
- [x] Refactor `GameCard` to inherit `BaseEntity` ✅
- [x] Refactor `GameCharacter` to inherit `BaseEntity` ✅
- [x] Refactor `GameRuleChunk` to inherit `BaseEntity` ✅
- [x] Refactor `MatchHistory` to inherit `BaseEntity` ✅
- [x] Create domain Enums — not needed (special results stored in GameFeatures JSON) ✅
- [x] Create domain ValueObjects — not needed for this phase (deferred) ✅
- [x] Ensure all entities have zero external dependencies (only `System.*`) ✅

> ✅ All entities now inherit `BaseEntity`. Enums/ValueObjects deferred — not needed currently.

---

## Phase 2: Application Layer (CQRS + Validation) — 🟡 75% Done

- [x] Setup MediatR with assembly scanning (`IApplicationAssemblyMarker`) ✅
- [x] Create `ApiResult<T>` response wrapper (see `architecture.md` Section 7.5) ✅
- [x] Create `PaginatedList<T>` for paginated queries ✅
- [x] Create `ValidationException` custom exception (RFC 7807 ProblemDetails) ✅
- [x] Create `ValidationBehavior<TRequest, TResponse>` MediatR pipeline behavior ✅
- [x] Create `LoggingBehavior<TRequest, TResponse>` MediatR pipeline behavior ✅
- [x] Create `DependencyInjection.cs` in Application layer (MediatR + behaviors + FluentValidation registration) ✅
- [x] Create service interfaces ✅:
  - [x] `IGenericRepository<T>` ✅
  - [x] `IUnitOfWork` ✅ (simplified — no transaction methods)
  - [x] `IDateTimeProvider` ✅
- [x] **Games Feature (CQRS)** ✅:
  - [x] `CreateGameCommand` + Handler + Validator + Response DTO
  - [x] `UpdateGameCommand` + Handler + Validator + Response DTO
  - [x] `DeleteGameCommand` + Handler (soft delete)
  - [x] `GetGamesQuery` + Handler + `GameDto` (paginated, search)
  - [x] `GetGameByIdQuery` + Handler + `GameDetailDto`
  - [x] `GameMappings` AutoMapper profile
  - [x] `NotFoundException` custom exception
  - [x] `Game.Update()` domain method added
- [x] **Chat Feature (CQRS)** — Placeholder ✅:
  - [x] `SendChatMessageCommand` + Handler (placeholder — returns stub response)
  - [x] `GetChatHistoryQuery` + Handler (placeholder — returns empty list)
- [x] **Predictions Feature (CQRS)** — Placeholder ✅:
  - [x] `GetWinRateQuery` + Handler (placeholder — returns stub response)
  - [x] `GetChurnPredictionQuery` + Handler (placeholder — returns stub response)
- [x] **Auth Feature (CQRS)** (Phase 6) ✅:
  - [x] `RegisterUserCommand` + Handler + Validator
  - [x] `LoginUserCommand` + Handler + Validator
  - [x] `RefreshTokenCommand` + Handler + Validator
  - [x] `GetCurrentUserQuery` + Handler
  - [x] `IPasswordHasher` interface + `BCryptPasswordHasher` implementation
  - [x] `IJwtTokenService` interface + `JwtTokenService` implementation (access + refresh token)
  - [x] `User` domain entity (inherits BaseEntity)
  - [x] `RefreshToken` domain entity (inherits BaseEntity)
  - [x] JWT Bearer authentication configured in `Program.cs`
  - [x] `[Authorize]` on all endpoints except `/api/auth/*`
  - [x] EF Core migration `AddAuthTables` created
- [x] **MatchHistory Feature (CQRS)** — Placeholder ✅:
  - [x] `RecordMatchCommand` + Handler (placeholder — returns stub response)
  - [x] `GetMatchHistoryQuery` + Handler (placeholder — returns empty list)

> Note: `Features/Users/Commands/RegisterUser/` folder exists but is **empty**.

---

## Phase 3: Database & EF Core — 🟢 85% Done

- [x] Create `ApplicationDbContext` with DbSets for all entities ✅
- [x] Implement JSON column `ValueConverter` and `ValueComparer` for:
  - [x] `GameCharacter.CustomProperties` ✅
  - [x] `GameCard.CardProperties` ✅
  - [x] `MatchHistory.GameFeatures` ✅
- [x] Create Fluent API configurations:
  - [x] `GameConfiguration` ✅
  - [x] `GameCardConfiguration` ✅
  - [x] `GameCharacterConfiguration` ✅
  - [x] `GameRuleChunkConfiguration` ✅
  - [x] `MatchHistoryConfiguration` ✅
- [x] Configure soft delete global query filter (`!IsDeleted`) ✅ — Implemented via `HasQueryFilter` + shadow property (runtime-only, no snapshot) ✅
- [x] Add EF Core migrations — `InitialCreate` migration exists ✅
  - [x] Soft delete via shadow property + query filter — no re-migration needed ✅
- [ ] ❌ Seed initial Games data

---

## Phase 4: Repository & Unit of Work — ✅ 100% Done

- [x] Create `IGenericRepository<T>` interface in Application layer ✅ (done in Phase 2)
- [x] Create `IUnitOfWork` interface in Application layer ✅ (done in Phase 2)
- [x] Implement `GenericRepository<T>` in Infrastructure layer ✅:
  - `GetByIdAsync` (AsNoTracking), `GetAllAsync` (AsNoTracking), `GetPagedAsync` (with optional filter)
  - `AddAsync`, `UpdateAsync` (Attach + Modified + MarkUpdated), `DeleteAsync` (soft-delete + Modified)
  - `CountAsync` (with optional filter), `Query()` (IQueryable for advanced LINQ)
  - All queries automatically filter `!IsDeleted` via EF Core `HasQueryFilter` ✅
- [x] Implement `UnitOfWork` with lazy-initialized repositories and `SaveChangesAsync` ✅
- [x] Register repositories in DI container (`DependencyInjection.cs`) ✅:
  - `IGenericRepository<>` → `GenericRepository<>` (open generic, scoped)
  - `IUnitOfWork` → `UnitOfWork` (scoped)
- [x] Build verified — 0 errors ✅

---

## Phase 5: API Controllers & Middleware — ✅ 100% Done

- [x] Create `ExceptionHandlingMiddleware` (global exception handler — RFC 7807 ProblemDetails, wrapped in ApiResult envelope) ✅
- [x] Create `RequestLoggingMiddleware` (structured request/response logging with Serilog) ✅
- [x] Create `ApiResultFilter` (uniform `{ success, data, timestamp }` envelope) ✅
- [x] Create Controllers (5 controllers) ✅:
  - [x] `GamesController` — full CRUD (`/api/games`) — Get, GetById, Create, Update, Delete ✅
  - [x] `ChatController` — RAG endpoints (`/api/chat`) — placeholder ✅
  - [x] `PredictionsController` — ML endpoints (`/api/predictions`) — placeholder ✅
  - [x] `AuthController` — Auth endpoints (`/api/auth`) —薄 Controller ✅
  - [x] `MatchHistoryController` — Match history endpoints (`/api/matches`) — placeholder ✅
- [x] Configure middleware pipeline in `Program.cs` (ExceptionHandling → RequestLogging → Authorization → Controllers) ✅
- [x] Register `ApiResultFilter` globally via `AddControllers(options.Filters.Add<ApiResultFilter>())` ✅
- [x] Swagger JWT Bearer token support ✅ (configured in Phase 0)

---

## Phase 6: Auth (JWT + Refresh Token) — ✅ 100% Done

- [x] Implement `IPasswordHasher` + `BCryptPasswordHasher` ✅
- [x] Implement `IJwtTokenService` + `JwtTokenService` ✅:
  - Access Token (15 min TTL) ✅
  - Refresh Token (7 day TTL, single-use rotation) ✅
- [x] Create `User` domain entity (Email, DisplayName, PasswordHash) ✅
- [x] Create `RefreshToken` domain entity (Token, Expires, Revoked, ReplacedByToken) ✅
- [x] Create `RegisterUserCommand` + Handler + Validator ✅
- [x] Create `LoginUserCommand` + Handler + Validator ✅
- [x] Create `RefreshTokenCommand` + Handler + Validator ✅
- [x] Create `GetCurrentUserQuery` + Handler ✅
- [x] `GET /api/auth/me` — Get current user profile ✅
- [x] `POST /api/auth/refresh` — Refresh token endpoint ✅
- [x] Configure JWT Bearer authentication in `Program.cs` ✅
- [x] Add `[Authorize]` to all endpoints except `/api/auth/*` ✅
- [x] EF Core migration `AddAuthTables` created ✅
- [x] Build verified — 0 errors ✅

---

## Phase 6.5: Auth API Refactoring — ✅ 100% Done

> Eliminated "Fat Controller" anti-pattern. Controllers now only handle HTTP concerns.

### Problems Fixed
1. **Double-wrapping `ApiResult`**: AuthController's `Login/Register/Refresh` actions manually called `ApiResult<T>.Ok()` → `ApiResultFilter` wrapped again → frontend received `{ success, data: { success, data: ... } }`. Removed `Ok()` calls; filter handles it.
2. **Fat Controller**: `LoginUser` action contained BCrypt compare + JWT generation + refresh token issuance + DB operations (~40 lines business logic). Moved to `LoginUserCommandHandler`.
3. **Duplicate exceptions**: `UnauthorizedException` thrown from Controller + Handler (2 different exception types). Consolidated to `UnauthorizedException` everywhere.

### Files Modified
- **Controllers/AuthController.cs** — Stripped to thin dispatchers (1-3 lines each). Uses `[ApiController]`, `[Route("api/[controller]")]`, auto `[ApiResultFilter]` via controller-level attribute. Removed `IAuthorizationService` dependency.
- **RegisterUserCommandHandler.cs** — Absorbed registration logic (duplicate check, password hash, user+token create, save). Throws `ConflictException` / `ValidationException`.
- **LoginUserCommandHandler.cs** — Absorbed login logic (user lookup, BCrypt verify, JWT issue, refresh token create). Throws `NotFoundException` / `UnauthorizedException` / `ValidationException`.
- **RefreshTokenCommandHandler.cs** — Absorbed refresh logic (validate token, rotate, issue new pair). Throws `NotFoundException` / `UnauthorizedException` / `ValidationException`.
- **GetCurrentUserQueryHandler.cs** — Changed `KeyNotFoundException` to `NotFoundException`.

### Build Result
- ✅ 0 errors, 0 warnings (pre-existing AutoMapper NU1903 warning only)

### Governance Rule
> **All future CQRS handlers must throw domain exceptions (`NotFoundException`, `UnauthorizedException`, `ConflictException`, `ValidationException`) instead of standard `InvalidOperationException`.**
> **Controllers must never contain business logic — only HTTP concerns (status codes, route, request binding).**

---

## Phase 6.6: Unified Response Envelope — ✅ 100% Done

> Fixed architectural conflict: Middleware was outputting raw ProblemDetails while Filter wraps with `{ success, data, timestamp }`. Frontend received two incompatible response formats.

### Problem
- **Success path**: `ApiResultFilter` wraps → `{ success: true, data: {...}, timestamp: "..." }`
- **Error path**: `ExceptionHandlingMiddleware` wrote raw ProblemDetails → `{ status: 401, title: "...", detail: "..." }` 
- Frontend Interceptor must handle **two completely different shapes** — `response.data.success` is `undefined` on error.

### Solution
`ExceptionHandlingMiddleware` now wraps ProblemDetails in the same `{ success: false, data: ProblemDetails, timestamp }` envelope. Frontend Interceptor uses **one unified logic**:

```typescript
// Unified — works for both success and error
if (response.data.success) { /* ok */ } else { /* error from response.data.data */ }
```

### File Modified
- **Middleware/ExceptionHandlingMiddleware.cs** — Wrapped ProblemDetails output in `{ success: false, data, timestamp }` envelope. Changed `ContentType` from `application/problem+json` to `application/json`. Updated XML doc.

### Build Result
- ✅ 0 errors (2 pre-existing warnings: AutoMapper NU1903 + nullable CS8625)

---

## Phase 7: RAG Pipeline (Core Feature) — 🔴 0% Not Started

- [ ] ❌ Implement `IEmbeddingService` (Azure OpenAI `text-embedding-3-small`)
- [ ] ❌ Setup Qdrant client and `game_rules` collection (1536-dim, Cosine)
- [ ] ❌ Implement `IVectorDbService` / `QdrantVectorSearchService` with hybrid search
- [ ] ❌ Build `RuleIngestionService`:
  - PDF → semantic chunking → embedding → Qdrant store
  - Store `QdrantPointId` in `GameRuleChunk` entity
- [ ] ❌ Implement `RagOrchestratorService`:
  - Embed question → Qdrant search (top-K) → build context → LLM completion
  - Support Cantonese/Chinese/English responses
  - Include source citations (SectionTitle + GameName)
- [ ] ❌ Wire up `ChatController` with `/api/chat/ask` endpoint
- [ ] ❌ Add metadata filtering (GameId, SectionTitle) to vector searches

> NuGet packages installed: SemanticKernel ✅, Qdrant.Client ✅ — but zero code exists.

---

## Phase 8: ML.NET Predictions — 🔴 0% Not Started

- [ ] ❌ Implement `FeatureEngineering` (extract features from MatchHistory)
- [ ] ❌ Implement `WinRatePredictionService`:
  - Binary Classification (Win/Loss) or Regression (Win Probability)
  - Input: match features → Output: win %
- [ ] ❌ Implement `ChurnPredictionService`
- [ ] ❌ Add "What-If" simulation logic
- [ ] ❌ Wire up `PredictionsController` with endpoints
- [ ] ❌ Save trained models as ONNX files

> NuGet package installed: ML.NET 5.0.0 ✅ — but zero code exists.

---

## Phase 9: Redis Caching — 🟡 20% Done

- [x] Install Redis NuGet package ✅ (`Microsoft.Extensions.Caching.StackExchangeRedis`)
- [ ] ❌ Implement `RedisCacheService` with cache-aside pattern
- [ ] ❌ Configure cache keys per `architecture.md` Section 13:
  - `game:{id}` — 10 min TTL
  - `winrate:{gameId}:{params}` — 5 min TTL
  - `rag:{hash(query+gameId)}` — 30 min TTL
  - `leaderboard:{gameId}` — 15 min TTL
- [ ] ❌ Apply caching to Game queries and ML predictions
- [ ] ❌ Add cache invalidation on write operations

---

## Phase 10: Hangfire Background Jobs — 🟡 33% Done

- [x] Install Hangfire NuGet package ✅ (`Hangfire.Core`, `Hangfire.SqlServer`, `Hangfire.AspNetCore`)
- [x] Setup Hangfire with SQL Server storage ✅ (in `Infrastructure/DependencyInjection.cs`)
- [ ] ❌ Create `ModelTrainingJob` — periodic model retraining
- [ ] ❌ Create `CacheRefreshJob` — refresh warm caches
- [x] Configure Hangfire dashboard (dev only) ✅ (in `Program.cs`)
- [ ] ❌ Register recurring jobs in `Program.cs`

---

## Phase 11: SignalR Real-time — 🔴 0% Not Started

- [ ] ❌ Install SignalR NuGet package
- [ ] ❌ Create `ChatHub` for live chat streaming
- [ ] ❌ Implement live prediction update broadcasting
- [ ] ❌ Add game event streaming (match recorded, model updated)
- [ ] ❌ Configure SignalR with JWT auth
- [ ] ❌ Setup Angular SignalR client service (future)

---

## Phase 12: Angular Frontend — 🔴 0% Not Started

- [ ] ❌ Setup Angular 19 project with Tailwind CSS
- [ ] ❌ **Core Module**:
  - Auth Guard + JWT Interceptor
  - HTTP Service (API calls with `ApiResult` handling)
  - SignalR Service (real-time connection)
- [ ] ❌ **Feature Modules**:
  - Dashboard (overview stats, charts with ng2-charts)
  - Games (CRUD, game detail page)
  - Chat (RAG chatbot interface with source citations)
  - Predictions (win rate, churn, What-If simulation)
  - Match History (record & view matches)
- [ ] ❌ **Shared Components**:
  - Navbar, Sidebar, Footer
  - Data Table with Pagination
  - Loading Spinner, Error Alert
  - Confirmation Dialog (SweetAlert2)

---

## Phase 13: Testing & Deployment — 🔴 0% Not Started

- [ ] ❌ **Unit Tests (xUnit + Moq)**:
  - Test CQRS handlers in isolation
  - Test repository logic
  - Test validation behaviors
- [ ] ❌ **Integration Tests**:
  - Test API endpoints with `WebApplicationFactory`
  - Test EF Core with InMemory provider
- [ ] ❌ **E2E Tests (Playwright)**:
  - Test critical user flows
- [ ] ❌ **Docker Compose (Local)**:
  - SQL Server (1433)
  - Qdrant (6333)
  - Redis (6379)
  - Seq (5341)
- [ ] ❌ **Azure Deployment (Future)**:
  - Azure App Service + SQL Database
  - Azure OpenAI + Qdrant Cloud
  - Azure Redis Cache + SignalR Service
  - Key Vault for secrets

---

## 🎯 High Priority Quick Wins (Do These First)

1. **Phase 0** → Complete missing NuGet packages, DI setup, appsettings → ✅ 100%
2. **Phase 1** → Create BaseEntity, refactor all entities → ✅ 100%
3. **Phase 3** → Add soft delete query filter, re-migrate, seed data → 🟢 95%
4. **Phase 4** → Repository pattern operational → ✅ 100%
5. **Phase 5** → Games CRUD + middleware + all controllers → ✅ 100%
6. **Phase 7** → Basic RAG flow (`/api/chat/ask` with one game's rules) → 🔴 0%

> 💡 **Strategy**: Get a thin vertical slice working first (Game CRUD + RAG Chat), then expand horizontally.

---

## 🛑 Currently Blocked

- (none — Phase 3 soft delete query filter resolved ✅)

---

## 📊 Overall Progress

| Phase | Name | Status | Progress |
|-------|------|--------|----------|
| 0 | Project Setup | ✅ Done | 100% |
| 1 | Domain Entities | ✅ Done | 100% |
| 2 | Application Layer | 🟢 Done | 100% |
| 3 | Database & EF Core | 🟢 Done (pending seed) | 95% |
| 4 | Repository & UoW | ✅ Done | 100% |
| 5 | API Controllers & Middleware | ✅ Done | 100% |
| 6 | Auth (JWT) | ✅ Done | 100% |
| 7 | RAG Pipeline | 🔴 Not Started | 0% |
| 8 | ML.NET | 🔴 Not Started | 0% |
| 9 | Redis Caching | 🟡 Partial | 20% |
| 10 | Hangfire | 🟡 Partial | 33% |
| 11 | SignalR | 🔴 Not Started | 0% |
| 12 | Angular Frontend | 🔴 Not Started | 0% |
| 13 | Testing & Deployment | 🔴 Not Started | 0% |

---

## 📝 Notes

- Use **Cline aggressively** for generating boilerplate and services
- **RAG first** — it's the core value of this project
- All EF Core queries must filter `!IsDeleted` — no exceptions
- All API methods must be `async/await` — never use `.Result` or `.Wait()`
- API responses unified: `{ success: boolean, data: T | ProblemDetails, timestamp }` — both success and error paths use the same envelope. `ApiResultFilter` handles success, `ExceptionHandlingMiddleware` wraps errors.
- Keep ML models lightweight; use ONNX where possible
- Always include metadata filtering in vector searches
- Reference `architecture.md` for any architectural decisions

---

*Update this TODO as you progress. Mark items done with `[x]`. Move blockers to the blocked section.*
