namespace PremierLeaguePredictions.Application.Interfaces;

public interface IAutoPickService
{
    /// <summary>
    /// Auto-assign picks for users who missed the deadline for a specific gameweek
    /// </summary>
    Task AssignMissedPicksForGameweekAsync(string seasonId, int gameweekNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Auto-assign picks for all gameweeks with passed deadlines
    /// </summary>
    Task AssignAllMissedPicksAsync(CancellationToken cancellationToken = default);
}
