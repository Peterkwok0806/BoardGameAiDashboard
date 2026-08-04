---
name: dotnet-testing
description: 撰寫與執行 .NET 單元測試、使用 xUnit、Moq、EF Core InMemory 測試
---

## 專案測試架構

| 項目 | 路徑 |
|------|------|
| 測試專案 | `BoardGameAiDashboard/BoardGameAiDashboard.Tests/` |
| 框架 | xUnit 2.7.0 + Moq + Microsoft.EntityFrameworkCore.InMemory 8.0.0 |

## 執行指令

```bash
dotnet test                                    # 執行所有測試
dotnet test --filter "FullyQualifiedName~Tests"  # 執行特定類別
dotnet test --filter "FullyQualifiedName~MethodName"  # 執行特定方法
dotnet test /p:CollectCoverage=true            # 含覆蓋率
```

## xUnit 模式

### Fact vs Theory

```csharp
[Fact]
public void Game_Create_SetsProperties()
{
    var game = Game.Create("Catan", "Desc", 3, 4);
    Assert.Equal("Catan", game.Name);
    Assert.False(game.IsDeleted);
}

[Theory]
[InlineData(0, 2)]
[InlineData(-1, 4)]
public void Create_InvalidMinPlayers_Throws(int min, int max)
{
    Assert.Throws<ArgumentException>(() => Game.Create("Test", "Desc", min, max));
}
```

### 非同步測試

```csharp
[Fact]
public async Task GetByIdAsync_ReturnsGame_WhenExists()
{
    using var context = CreateDbContext();
    var game = Game.Create("Catan", "Desc", 3, 4);
    context.Games.Add(game);
    await context.SaveChangesAsync();

    var result = await _repository.GetByIdAsync(game.Id);
    Assert.NotNull(result);
}
```

## Moq Mock

```csharp
[Fact]
public async Task Handler_ReturnsGame_WhenExists()
{
    var gameId = Guid.NewGuid();
    var expectedGame = Game.Create("Catan", "Desc", 3, 4);

    var mockRepo = new Mock<IGameRepository>();
    mockRepo.Setup(r => r.GetByIdAsync(gameId, default))
        .ReturnsAsync(expectedGame);

    var mockUnitOfWork = new Mock<IUnitOfWork>();
    mockUnitOfWork.Setup(u => u.Games).Returns(mockRepo.Object);

    var handler = new GetGameHandler(mockUnitOfWork.Object);
    var result = await handler.Handle(new GetGameQuery(gameId), default);

    Assert.NotNull(result);
    mockRepo.Verify(r => r.GetByIdAsync(gameId, default), Times.Once);
}
```

### 常見設定

```csharp
mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
    .ReturnsAsync((Game?)null);

mockRepo.Setup(r => r.GetAllAsync(default))
    .ReturnsAsync(new List<Game>());

// 驗證
mockRepo.Verify(r => r.AddAsync(It.IsAny<Game>(), default), Times.Once);
mockUnitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
```

## EF Core InMemory

```csharp
public class GameRepositoryTests : IDisposable
{
    private readonly ApplicationDbContext _context;

    public GameRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
    }

    public void Dispose() => _context.Dispose();
}
```

### 軟刪除測試

```csharp
[Fact]
public async Task QueryFilter_ExcludesSoftDeleted()
{
    using var context = CreateDbContext();
    var active = Game.Create("Active", "Desc", 2, 4);
    var deleted = Game.Create("Deleted", "Desc", 2, 4);
    context.Games.AddRange(active, deleted);
    await context.SaveChangesAsync();

    // 軟刪除
    context.Entry(deleted).Property<bool>("IsDeleted").CurrentValue = true;
    await context.SaveChangesAsync();

    var games = await context.Games.ToListAsync();
    Assert.Single(games);
    Assert.Equal("Active", games[0].Name);
}

[Fact]
public async Task QueryFilter_IncludeDeleted_UsingIgnoreQueryFilters()
{
    var games = await context.Games
        .IgnoreQueryFilters()
        .ToListAsync();
    Assert.Equal(2, games.Count);
}
```

## 命名規範

```csharp
// Given_When_Then 模式
[Fact]
public void Create_WithValidInput_ReturnsNewGame() { }

[Fact]
public async Task GetById_WhenGameExists_ReturnsGame() { }

[Fact]
public void Create_WithNegativePlayers_ThrowsArgumentException() { }

// 類別命名：XxxTests
public class GameRepositoryTests { }
public class SoftDeleteTests { }
```

## FluentAssertions（建議）

```csharp
[Fact]
public void Game_Create_SetsAllProperties()
{
    var game = Game.Create("Catan", "Desc", 3, 4);

    game.Should().NotBeNull();
    game.Name.Should().Be("Catan");
    game.IsDeleted.Should().BeFalse();
}

[Fact]
public async Task GetById_ReturnsNull_WhenNotFound()
{
    var result = await _repository.GetByIdAsync(Guid.NewGuid());
    result.Should().BeNull();
}

[Fact]
public void Create_Invalid_ThrowsArgumentException()
{
    var action = () => Game.Create("", "Desc", 3, 4);
    action.Should().Throw<ArgumentException>();
}
```

## 測試 CQRS Handler

```csharp
[Fact]
public async Task CreateGameHandler_ValidCommand_ReturnsGameId()
{
    var mockUnitOfWork = new Mock<IUnitOfWork>();
    var mockRepo = new Mock<IGameRepository>();
    mockUnitOfWork.Setup(u => u.Games).Returns(mockRepo.Object);

    var handler = new CreateGameHandler(mockUnitOfWork.Object);
    var command = new CreateGameCommand("Catan", "Trading", 3, 4);

    var result = await handler.Handle(command, default);

    result.Value.Should().NotBeEmpty();
    mockRepo.Verify(r => r.AddAsync(It.IsAny<Game>(), default), Times.Once);
}

[Fact]
public async Task CreateGameHandler_InvalidCommand_ThrowsValidationException()
{
    var handler = new CreateGameHandler(_mockUnitOfWork.Object);
    var command = new CreateGameCommand("", "Desc", 3, 4);

    await Assert.ThrowsAsync<ValidationException>(() =>
        handler.Handle(command, default));
}
```

## 常見問題

### InMemory 不支援的功能
- 交易、ExecuteDelete、FromSqlRaw
- 解法：使用 SQLite InMemory 或 Testcontainers

### 測試隔離
```csharp
// ✅ 每個測試使用唯一資料庫
var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseInMemoryDatabase(Guid.NewGuid().ToString())  // 唯一名稱
    .Options;
```

## 覆蓋率

```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:coverage/coverage.cobertura.xml -targetdir:coverage/html
```
