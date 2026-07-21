using BoardGameAiDashboard.Domain.Entities;

namespace BoardGameAiDashboard.Application.Common.Interfaces;

/// <summary>
/// Lightweight unit-of-work that groups all repository "drawers"
/// and exposes a single <see cref="SaveChangesAsync"/> to persist changes.
/// No explicit transaction management — keep it simple for a RAG dashboard.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>Games repository.</summary>
    IGenericRepository<Game> Games { get; }

    /// <summary>Game rule chunks repository (RAG document source).</summary>
    IGenericRepository<GameRuleChunk> Rules { get; }

    /// <summary>Game characters repository.</summary>
    IGenericRepository<GameCharacter> Characters { get; }

    /// <summary>Game cards repository.</summary>
    IGenericRepository<GameCard> Cards { get; }

    /// <summary>Match history repository (ML.NET feature data).</summary>
    IGenericRepository<MatchHistory> Matches { get; }

    /// <summary>User repository (authentication).</summary>
    IGenericRepository<User> Users { get; }

    /// <summary>Refresh token repository (JWT refresh flow).</summary>
    IGenericRepository<RefreshToken> RefreshTokens { get; }

    /// <summary>
    /// Persist all pending changes across all repositories in a single batch.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of affected rows.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
