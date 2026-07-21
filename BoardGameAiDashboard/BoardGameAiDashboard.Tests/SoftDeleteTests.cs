using BoardGameAiDashboard.Domain.Entities;
using BoardGameAiDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardGameAiDashboard.Tests;

public class SoftDeleteTests
{
    private ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GlobalQueryFilter_ExcludesSoftDeletedGames()
    {
        using var ctx = CreateContext();

        ctx.Games.Add(new Game("Active Game", "desc", 2, 4));
        var deleted = new Game("Deleted Game", "desc", 2, 4);
        ctx.Games.Add(deleted);
        await ctx.SaveChangesAsync();

        // Soft delete manually (IsDeleted is private set)
        ctx.Entry(deleted).Property<bool>("IsDeleted").CurrentValue = true;
        await ctx.SaveChangesAsync();

        // Query should filter out deleted
        var games = await ctx.Games.ToListAsync();
        Assert.Single(games);
        Assert.Equal("Active Game", games[0].Name);
    }

    [Fact]
    public async Task GlobalQueryFilter_ExcludesSoftDeletedGameCards()
    {
        using var ctx = CreateContext();
        var game = new Game("Test", "desc", 2, 4);
        ctx.Games.Add(game);
        await ctx.SaveChangesAsync();

        var card = new GameCard(game.Id, "Code", "Name", "Desc", new Dictionary<string, string>());
        ctx.GameCards.Add(card);
        await ctx.SaveChangesAsync();

        ctx.Entry(card).Property<bool>("IsDeleted").CurrentValue = true;
        await ctx.SaveChangesAsync();

        var cards = await ctx.GameCards.ToListAsync();
        Assert.Empty(cards);
    }

    [Fact]
    public async Task GlobalQueryFilter_ExcludesSoftDeletedGameCharacters()
    {
        using var ctx = CreateContext();
        var game = new Game("Test", "desc", 2, 4);
        ctx.Games.Add(game);
        await ctx.SaveChangesAsync();

        var character = new GameCharacter(game.Id, "Code", "Name", "Skill", new Dictionary<string, string>());
        ctx.GameCharacters.Add(character);
        await ctx.SaveChangesAsync();

        ctx.Entry(character).Property<bool>("IsDeleted").CurrentValue = true;
        await ctx.SaveChangesAsync();

        var characters = await ctx.GameCharacters.ToListAsync();
        Assert.Empty(characters);
    }

    [Fact]
    public async Task GlobalQueryFilter_ExcludesSoftDeletedGameRuleChunks()
    {
        using var ctx = CreateContext();
        var game = new Game("Test", "desc", 2, 4);
        ctx.Games.Add(game);
        await ctx.SaveChangesAsync();

        var chunk = new GameRuleChunk(game.Id, "Section", "Content", "point-1");
        ctx.GameRuleChunks.Add(chunk);
        await ctx.SaveChangesAsync();

        ctx.Entry(chunk).Property<bool>("IsDeleted").CurrentValue = true;
        await ctx.SaveChangesAsync();

        var chunks = await ctx.GameRuleChunks.ToListAsync();
        Assert.Empty(chunks);
    }

    [Fact]
    public async Task GlobalQueryFilter_ExcludesSoftDeletedMatchHistories()
    {
        using var ctx = CreateContext();
        var game = new Game("Test", "desc", 2, 4);
        ctx.Games.Add(game);
        await ctx.SaveChangesAsync();

        var match = new MatchHistory(game.Id, 4, true, new Dictionary<string, string>());
        ctx.MatchHistories.Add(match);
        await ctx.SaveChangesAsync();

        ctx.Entry(match).Property<bool>("IsDeleted").CurrentValue = true;
        await ctx.SaveChangesAsync();

        var matches = await ctx.MatchHistories.ToListAsync();
        Assert.Empty(matches);
    }
}
