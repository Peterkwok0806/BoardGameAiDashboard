using Microsoft.EntityFrameworkCore;
using BoardGameAiDashboard.Infrastructure.Persistence;
using BoardGameAiDashboard.Domain.Entities;

var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseInMemoryDatabase("test_" + Guid.NewGuid())
    .Options;

using var ctx = new ApplicationDbContext(options);

// Add a soft-deleted game
ctx.Games.Add(new Game { Name = "Deleted Game", Description = "test", IsDeleted = true, MinPlayers = 1, MaxPlayers = 2 });
ctx.Games.Add(new Game { Name = "Active Game", Description = "test", IsDeleted = false, MinPlayers = 1, MaxPlayers = 2 });
ctx.SaveChanges();

// Query - should filter out deleted
var games = ctx.Games.ToList();
Console.WriteLine($"Game count (should be 1): {games.Count}");
Console.WriteLine($"Games: {string.Join(", ", games.Select(g => g.Name))}");
Console.WriteLine($"Query: {ctx.Games.ToQueryString()}");
