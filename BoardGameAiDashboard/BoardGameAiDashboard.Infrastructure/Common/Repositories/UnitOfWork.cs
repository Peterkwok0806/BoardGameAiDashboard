using BoardGameAiDashboard.Application.Common.Interfaces;
using BoardGameAiDashboard.Domain.Entities;
using BoardGameAiDashboard.Infrastructure.Persistence;

namespace BoardGameAiDashboard.Infrastructure.Common.Repositories;

/// <summary>
/// Unit of Work implementation that groups all repository "drawers"
/// and delegates persistence to a single <see cref="ApplicationDbContext.SaveChangesAsync"/>.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;

    private IGenericRepository<Game>? _games;
    private IGenericRepository<GameRuleChunk>? _rules;
    private IGenericRepository<GameCharacter>? _characters;
    private IGenericRepository<GameCard>? _cards;
    private IGenericRepository<MatchHistory>? _matches;
    private IGenericRepository<User>? _users;
    private IGenericRepository<RefreshToken>? _refreshTokens;

    public UnitOfWork(ApplicationDbContext context)
    {
        _context = context;
    } 

    /// <inheritdoc />
    public IGenericRepository<Game> Games =>
        _games ??= new GenericRepository<Game>(_context);

    /// <inheritdoc />
    public IGenericRepository<GameRuleChunk> Rules =>
        _rules ??= new GenericRepository<GameRuleChunk>(_context);

    /// <inheritdoc />
    public IGenericRepository<GameCharacter> Characters =>
        _characters ??= new GenericRepository<GameCharacter>(_context);

    /// <inheritdoc />
    public IGenericRepository<GameCard> Cards =>
        _cards ??= new GenericRepository<GameCard>(_context);

    /// <inheritdoc />
    public IGenericRepository<MatchHistory> Matches =>
        _matches ??= new GenericRepository<MatchHistory>(_context);

    /// <inheritdoc />
    public IGenericRepository<User> Users =>
        _users ??= new GenericRepository<User>(_context);

    /// <inheritdoc />
    public IGenericRepository<RefreshToken> RefreshTokens =>
        _refreshTokens ??= new GenericRepository<RefreshToken>(_context);

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
