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
    IRepository<PickRule> PickRules { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);

    // Specialized query for league standings optimization
    Task<List<StandingsData>> GetStandingsDataAsync(string seasonId, List<Guid> approvedUserIds, CancellationToken cancellationToken = default);
}

// DTO for standings query result
public class StandingsData
{
    public Guid UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int TotalPoints { get; set; }
    public int CompletedPicksCount { get; set; }
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public bool IsEliminated { get; set; }
    public int? EliminationGameweek { get; set; }
    public int? EliminationPosition { get; set; }
}
