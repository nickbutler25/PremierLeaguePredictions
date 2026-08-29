using PremierLeaguePredictions.Core.Entities;
using PremierLeaguePredictions.Core.Interfaces;

namespace PremierLeaguePredictions.Application.Services;

/// <summary>
/// Finds the gameweek whose picks are in play.
/// </summary>
/// <remarks>
/// Shared rather than reimplemented per service: the standings, the dashboard and the live
/// gameweek page all have to agree about which gameweek is current, and a second copy of this
/// rule is a second chance for them to disagree.
/// </remarks>
internal static class ActiveGameweekFinder
{
    /// <summary>
    /// The most recent gameweek whose deadline has passed and which still has a fixture
    /// unfinished or yet to kick off. Null once every fixture is played, or before the first
    /// deadline of the season.
    /// </summary>
    public static async Task<Gameweek?> FindAsync(
        IUnitOfWork unitOfWork, string seasonId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var candidates = (await unitOfWork.Gameweeks.FindAsync(
                g => g.SeasonId == seasonId && g.Deadline <= now,
                trackChanges: false, cancellationToken))
            .OrderByDescending(g => g.Deadline)
            .ToList();

        foreach (var gameweek in candidates)
        {
            var fixtures = await unitOfWork.Fixtures.FindAsync(
                f => f.SeasonId == seasonId && f.GameweekNumber == gameweek.WeekNumber,
                trackChanges: false, cancellationToken);

            var fixtureList = fixtures.ToList();
            if (fixtureList.Count == 0)
                continue;

            var stillRunning = fixtureList.Any(f => f.Status != "FINISHED") ||
                               fixtureList.Any(f => f.KickoffTime > now);

            if (stillRunning)
                return gameweek;

            // Deadlines are ordered, so once a gameweek is fully played every earlier one is too.
            break;
        }

        return null;
    }
}
