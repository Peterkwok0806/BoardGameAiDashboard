using BoardGameAiDashboard.Domain.Entities;
using BoardGameAiDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardGameAiDashboard.Tests;

public class RagServiceTests
{
    private ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task ChatMessage_SupportsConversationId()
    {
        using var ctx = CreateContext();
        var game = new Game("Test Game", "desc", 2, 4);
        ctx.Games.Add(game);
        await ctx.SaveChangesAsync();

        var convId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var userMsg = new ChatMessage(userId, game.Id, convId, "How do I win?", false, new());
        var assistantMsg = new ChatMessage(userId, game.Id, convId,
            "You win by scoring 10 points.", true, new List<string> { "RAG: Victory section" });

        ctx.ChatMessages.Add(userMsg);
        ctx.ChatMessages.Add(assistantMsg);
        await ctx.SaveChangesAsync();

        // Query by ConversationId
        var messages = await ctx.ChatMessages
            .Where(m => m.ConversationId == convId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        Assert.Equal(2, messages.Count);
        Assert.Equal(userId, messages[0].UserId);
        Assert.Equal("How do I win?", messages[0].Content);
        Assert.False(messages[0].IsFromAi);
        Assert.True(messages[1].IsFromAi);
        Assert.Single(messages[1].Sources);
        Assert.Equal("RAG: Victory section", messages[1].Sources[0]);
    }

    [Fact]
    public async Task ChatMessage_DifferentConversations_AreIsolated()
    {
        using var ctx = CreateContext();
        var game = new Game("Test", "desc", 2, 4);
        ctx.Games.Add(game);
        await ctx.SaveChangesAsync();

        var convA = Guid.NewGuid();
        var convB = Guid.NewGuid();
        var userId = Guid.NewGuid();

        ctx.ChatMessages.Add(new ChatMessage(userId, game.Id, convA, "Q1", false, new()));
        ctx.ChatMessages.Add(new ChatMessage(userId, game.Id, convA, "A1", true, new()));
        ctx.ChatMessages.Add(new ChatMessage(userId, game.Id, convB, "Q2", false, new()));
        await ctx.SaveChangesAsync();

        var messagesA = await ctx.ChatMessages
            .Where(m => m.ConversationId == convA)
            .ToListAsync();
        var messagesB = await ctx.ChatMessages
            .Where(m => m.ConversationId == convB)
            .ToListAsync();

        Assert.Equal(2, messagesA.Count);
        Assert.Single(messagesB);
    }

    [Fact]
    public async Task GameRuleChunk_CanBeCreatedAndQueried()
    {
        using var ctx = CreateContext();
        var game = new Game("Chess", "Classic", 2, 2);
        ctx.Games.Add(game);
        await ctx.SaveChangesAsync();

        var chunk1 = new GameRuleChunk(game.Id, "Pawns move forward one square.", "Movement", "qdrant-pt-1");
        var chunk2 = new GameRuleChunk(game.Id, "The king can move one square in any direction.", "Movement", "qdrant-pt-2");
        var chunk3 = new GameRuleChunk(game.Id, "Checkmate ends the game.", "Winning", "qdrant-pt-3");

        ctx.GameRuleChunks.AddRange(chunk1, chunk2, chunk3);
        await ctx.SaveChangesAsync();

        // Query chunks by game
        var chunks = await ctx.GameRuleChunks
            .Where(c => c.GameId == game.Id)
            .ToListAsync();

        Assert.Equal(3, chunks.Count);

        // Query by section title
        var movementChunks = await ctx.GameRuleChunks
            .Where(c => c.GameId == game.Id && c.SectionTitle == "Movement")
            .ToListAsync();

        Assert.Equal(2, movementChunks.Count);
    }

    [Fact]
    public async Task GameRuleChunk_SoftDeletedChunks_AreFiltered()
    {
        using var ctx = CreateContext();
        var game = new Game("Test", "desc", 2, 4);
        ctx.Games.Add(game);
        await ctx.SaveChangesAsync();

        var activeChunk = new GameRuleChunk(game.Id, "Active content", "Rules", "pt-active");
        var deletedChunk = new GameRuleChunk(game.Id, "Old content", "Rules", "pt-old");

        ctx.GameRuleChunks.Add(activeChunk);
        ctx.GameRuleChunks.Add(deletedChunk);
        await ctx.SaveChangesAsync();

        // Soft-delete one chunk
        ctx.Entry(deletedChunk).Property<bool>("IsDeleted").CurrentValue = true;
        await ctx.SaveChangesAsync();

        // Query filters out deleted
        var chunks = await ctx.GameRuleChunks.ToListAsync();
        Assert.Single(chunks);
        Assert.Equal("Active content", chunks[0].Content);
    }

    [Fact]
    public async Task GameRuleChunk_DeletedChunks_AreFilteredByQueryFilter()
    {
        using var ctx = CreateContext();
        var game = new Game("Test", "desc", 2, 4);
        ctx.Games.Add(game);
        await ctx.SaveChangesAsync();

        var chunk1 = new GameRuleChunk(game.Id, "Keep me", "s1", "pt-1");
        var chunk2 = new GameRuleChunk(game.Id, "Delete me", "s2", "pt-2");
        ctx.GameRuleChunks.AddRange(chunk1, chunk2);
        await ctx.SaveChangesAsync();

        // Soft-delete one chunk directly
        ctx.Entry(chunk2).Property<bool>("IsDeleted").CurrentValue = true;
        await ctx.SaveChangesAsync();

        // GameRuleChunk has its own IsDeleted query filter
        var visibleChunks = await ctx.GameRuleChunks.ToListAsync();
        Assert.Single(visibleChunks);
        Assert.Equal("Keep me", visibleChunks[0].Content);
    }

    [Fact]
    public async Task ChatMessage_GlobalQueryFilter_ExcludesDeletedMessages()
    {
        using var ctx = CreateContext();
        var game = new Game("Test", "desc", 2, 4);
        ctx.Games.Add(game);
        await ctx.SaveChangesAsync();

        var userId = Guid.NewGuid();
        var convId = Guid.NewGuid();

        var msg1 = new ChatMessage(userId, game.Id, convId, "visible", false, new());
        var msg2 = new ChatMessage(userId, game.Id, convId, "deleted", false, new());
        ctx.ChatMessages.AddRange(msg1, msg2);
        await ctx.SaveChangesAsync();

        // Soft-delete one message
        ctx.Entry(msg2).Property<bool>("IsDeleted").CurrentValue = true;
        await ctx.SaveChangesAsync();

        var messages = await ctx.ChatMessages.ToListAsync();
        Assert.Single(messages);
        Assert.Equal("visible", messages[0].Content);
    }
}
