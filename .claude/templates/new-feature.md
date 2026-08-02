# 新增功能工作流程

本文檔定義在本專案中新增功能時應遵循的標準工作流程。

---

## 觸發條件

當使用者請求：
- 新增新的功能模組
- 新增實體（Entity）
- 新增 API 端點
- 新增前端頁面

---

## 工作流程

### Phase 1: 理解需求

#### 1.1 收集資訊
- [ ] 理解業務需求和使用情境
- [ ] 確認功能範圍和邊界
- [ ] 查看現有類似功能的實作模式

#### 1.2 規劃結構
- [ ] 確認功能屬於哪個領域（Games, Auth, Chat, Predictions 等）
- [ ] 規劃實體和關係
- [ ] 設計 API 端點

#### 1.3 檢查現有資產
- [ ] 閱讀相關 Skills（Angular、RAG、EF Core、JWT）
- [ ] 查看 CLAUDE.md 架構說明
- [ ] 查看 Coding Standards

---

### Phase 2: 後端實作（.NET）

按照 Clean Architecture 從內到外實作：

#### 2.1 Domain 層：實體

```csharp
// BoardGameAiDashboard.Domain/Entities/{EntityName}.cs
public class {EntityName} : BaseEntity
{
    // 屬性（使用 private set）
    public string Name { get; private set; } = null!;
    
    // 導航屬性
    public Guid? ParentId { get; private set; }
    public ParentEntity? Parent { get; private set; }
    
    // 私有建構函式
    private {EntityName}() { }
    
    // 工廠方法
    public static {EntityName} Create(string name, ...)
    {
        var entity = new {EntityName}
        {
            Name = name,
            // ...
        };
        entity.SetCreated();
        return entity;
    }
    
    // 網域方法
    public void Update(...) { }
    public void Delete() => base.Delete();
}
```

#### 2.2 Application 層：介面

```csharp
// BoardGameAiDashboard.Application/Common/Interfaces/I{EntityName}Repository.cs
public interface I{EntityName}Repository
{
    Task<{EntityName}?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<{EntityName}>> GetAllAsync(CancellationToken ct);
    Task AddAsync({EntityName} entity, CancellationToken ct);
    Task UpdateAsync({EntityName} entity, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}
```

#### 2.3 Application 層：CQRS Commands

```
Application/Features/{Feature}/Commands/{Action}{EntityName}/
├── {Action}{EntityName}Command.cs
├── {Action}{EntityName}CommandValidator.cs
├── {Action}{EntityName}CommandHandler.cs
└── {Action}{EntityName}CommandResponse.cs
```

```csharp
// Command
public record {Action}{EntityName}Command(...) 
    : IRequest<ApiResult<{EntityName}Dto>>;

// Validator
public class {Action}{EntityName}CommandValidator 
    : AbstractValidator<{Action}{EntityName}Command>
{
    public {Action}{EntityName}CommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().MaximumLength(100);
        // ...
    }
}

// Handler
public class {Action}{EntityName}CommandHandler 
    : IRequestHandler<{Action}{EntityName}Command, ApiResult<{EntityName}Dto>>
{
    public async Task<ApiResult<{EntityName}Dto>> Handle(
        {Action}{EntityName}Command command, 
        CancellationToken ct)
    {
        var entity = {EntityName}.Create(...);
        await _repository.AddAsync(entity, ct);
        return ApiResult<{EntityName}Dto>.Success(MapToDto(entity));
    }
}
```

#### 2.4 Application 層：CQRS Queries

```
Application/Features/{Feature}/Queries/{Get}{EntityName}s/
├── {Get}{EntityName}Query.cs
└── {Get}{EntityName}QueryHandler.cs
```

#### 2.5 Infrastructure 層：Repository 實作

```csharp
// BoardGameAiDashboard.Infrastructure/Persistence/Repositories/{EntityName}Repository.cs
public class {EntityName}Repository : I{EntityName}Repository
{
    private readonly ApplicationDbContext _context;
    
    public async Task<{EntityName}?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await _context.{EntityName}s
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, ct);
    }
}
```

#### 2.6 Infrastructure 層：DbContext 設定

```csharp
// Infrastructure/Persistence/ApplicationDbContext.cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<{EntityName}>(entity =>
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
        
        // 關聯
        entity.HasOne(e => e.Parent)
              .WithMany()
              .HasForeignKey(e => e.ParentId)
              .OnDelete(DeleteBehavior.Cascade);
        
        // 軟刪除過濾器（必須！）
        entity.HasQueryFilter(e => e.IsDeleted == false);
    });
}
```

#### 2.7 Api 層：Controller

```csharp
// BoardGameAiDashboard.Api/Controllers/{Feature}Controller.cs
[ApiController]
[Route("api/{feature}")]
public class {Feature}Controller : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] {Action}{EntityName}Command command,
        CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }
    
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _mediator.Send(new Get{EntityName}sQuery(), ct);
        return Ok(result);
    }
}
```

#### 2.8 資料庫遷移

```bash
dotnet ef migrations add Add{EntityName} 
    --project BoardGameAiDashboard.Infrastructure 
    --startup-project BoardGameAiDashboard.Api
dotnet ef database update 
    --project BoardGameAiDashboard.Infrastructure 
    --startup-project BoardGameAiDashboard.Api
```

---

### Phase 3: 前端實作（Angular）

#### 3.1 模型定義

```typescript
// DashboardFrontend/src/app/core/models/{feature}.model.ts
export interface {EntityName}Dto {
  id: string;
  name: string;
  createdAt: string;
}

export interface Create{EntityName}Request {
  name: string;
}
```

#### 3.2 服務層

```typescript
// DashboardFrontend/src/app/core/services/{feature}.service.ts
import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { {EntityName}Dto, Create{EntityName}Request } from '../models/{feature}.model';

@Injectable({ providedIn: 'root' })
export class {Feature}Service {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/{feature}`;
  
  readonly items = signal<{EntityName}Dto[]>([]);
  readonly isLoading = signal(false);
  
  getAll(): Observable<{EntityName}Dto[]> {
    this.isLoading.set(true);
    return this.http.get<{EntityName}Dto[]>(this.baseUrl).pipe(
      tap({
        next: (items) => this.items.set(items),
        error: () => this.isLoading.set(false),
        finalize: () => this.isLoading.set(false)
      })
    );
  }
  
  create(request: Create{EntityName}Request): Observable<{EntityName}Dto> {
    return this.http.post<{EntityName}Dto>(this.baseUrl, request).pipe(
      tap((item) => this.items.update(items => [...items, item]))
    );
  }
}
```

#### 3.3 頁面元件

```typescript
// DashboardFrontend/src/app/features/{feature}/pages/{feature}-list.component.ts
import { Component, inject, OnInit } from '@angular/core';
import { {Feature}Service } from '../../core/services/{feature}.service';

@Component({
  selector: 'app-{feature}-list',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="container">
      <h1>{{ title }}</h1>
      
      @if (service.isLoading()) {
        <p>Loading...</p>
      } @else {
        @for (item of service.items(); track item.id) {
          <div class="item">{{ item.name }}</div>
        }
      }
    </div>
  `
})
export class {Feature}ListComponent implements OnInit {
  readonly service = inject({Feature}Service);
  title = '{Feature} List';
  
  ngOnInit(): void {
    this.service.getAll();
  }
}
```

#### 3.4 路由配置

```typescript
// DashboardFrontend/src/app/app.routes.ts
export const routes: Routes = [
  {
    path: '{feature}',
    loadComponent: () => import('./features/{feature}/pages/{feature}-list.component')
      .then(m => m.{Feature}ListComponent),
    canActivate: [authGuard]
  }
];
```

---

### Phase 4: 驗證

#### 4.1 建置測試

```bash
# 後端建置
cd BoardGameAiDashboard
dotnet build

# 前端建置
cd DashboardFrontend
npm run build
```

#### 4.2 執行測試

```bash
# 單元測試
dotnet test BoardGameAiDashboard.Tests

# 端對端測試（如果有的話）
npm run test
```

#### 4.3 手動驗證

- [ ] API 端點可以正常呼叫
- [ ] 前端頁面正確顯示資料
- [ ] 建立/更新/刪除功能正常
- [ ] 軟刪除後查詢自動過濾
- [ ] 認證保護正常運作

---

### Phase 5: 提交

#### 5.1 Git Commit

```bash
git add .
git commit -m "feat({feature}): add {entityName} feature

- Add {EntityName} entity with soft delete
- Add CQRS commands and queries
- Add Angular service and component
- Add database migration
- Add unit tests

Closes #<issue-number>"
```

#### 5.2 Commit 訊息規範

| Type | 用途 |
|------|------|
| feat | 新功能 |
| fix | 錯誤修復 |
| docs | 文件變更 |
| style | 程式碼格式 |
| refactor | 重構 |
| perf | 效能改善 |
| test | 測試相關 |
| chore | 建置/工具 |

---

## 快速檢查清單

- [ ] Domain 實體使用 `BaseEntity`
- [ ] 實體使用工廠方法和網域方法
- [ ] DbContext 設定 `HasQueryFilter`
- [ ] Application 定義介面
- [ ] CQRS 檔案結構正確
- [ ] Validator 使用 FluentValidation
- [ ] Controller 只做 HTTP 處理
- [ ] Angular 使用 Standalone Components
- [ ] Angular 使用 Signals 狀態管理
- [ ] `dotnet build` 成功
- [ ] `dotnet test` 通過
- [ ] 前端 `npm run build` 成功
