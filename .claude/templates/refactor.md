# 重構工作流程

本文檔定義在本專案中進行重構時應遵循的標準工作流程。

---

## 觸發條件

當使用者請求：
- 重構現有程式碼
- 改善程式碼結構
- 提取可復用元件
- 優化效能
- 準備新增功能

---

## 工作流程

### Phase 1: 評估範圍

#### 1.1 識別受影響範圍
- [ ] 確定需要重構的程式碼範圍
- [ ] 列出所有相關檔案
- [ ] 識別依賴該程式碼的其他模組

#### 1.2 評估風險
- [ ] 重構可能影響哪些功能？
- [ ] 是否有現有測試覆蓋？
- [ ] 是否涉及跨層依賴？

#### 1.3 制定回滾計畫
- [ ] 建立還原點（Git commit）
- [ ] 準備回滾步驟
- [ ] 確認可以漸進式重構

---

### Phase 2: 準備工作

#### 2.1 建立重構分支
```bash
git checkout -b refactor/{feature}-{description}
```

#### 2.2 確保現有測試通過
```bash
# 後端
cd BoardGameAiDashboard
dotnet test --filter "FullyQualifiedName~{AffectedModule}"

# 前端
cd DashboardFrontend
npm test
```

#### 2.3 備份現有程式碼
```bash
git add .
git commit -m "chore(refactor): backup before {description}"
```

---

### Phase 3: 小步重構

#### 3.1 重構原則
- **每次只做一件事**：提取介面、移动代码、重命名、简化逻辑
- **每步驗證後再繼續**
- **保持 Commit 顆粒度小**

#### 3.2 常見重構模式

##### 3.2.1 提取介面（Extract Interface）

**Before**: 直接依賴具體實作
```csharp
// ❌ Application/Features/Games/Queries/GetGamesQueryHandler.cs
public class GetGamesQueryHandler 
{
    private readonly ApplicationDbContext _context; // 直接依賴
    
    public GetGamesQueryHandler(ApplicationDbContext context)
    {
        _context = context;
    }
}
```

**After**: 依賴抽象介面
```csharp
// ✅ Application/Common/Interfaces/IUnitOfWork.cs
public interface IUnitOfWork
{
    IGameRepository Games { get; }
    Task SaveChangesAsync(CancellationToken ct);
}

// ✅ Application/Features/Games/Queries/GetGamesQueryHandler.cs
public class GetGamesQueryHandler 
{
    private readonly IUnitOfWork _unitOfWork;
    
    public GetGamesQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }
}
```

##### 3.2.2 移動程式碼到正確層

**Before**: Api 層包含業務邏輯
```csharp
// ❌ Api/Controllers/GamesController.cs
[HttpPost]
public async Task<IActionResult> Create([FromBody] CreateGameCommand command)
{
    // ❌ Controller 做驗證
    if (string.IsNullOrEmpty(command.Name))
        return BadRequest("Name is required");
    
    // ❌ Controller 做業務邏輯
    var game = new Game(command.Name);
    _context.Games.Add(game);
    await _context.SaveChangesAsync();
    
    return Ok(game);
}
```

**After**: Api 只做 HTTP，業務邏輯在 Handler
```csharp
// ✅ Api/Controllers/GamesController.cs
[ApiController]
[Route("api/[controller]")]
public class GamesController : ControllerBase
{
    private readonly IMediator _mediator;
    
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateGameCommand command,
        CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }
}

// ✅ Application/Features/Games/Commands/CreateGame/CreateGameCommandHandler.cs
public class CreateGameCommandHandler 
    : IRequestHandler<CreateGameCommand, ApiResult<GameDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    
    public CreateGameCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }
    
    public async Task<ApiResult<GameDto>> Handle(CreateGameCommand command, CancellationToken ct)
    {
        // ✅ 業務邏輯在這裡
        var game = Game.Create(command.Name);
        await _unitOfWork.Games.AddAsync(game, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return ApiResult<GameDto>.Success(MapToDto(game));
    }
}
```

##### 3.2.3 提取可復用元件（Angular）

**Before**: 重複的 HTTP 錯誤處理
```typescript
// ❌ DashboardFrontend/src/app/features/games/game-list.component.ts
loadGames(): void {
  this.http.get('/api/games').subscribe({
    next: (games) => this.games = games,
    error: (error) => {
      console.error(error);
      alert('Failed to load games');
    }
  });
}
```

**After**: 使用 HttpInterceptor 統一處理
```typescript
// ✅ DashboardFrontend/src/app/core/interceptors/error-handler.interceptor.ts
@Injectable()
export class ErrorHandlerInterceptor implements HttpInterceptor {
  intercept(req: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    return next.handle(req).pipe(
      catchError(error => {
        console.error('HTTP Error:', error);
        alert(`Error: ${error.message}`);
        return throwError(() => error);
      })
    );
  }
}

// ✅ 組件中移除重複的錯誤處理
loadGames(): void {
  this.service.getAll().subscribe({
    next: (games) => this.games.set(games)
  });
}
```

##### 3.2.4 改用 Signals（Angular）

**Before**: 使用 BehaviorSubject
```typescript
// ❌ DashboardFrontend/src/app/core/services/game.service.ts
@Injectable({ providedIn: 'root' })
export class GameService {
  private readonly gamesSubject = new BehaviorSubject<Game[]>([]);
  readonly games$ = this.gamesSubject.asObservable();
  
  loadGames(): void {
    this.http.get<Game[]>('/api/games').subscribe(games => {
      this.gamesSubject.next(games);
    });
  }
}
```

**After**: 使用 Signals
```typescript
// ✅ DashboardFrontend/src/app/core/services/game.service.ts
@Injectable({ providedIn: 'root' })
export class GameService {
  private readonly http = inject(HttpClient);
  
  readonly games = signal<Game[]>([]);
  readonly isLoading = signal(false);
  
  loadGames(): void {
    this.isLoading.set(true);
    this.http.get<Game[]>('/api/games').pipe(
      tap(games => this.games.set(games)),
      finalize(() => this.isLoading.set(false))
    ).subscribe();
  }
}
```

---

### Phase 4: 驗證每步

#### 4.1 編譯檢查
```bash
# 後端
cd BoardGameAiDashboard
dotnet build

# 前端
cd DashboardFrontend
npm run build
```

#### 4.2 執行測試
```bash
# 後端
dotnet test

# 前端
npm test
```

#### 4.3 功能驗證
- [ ] 手動測試受影響的功能
- [ ] 確認沒有破壞現有行為

---

### Phase 5: 清理與提交

#### 5.1 移除死程式碼
- [ ] 檢查是否有未使用的類別/方法
- [ ] 移除陳舊的註解
- [ ] 更新相關文件

#### 5.2 格式化程式碼
```bash
dotnet format
```

#### 5.3 Git Commit

```bash
git add .
git commit -m "refactor({feature}): {description}

Changes:
- {change 1}
- {change 2}

Before: {brief description of before state}
After: {brief description of after state}"
```

---

## 常見重構場景檢查清單

### 1. Clean Architecture 約束檢查

| 檢查項 | 標準 |
|--------|------|
| Domain 無外部依賴 | 不能引用 Application、Infrastructure、Api |
| Application 只依賴 Domain | 只能引用 Domain 層 |
| Infrastructure 實作 Application 介面 | 不能直接引用 Api 層 |
| Api 只做 HTTP 處理 | 不含業務邏輯 |

### 2. CQRS 模式檢查

| 檢查項 | 標準 |
|--------|------|
| Commands 只做寫入 | 不應返回大量資料 |
| Queries 只做讀取 | 不應修改資料庫狀態 |
| Handler 單一職責 | 一個 Handler 一個職責 |

### 3. Angular 模式檢查

| 檢查項 | 標準 |
|--------|------|
| Standalone Components | 不使用 NgModule |
| Signals 狀態管理 | 優先使用 signal() 而非 BehaviorSubject |
| 延遲載入路由 | 使用 loadComponent() 懶載入 |

---

## 快速檢查清單

- [ ] 重構範圍已評估
- [ ] 回滾計畫已準備
- [ ] 現有測試通過
- [ ] 小步重構，每步驗證
- [ ] 無破壞現有功能
- [ ] 程式碼格式化
- [ ] Commit 訊息清晰
