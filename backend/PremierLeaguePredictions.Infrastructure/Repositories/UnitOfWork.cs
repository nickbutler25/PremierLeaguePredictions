using Microsoft.EntityFrameworkCore.Storage;
using PremierLeaguePredictions.Core.Entities;
using PremierLeaguePredictions.Core.Interfaces;
using PremierLeaguePredictions.Infrastructure.Data;

namespace PremierLeaguePredictions.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    private IDbContextTransaction? _transaction;

    public UnitOfWork(ApplicationDbContext context)
    {
        _context = context;
        Users = new Repository<User>(_context);
        Seasons = new Repository<Season>(_context);
        Teams = new Repository<Team>(_context);
        Gameweeks = new Repository<Gameweek>(_context);
        Fixtures = new Repository<Fixture>(_context);
        Picks = new Repository<Pick>(_context);
        TeamSelections = new Repository<TeamSelection>(_context);
        EmailNotifications = new Repository<EmailNotification>(_context);
        AdminActions = new Repository<AdminAction>(_context);
        SeasonParticipations = new Repository<SeasonParticipation>(_context);
        UserEliminations = new Repository<UserElimination>(_context);
    }

    public IRepository<User> Users { get; }
    public IRepository<Season> Seasons { get; }
    public IRepository<Team> Teams { get; }
    public IRepository<Gameweek> Gameweeks { get; }
    public IRepository<Fixture> Fixtures { get; }
    public IRepository<Pick> Picks { get; }
    public IRepository<TeamSelection> TeamSelections { get; }
    public IRepository<EmailNotification> EmailNotifications { get; }
    public IRepository<AdminAction> AdminActions { get; }
    public IRepository<SeasonParticipation> SeasonParticipations { get; }
    public IRepository<UserElimination> UserEliminations { get; }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}
