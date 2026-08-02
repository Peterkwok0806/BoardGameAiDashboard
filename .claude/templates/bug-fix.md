# Bug 修復工作流程

本文檔定義在本專案中修復 Bug 時應遵循的標準工作流程。

---

## 觸發條件

當使用者請求：
- 修復錯誤或異常行為
- 處理預期外的結果
- 排查功能性問題

---

## 工作流程

### Phase 1: 重現問題

#### 1.1 收集資訊
- [ ] 理解預期行為 vs 實際行為
- [ ] 確認觸發條件和步驟
- [ ] 檢查相關的錯誤訊息或日誌

#### 1.2 建立最小重現案例
- [ ] 隔離問題，移除不相關因素
- [ ] 記錄觸發步驟
- [ ] 確認在乾淨環境可重現

#### 1.3 檢查現有測試覆蓋
- [ ] 是否有相關的單元測試？
- [ ] 是否需要新增測試防止回歸？

---

### Phase 2: 分析根因

#### 2.1 找到相關程式碼
- [ ] 定位問題發生的位置
- [ ] 追蹤資料流和呼叫鏈
- [ ] 檢查相關的約束和假設

#### 2.2 理解問題根因
- [ ] 是邏輯錯誤？資料問題？環境問題？
- [ ] 為什麼之前沒發現？
- [ ] 是否涉及架構層面的問題？

#### 2.3 諮詢相關資產
- [ ] 查看 Troubleshooting Guide
- [ ] 檢查現有的 Architecture Decision Records
- [ ] 參考 Coding Standards

---

### Phase 3: 修復問題

#### 3.1 設計修復方案
- [ ] 最小化變更範圍
- [ ] 避免引入新問題
- [ ] 考慮向後相容性

#### 3.2 後端修復（.NET）

```csharp
// 1. 如果涉及實體邏輯，檢查網域方法
// Domain/Entities/Game.cs
public class Game : BaseEntity
{
    // 檢查工廠方法和網域方法
    public static Game Create(string name, ...)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required", nameof(name));
        // ...
    }
}

// 2. 如果涉及資料庫查詢，檢查 EF Core 配置
// 確認 HasQueryFilter 設定正確
modelBuilder.Entity<Game>().HasQueryFilter(e => e.IsDeleted == false);

// 3. 如果涉及驗證，檢查 FluentValidation
// Application/Features/Games/Commands/CreateGame/CreateGameCommandValidator.cs
public class CreateGameCommandValidator : AbstractValidator<CreateGameCommand>
{
    public CreateGameCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required");
    }
}
```

#### 3.3 前端修復（Angular）

```typescript
// 1. 如果涉及 HTTP 呼叫，檢查服務層
// DashboardFrontend/src/app/core/services/game.service.ts
@Injectable({ providedIn: 'root' })
export class GameService {
  private readonly http = inject(HttpClient);
  
  getAll(): Observable<GameDto[]> {
    return this.http.get<GameDto[]>(`${environment.apiBaseUrl}/games`).pipe(
      catchError(error => {
        console.error('Failed to load games:', error);
        return throwError(() => error);
      })
    );
  }
}

// 2. 如果涉及信號更新，檢查狀態管理
// DashboardFrontend/src/app/features/games/pages/game-list.component.ts
@Component({...})
export class GameListComponent {
  readonly gameService = inject(GameService);
  
  loadGames(): void {
    this.gameService.getAll().subscribe({
      next: (games) => this.games.set(games),
      error: (error) => this.handleError(error)
    });
  }
}
```

---

### Phase 4: 驗證修復

#### 4.1 新增單元測試
- [ ] 為 Bug 建立回歸測試
- [ ] 確保測試覆蓋邊界情況

```csharp
// BoardGameAiDashboard.Tests/Features/Games/
[Fact]
public async Task CreateGame_WithEmptyName_ShouldThrowValidationException()
{
    // Arrange
    var command = new CreateGameCommand { Name = "" };
    
    // Act & Assert
    await Assert.ThrowsAsync<ValidationException>(
        () => _mediator.Send(command));
}
```

```typescript
// DashboardFrontend/src/app/features/games/game.service.spec.ts
describe('GameService', () => {
  it('should throw error when name is empty', () => {
    service.create({ name: '' }).subscribe({
      error: (error) => expect(error).toBeDefined()
    });
  });
});
```

#### 4.2 執行測試

```bash
# 後端測試
cd BoardGameAiDashboard
dotnet test --filter "FullyQualifiedName~{TestClass}"

# 前端測試
cd DashboardFrontend
npm test
```

#### 4.3 手動驗證
- [ ] 在瀏覽器中驗證修復
- [ ] 測試相關功能流程
- [ ] 確認邊界情況處理正確

---

### Phase 5: 清理與提交

#### 5.1 清理程式碼
- [ ] 移除除錯用的 Console.WriteLine / console.log
- [ ] 移除暫時的程式碼
- [ ] 確保程式碼格式正確

#### 5.2 Git Commit

```bash
git add .
git commit -m "fix({feature}): resolve {short description}

Problem: {描述問題}
Root cause: {根本原因}
Solution: {修復方案}

Fixes #<issue-number>"
```

---

## 常見 Bug 類型與修復模式

### 1. 軟刪除查詢問題

**徵兆**: 查詢不到應該存在的資料

**檢查點**:
- [ ] 實體是否繼承 `BaseEntity`？
- [ ] DbContext 是否設定 `HasQueryFilter`？
- [ ] 是否使用了 `.IgnoreQueryFilters()`？

**修復**:
```csharp
// 確認 DbContext 中有設定
modelBuilder.Entity<Game>().HasQueryFilter(e => e.IsDeleted == false);

// 如果需要查詢已刪除的實體
var deletedGame = await _context.Games
    .IgnoreQueryFilters()
    .FirstOrDefaultAsync(g => g.Id == id);
```

### 2. JWT 認證失敗

**徵兆**: 401 Unauthorized 或 Token 過期

**檢查點**:
- [ ] Token 是否在有效期限內？
- [ ] Token 簽章是否正確？
- [ ] Refresh token 是否正確輪換？

**修復**: 參考 `skills/jwt-auth.md`

### 3. RAG 查詢返回空結果

**徵兆**: 向量搜尋返回空結果

**檢查點**:
- [ ] Collection 是否存在？
- [ ] Metadata 過濾條件是否正確？
- [ ] 嵌入維度是否匹配？

**修復**: 參考 `skills/rag-pipeline.md`

### 4. EF Core 遷移失敗

**徵兆**: 遷移命令執行錯誤

**檢查點**:
- [ ] 是否已套用所有遷移？
- [ ] 遷移檔案是否有衝突？
- [ ] 模型變更是否正確？

**修復**:
```bash
# 檢查遷移狀態
dotnet ef migrations list --project BoardGameAiDashboard.Infrastructure --startup-project BoardGameAiDashboard.Api

# 修復遷移
dotnet ef migrations remove --project BoardGameAiDashboard.Infrastructure --startup-project BoardGameAiDashboard.Api
dotnet ef migrations add <Name> --project BoardGameAiDashboard.Infrastructure --startup-project BoardGameAiDashboard.Api
```

---

## 快速檢查清單

- [ ] 問題已重現
- [ ] 根因已確認
- [ ] 修復已完成
- [ ] 新增回歸測試
- [ ] 所有測試通過
- [ ] 程式碼已格式化
- [ ] Commit 訊息完整
