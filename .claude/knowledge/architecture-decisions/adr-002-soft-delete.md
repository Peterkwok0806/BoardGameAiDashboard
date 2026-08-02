# ADR-002: 全域軟刪除模式

## 狀態
已接受 (Accepted)

## 日期
2024-01-20

## 上下文
本專案需要追蹤遊戲、角色、卡牌、對戰記錄等資料。考慮到業務需求：
1. 用戶可能需要恢復意外刪除的資料
2. 需要維護資料的歷史完整性
3. 法規合規可能要求保留刪除記錄

因此選擇軟刪除而非硬刪除。

## 決策

### 實作方式

#### 1. BaseEntity 基底類別
所有實體繼承 `BaseEntity`，提供統一的軟刪除能力：

```csharp
// Domain/Common/BaseEntity.cs
public abstract class BaseEntity
{
    public Guid Id { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    
    public void Delete()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
    }
    
    protected void SetCreated()
    {
        CreatedAt = DateTime.UtcNow;
        Id = Guid.NewGuid();
    }
}
```

#### 2. EF Core 全域查詢過濾器
在 DbContext 中設定全域過濾器，自動過濾已刪除的實體：

```csharp
// Infrastructure/Persistence/ApplicationDbContext.cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // 軟刪除過濾器
    modelBuilder.Entity<Game>(entity =>
    {
        entity.HasQueryFilter(e => e.IsDeleted == false);
    });
    
    // 所有實體都需要設定過濾器
}
```

#### 3. 刪除時呼叫網域方法
```csharp
// 正確的刪除方式
game.Delete();

// 不直接修改屬性
// ❌ game.IsDeleted = true;
```

## 理由

### 為什麼使用基底類別？
- **一致性**：所有實體使用相同的軟刪除欄位
- **可維護性**：單一位置修改即可影響所有實體
- **類型安全**：編譯時檢查

### 為什麼使用全域查詢過濾器？
- **透明性**：所有查詢自動應用軟刪除過濾
- **防呆**：開發者不會意外查詢到已刪除資料
- **效能**：過濾在資料庫層執行

### 為什麼需要網域方法？
- **封裝**：刪除邏輯集中管理
- **鉤子**：可在刪除時執行額外邏輯（UpdatedAt）
- **可追蹤**：易於新增審計日誌

## 後續影響

### 需注意的事項

1. **IgnoreQueryFilters()** - 當需要查詢已刪除資料時使用
```csharp
var deletedGames = await _context.Games
    .IgnoreQueryFilters()
    .Where(g => g.IsDeleted)
    .ToListAsync();
```

2. **導航屬性**：已刪除實體的導航屬性仍可載入
```csharp
// 會載入已刪除的子實體
var game = await _context.Games
    .Include(g => g.Characters)
    .FirstOrDefaultAsync(g => g.Id == id);
```

3. **遷移**：新增實體時忘記添加過濾器會導致不一致

## 相關檔案

- [BaseEntity.cs](BoardGameAiDashboard/BoardGameAiDashboard.Domain/Common/BaseEntity.cs)
- [ApplicationDbContext.cs](BoardGameAiDashboard/BoardGameAiDashboard.Infrastructure/Persistence/ApplicationDbContext.cs)
- [EF Core Migration Skill](../skills/efcore-migration.md)
