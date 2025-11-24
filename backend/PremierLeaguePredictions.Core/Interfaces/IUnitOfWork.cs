using PremierLeaguePredictions.Core.Entities;

namespace PremierLeaguePredictions.Core.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IRepository<User> Users { get; }
    IRepository<Season> Seasons { get; }
    IRepository<Team> Teams { get; }
    IRepository<Gameweek> Gameweeks { get; }
    IRepository<Fixture> Fixtures { get; }
    IRepository<Pick> Picks { get; }
    IRepository<TeamSelection> TeamSelections { get; }
    IRepository<EmailNotification> EmailNotifications { get; }
    IRepository<AdminAction> AdminActions { get; }
    IRepository<SeasonParticipation> SeasonParticipations { get; }
    IRepository<UserElimination> UserEliminations { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
