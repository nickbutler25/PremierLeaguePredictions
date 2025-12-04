using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PremierLeaguePredictions.Core.Constants;
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
        PickRules = new Repository<PickRule>(_context);
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
    public IRepository<PickRule> PickRules { get; }

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

    public async Task<List<StandingsData>> GetStandingsDataAsync(string seasonId, List<Guid> approvedUserIds, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var standings = await _context.Users
            .Where(u => u.IsActive && approvedUserIds.Contains(u.Id))
            .Select(u => new
            {
                User = u,
                AllPicks = u.Picks.Where(p => p.SeasonId == seasonId),
                CompletedPicks = u.Picks.Where(p =>
                    p.SeasonId == seasonId &&
                    _context.Fixtures.Any(f =>
                        f.SeasonId == p.SeasonId &&
                        f.GameweekNumber == p.GameweekNumber &&
                        (f.HomeTeamId == p.TeamId || f.AwayTeamId == p.TeamId) &&
                        (f.Status == "FINISHED" || f.Status == "IN_PLAY" || f.Status == "PAUSED"))),
                Elimination = _context.UserEliminations
                    .Where(e => e.UserId == u.Id && e.SeasonId == seasonId)
                    .FirstOrDefault()
            })
            .Select(x => new StandingsData
            {
                UserId = x.User.Id,
                FirstName = x.User.FirstName,
                LastName = x.User.LastName,
                TotalPoints = x.AllPicks.Sum(p => p.Points),
                CompletedPicksCount = x.CompletedPicks.Count(),
                Wins = x.CompletedPicks.Count(p => p.Points == GameRules.PointsForWin),
                Draws = x.CompletedPicks.Count(p => p.Points == GameRules.PointsForDraw),
                Losses = x.CompletedPicks.Count(p => p.Points == GameRules.PointsForLoss),
                GoalsFor = x.CompletedPicks.Sum(p => p.GoalsFor),
                GoalsAgainst = x.CompletedPicks.Sum(p => p.GoalsAgainst),
                IsEliminated = x.Elimination != null,
                EliminationGameweek = x.Elimination != null ? (int?)x.Elimination.GameweekNumber : null,
                EliminationPosition = x.Elimination != null ? (int?)x.Elimination.Position : null
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return standings;
    }
}
