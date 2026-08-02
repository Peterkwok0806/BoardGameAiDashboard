# Coding Standards

本文件定義本專案的程式碼風格和命名規範，確保程式碼一致性和可維護性。

---

## C# (.NET) 命名規範

### 1. 命名空間 (Namespace)

```csharp
// 格式：Company.Product.Layer.Entity
namespace BoardGameAiDashboard.Domain.Entities;
namespace BoardGameAiDashboard.Application.Features.Games.Commands.CreateGame;
namespace BoardGameAiDashboard.Infrastructure.Services.Auth;
```

### 2. 類別和方法

| 元素 | 命名方式 | 範例 |
|------|----------|------|
| 類別 | PascalCase | `GameService`, `JwtTokenService` |
| 公開方法 | PascalCase | `GetByIdAsync`, `CreateGame` |
| 私有方法 | PascalCase | `ValidateInput`, `BuildQuery` |
| 保護方法 | PascalCase | `OnInitialized`, `Dispose` |
| 介面 | `I` + PascalCase | `IGameRepository`, `IJwtService` |
| 列舉 | PascalCase | `GameStatus`, `UserRole` |
| 列舉值 | PascalCase | `GameStatus.Active`, `UserRole.Admin` |

### 3. 欄位和屬性

```csharp
// 私有欄位
private readonly IUnitOfWork _unitOfWork;
private Guid _userId;

// 公開屬性
public Guid Id { get; private set; }
public string Name { get; private set; } = null!;

// 集合屬性
public ICollection<Game> Games { get; private set; } = new List<Game>();

// 唯讀屬性（computed）
public bool IsActive => !IsDeleted;
```

### 4. 參數和區域變數

```csharp
// 參數：camelCase
public async Task<GameDto> GetGameByIdAsync(Guid gameId, CancellationToken ct)
{
    // 區域變數：camelCase
    var existingGame = await _repository.GetByIdAsync(gameId, ct);
    return MapToDto(existingGame);
}
```

### 5. 常數

```csharp
// 常數：PascalCase
public const int MaxPlayerCount = 10;
public const string DefaultRole = "User";
```

---

## TypeScript / Angular 命名規範

### 1. 檔案命名

| 類型 | 命名方式 | 範例 |
|------|----------|------|
| 元件 | kebab-case | `game-card.component.ts` |
| 服務 | kebab-case + .service | `auth.service.ts` |
| 模型 | kebab-case + .model | `user.model.ts` |
| 介面 | kebab-case + .interface | `api-result.interface.ts` |
| Guard | kebab-case + .guard | `auth.guard.ts` |
| Interceptor | kebab-case + .interceptor | `jwt.interceptor.ts` |

### 2. 類別命名

```typescript
// 類別名稱：PascalCase
export class GameService { }
export class AuthGuard { }
export class JwtInterceptor { }

// 介面：PascalCase
export interface GameDto { }
export interface UserProfile { }
```

### 3. 變數和函數

```typescript
// 區域變數：camelCase
const userId = '123';
const gameList: Game[] = [];

// 函數：camelCase
function getUserById(id: string): User { }
const handleSubmit = () => { };

// 導出變數：camelCase 或 CONSTANT_CASE
export const API_BASE_URL = 'http://localhost:5001';
export const MAX_RETRY_COUNT = 3;
```

### 4. Signal 命名

```typescript
// Signal：_前綴 + PascalCase（私有）
private readonly _games = signal<Game[]>([]);

// 唯讀 Signal（公開）：PascalCase
readonly games = this._games.asReadonly();

// Computed Signal：PascalCase
readonly isLoading = computed(() => this._isLoading());
readonly gameCount = computed(() => this.games().length);
```

### 5. HTML 模板

```html
<!-- 元素選擇器：kebab-case -->
<app-game-card></app-game-card>
<app-user-profile></app-user-profile>

<!-- 屬性：camelCase（事件）或 kebab-case（屬性綁定） -->
<button (click)="onSubmit()">Submit</button>
<input [value]="username" />

<!-- CSS 類別：kebab-case -->
<div class="game-card game-card--featured"></div>
```

---

## 檔案組織結構

### C# 專案

```
BoardGameAiDashboard/
├── Domain/
│   ├── Entities/          # 實體類別
│   ├── Common/            # 基底類別
│   └── Enums/             # 列舉
├── Application/
│   ├── Common/
│   │   ├── Interfaces/    # 介面定義
│   │   ├── Models/        # 共享模型
│   │   ├── Behaviors/     # MediatR behaviors
│   │   └── Exceptions/     # 自訂例外
│   └── Features/
│       └── {Feature}/
│           ├── Commands/  # CQRS Commands
│           └── Queries/    # CQRS Queries
├── Infrastructure/
│   ├── Services/          # 服務實作
│   ├── Persistence/       # DbContext、Config
│   ├── Migrations/        # EF Core 遷移
│   └── Settings/          # 設定類別
└── Api/
    ├── Controllers/       # API Controllers
    ├── Middleware/        # Middleware
    └── Filters/           # 篩選器
```

### Angular 前端

```
src/
├── app/
│   ├── core/
│   │   ├── models/        # TypeScript 介面
│   │   ├── services/      # Angular Services
│   │   ├── guards/        # Route Guards
│   │   ├── interceptors/  # HTTP Interceptors
│   │   └── pipes/         # Pipes
│   ├── features/
│   │   └── {feature}/
│   │       ├── components/
│   │       ├── services/
│   │       └── pages/
│   ├── shared/
│   │   ├── components/    # 共用元件
│   │   └── directives/
│   ├── app.config.ts
│   └── app.routes.ts
├── environments/
│   ├── environment.ts
│   └── environment.prod.ts
└── styles/
```

---

## 註解規範

### C# XML 文件註解

```csharp
/// <summary>
/// 取得遊戲依 ID
/// </summary>
/// <param name="gameId">遊戲 ID</param>
/// <param name="ct">取消權杖</param>
/// <returns>遊戲詳細資料</returns>
/// <exception cref="NotFoundException">當遊戲不存在時</exception>
public async Task<GameDetailDto?> GetGameByIdAsync(
    Guid gameId,
    CancellationToken ct = default)
{
    // ...
}
```

### 重要標記

```csharp
// TODO: 完成實作
// FIXME: 需要修復
// HACK: 臨時解決方案
// NOTE: 重要說明
// DEPRECATED: 已過時
```

---

## CQRS 模式規範

### Command 命名

```csharp
// CreateXxxCommand.cs
public record CreateGameCommand(
    string Name,
    string Description,
    int MinPlayers,
    int MaxPlayers
) : IRequest<ApiResult<GameDto>>;

// UpdateXxxCommand.cs
public record UpdateGameCommand(
    Guid Id,
    string Name,
    string Description
) : IRequest<ApiResult<GameDto>>;

// DeleteXxxCommand.cs
public record DeleteGameCommand(Guid Id) : IRequest<ApiResult<bool>>;
```

### Query 命名

```csharp
// GetXxxQuery.cs
public record GetGamesQuery(
    int Page = 1,
    int PageSize = 10
) : IRequest<ApiResult<PaginatedList<GameDto>>>;

// GetXxxByIdQuery.cs
public record GetGameByIdQuery(Guid Id) : IRequest<ApiResult<GameDetailDto>>;
```

### Handler 檔案結構

```
Commands/CreateGame/
├── CreateGameCommand.cs          # Command 定義
├── CreateGameCommandValidator.cs # FluentValidation 驗證器
├── CreateGameCommandHandler.cs   # Handler 實作
└── CreateGameCommandResponse.cs # 回應模型
```

---

## 異常處理規範

### 使用網域例外

```csharp
// Domain 層定義例外
public class NotFoundException : Exception { }
public class ValidationException : Exception { }
public class UnauthorizedException : Exception { }
public class ConflictException : Exception { }

// 使用時
throw new NotFoundException(nameof(Game), gameId);
throw new ValidationException("Invalid input", errors);
throw new UnauthorizedException("Invalid credentials");
```

### 避免使用

```csharp
// ❌ 避免
throw new InvalidOperationException();
throw new ArgumentException();
throw new Exception();

// ✅ 使用
throw new ValidationException("...");
throw new ConflictException("...");
```

---

## Async/Await 規範

```csharp
// ✅ 正確
public async Task<GameDto> GetGameAsync(Guid id)
{
    var game = await _repository.GetByIdAsync(id);
    return MapToDto(game);
}

// ❌ 錯誤
public Task<GameDto> GetGameAsync(Guid id)
{
    return _repository.GetByIdAsync(id); // 直接返回 Task
}

// ✅ 處理異步結果
public async Task ProcessAsync()
{
    var result = await _service.GetAsync();
    if (result == null)
    {
        throw new NotFoundException();
    }
    return result;
}

// ❌ 錯誤
public async Task ProcessAsync()
{
    var result = _service.GetAsync().Result; // 阻塞調用
}
```

---

## 軟刪除規範

### 實體必須

1. 繼承 `BaseEntity`
2. 使用 `Delete()` 方法軟刪除
3. DbContext 設定全域查詢過濾器

```csharp
// Domain/Entities/Game.cs
public class Game : BaseEntity
{
    public void Delete() => base.Delete();
}

// DbContext
modelBuilder.Entity<Game>(entity =>
{
    entity.HasQueryFilter(e => e.IsDeleted == false);
});
```

### 查詢已刪除實體

```csharp
// 使用 IgnoreQueryFilters
var deletedGame = await _context.Games
    .IgnoreQueryFilters()
    .FirstOrDefaultAsync(g => g.Id == id);
```

---

## 版本更新日誌格式

```csharp
/// <summary>
/// 版本更新日誌
/// 2024-01-15: v1.0 - 初始版本
/// 2024-02-20: v1.1 - 新增取得所有遊戲功能
/// 2024-03-10: v1.2 - 重構為 CQRS 模式
/// </summary>
```

---

## Git Commit 訊息格式

```
<type>(<scope>): <subject>

Types:
- feat: 新功能
- fix: 錯誤修復
- docs: 文件變更
- style: 程式碼格式（不影響功能）
- refactor: 重構
- perf: 效能改善
- test: 測試相關
- chore: 建置/工具變更

Examples:
feat(auth): 新增 Refresh Token 輪換功能
fix(games): 修復軟刪除查詢過濾問題
docs(readme): 更新 API 文件
refactor(rag): 重構向量搜尋服務
```
