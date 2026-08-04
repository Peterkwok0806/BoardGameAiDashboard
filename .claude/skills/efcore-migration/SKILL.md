---
name: efcore-migration
description: 新增資料庫遷移、更新結構、建立新實體，或使用 Entity Framework Core
---

## 說明

### EF Core 專案結構

| 元件 | 路徑 |
|------|------|
| DbContext | `Infrastructure/Persistence/ApplicationDbContext.cs` |
| 實體設定 | `Infrastructure/Persistence/Configurations/` |
| 遷移 | `Infrastructure/Migrations/` |
| 實體基底類別 | `Domain/Common/BaseEntity.cs` |
| 通用 Repository | `Infrastructure/Common/Repositories/GenericRepository.cs` |

### 遷移指令

```bash
# 新增遷移
dotnet ef migrations add <MigrationName> --project BoardGameAiDashboard.Infrastructure --startup-project BoardGameAiDashboard.Api

# 套用遷移
dotnet ef database update --project BoardGameAiDashboard.Infrastructure --startup-project BoardGameAiDashboard.Api

# 移除最後一個遷移（如果尚未套用）
dotnet ef migrations remove --project BoardGameAiDashboard.Infrastructure

# 產生 SQL 指令碼（不執行）
dotnet ef migrations script --project BoardGameAiDashboard.Infrastructure --output migrations.sql

# 更新到特定遷移
dotnet ef database update <MigrationName> --project BoardGameAiDashboard.Infrastructure
```

## 必要模式

### 1. 帶軟刪除的基底實體

所有實體**必須**繼承 `BaseEntity`，其提供 `Id` 和 `IsDeleted`：

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
    }
}
```

```csharp
// Domain/Entities/Game.cs
public class Game : BaseEntity
{
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public int MinPlayers { get; private set; }
    public int MaxPlayers { get; private set; }
    
    // 私有 setter — 使用 factory method 或 command 建立
    private Game() { }
    
    public static Game Create(string name, string description, int minPlayers, int maxPlayers)
    {
        var game = new Game
        {
            Name = name,
            Description = description,
            MinPlayers = minPlayers,
            MaxPlayers = maxPlayers
        };
        game.SetCreated();
        return game;
    }
    
    public void Update(string name, string description, int minPlayers, int maxPlayers)
    {
        Name = name;
        Description = description;
        MinPlayers = minPlayers;
        MaxPlayers = maxPlayers;
        UpdatedAt = DateTime.UtcNow;
    }
    
    public void Delete() => base.Delete(); // 使用網域刪除方法
}
```

### 2. 軟刪除的全域查詢過濾器（強制執行）

每個實體**必須**在 `ApplicationDbContext` 中有全域查詢過濾器：

```csharp
// Infrastructure/Persistence/ApplicationDbContext.cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    
    modelBuilder.Entity<Game>(entity =>
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
        
        // ✅ 強制執行 — 軟刪除過濾器
        entity.HasQueryFilter(e => e.IsDeleted == false);
    });
    
    // 其他實體使用相同模式...
}
```

### 3. JSON 欄位與 ValueConverter

對於彈性屬性，使用 `Dictionary<string, string>` 搭配 EF Core `ValueConverter` + `ValueComparer`：

```csharp
modelBuilder.Entity<GameCharacter>(entity =>
{
    // JSON 欄位用於自訂屬性
    entity.Property(e => e.CustomProperties)
          .HasConversion(
              // 序列化為字串
              v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
              // 從字串反序列化
              v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) 
                   ?? new Dictionary<string, string>()
          )
          .Metadata.SetValueComparer(CreateJsonComparer());
});

// Helper 方法（已存在於 ApplicationDbContext）
private static ValueConverter<Dictionary<string, string>, string> CreateJsonConverter() { ... }
private static ValueComparer<Dictionary<string, string>> CreateJsonComparer() { ... }
```

### 4. 實體關聯

設定關聯時使用 cascade delete：

```csharp
modelBuilder.Entity<GameRuleChunk>(entity =>
{
    entity.HasKey(e => e.Id);
    
    entity.HasOne(d => d.Game)
          .WithMany(p => p.RuleChunks)
          .HasForeignKey(d => d.GameId)
          .OnDelete(DeleteBehavior.Cascade); // ✅ 軟刪除建議使用
    
    entity.HasQueryFilter(e => e.IsDeleted == false);
});
```

## 新增實體

### 步驟 1：建立網域實體
```csharp
// Domain/Entities/Tournament.cs
public class Tournament : BaseEntity
{
    public string Name { get; private set; } = null!;
    public Guid GameId { get; private set; }
    public DateTime StartDate { get; private set; }
    public TournamentStatus Status { get; private set; }
    
    // 導航屬性
    public Game? Game { get; private set; }
    
    private Tournament() { }
    
    public static Tournament Create(string name, Guid gameId, DateTime startDate)
    {
        var tournament = new Tournament
        {
            Name = name,
            GameId = gameId,
            StartDate = startDate,
            Status = TournamentStatus.Pending
        };
        tournament.SetCreated();
        return tournament;
    }
}
```

### 步驟 2：在 DbContext 新增 DbSet
```csharp
public class ApplicationDbContext : DbContext
{
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Tournament> Tournaments => Set<Tournament>(); // ✅ 新增此行
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 設定 Tournament
        modelBuilder.Entity<Tournament>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Status)
                  .HasConversion<string>() // 將 enum 儲存為字串
                  .HasMaxLength(50);
            
            entity.HasOne(e => e.Game)
                  .WithMany()
                  .HasForeignKey(e => e.GameId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasQueryFilter(e => e.IsDeleted == false);
        });
    }
}
```

### 步驟 3：在父實體新增導航屬性
```csharp
public class Game : BaseEntity
{
    // ... 現有屬性 ...
    
    // 新增導航屬性
    public ICollection<Tournament> Tournaments { get; private set; } = new List<Tournament>();
}
```

### 步驟 4：建立並套用遷移
```bash
dotnet ef migrations add AddTournaments --project BoardGameAiDashboard.Infrastructure --startup-project BoardGameAiDashboard.Api
dotnet ef database update --project BoardGameAiDashboard.Infrastructure --startup-project BoardGameAiDashboard.Api
```

## 修改現有實體

### 新增欄位
```csharp
// 在 ApplicationDbContext.OnModelCreating 中
modelBuilder.Entity<Game>(entity =>
{
    // 新增屬性設定
    entity.Property(e => e.ImageUrl).HasMaxLength(500);
    
    // 新增索引以提升效能
    entity.HasIndex(e => e.Name);
});
```

### 新增遷移
```bash
dotnet ef migrations add AddGameImageUrl --project BoardGameAiDashboard.Infrastructure
```

## 效能模式

### 使用 AsNoTracking 進行唯讀查詢
```csharp
// 在 GenericRepository 中
public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
{
    return await _context.Set<T>()
        .AsNoTracking() // ✅ 效能優化
        .Where(e => EF.Property<bool>(e, "IsDeleted") == false)
        .ToListAsync(ct);
}
```

### 批次操作
```csharp
// 不要用迴圈 + 更新
// ❌ 錯誤
foreach (var item in items)
{
    item.Update(...);
    await _context.SaveChangesAsync();
}

// ✅ 正確 — 使用 ExecuteUpdateAsync
await _context.Games
    .Where(g => g.IsDeleted == false)
    .ExecuteUpdateAsync(s => s.SetProperty(g => g.UpdatedAt, DateTime.UtcNow));

// ✅ 正確 — 使用 ExecuteDeleteAsync
await _context.GameCards
    .Where(c => c.GameId == gameId)
    .ExecuteDeleteAsync();
```

## 常見問題

### 問題：遷移失敗，出現「無法為識別欄位插入明確值」
```csharp
// 解決方案：讓資料庫產生 ID
entity.Property(e => e.Id).ValueGeneratedOnAdd();
```

### 問題：查詢過濾器與明確過濾器衝突
```csharp
// 使用 IgnoreQueryFilters() 繞過全域過濾器
var deletedGame = await _context.Games
    .IgnoreQueryFilters()
    .FirstOrDefaultAsync(g => g.Id == id);
```

### 問題：關聯中的循環依賴
```csharp
// 解決方案：使用 owned type 或中斷循環
modelBuilder.Entity<Game>(entity =>
{
    entity.HasMany(g => g.Characters)
          .WithOne(c => c.Game)
          .HasForeignKey(c => c.GameId)
          .OnDelete(DeleteBehavior.Cascade);
});
```

## 測試實體變更

```csharp
[Fact]
public async Task GlobalQueryFilter_ExcludesSoftDeletedEntities()
{
    using var ctx = CreateContext();
    
    ctx.Games.Add(Game.Create("Active", "desc", 2, 4));
    var deleted = Game.Create("Deleted", "desc", 2, 4);
    ctx.Games.Add(deleted);
    await ctx.SaveChangesAsync();
    
    // 透過私有 setter 變通方式進行軟刪除
    ctx.Entry(deleted).Property<bool>("IsDeleted").CurrentValue = true;
    await ctx.SaveChangesAsync();
    
    // 查詢應該過濾掉已刪除的項目
    var games = await ctx.Games.ToListAsync();
    Assert.Single(games);
    Assert.Equal("Active", games[0].Name);
}
```
