using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Core.Constants;
using PremierLeaguePredictions.Core.Entities;

namespace PremierLeaguePredictions.Application.Services;

/// <summary>
/// Which teams a player has spent and which they have left for the half the season has reached.
/// </summary>
/// <remarks>
/// Shared by the profile page and the live gameweek page so both count a team as spent on the
/// same rule. Callers pass revealed picks only: a pick for a gameweek still open would show up
/// as a team missing from the available list, which gives it away as plainly as naming it.
/// </remarks>
internal static class TeamUsageBuilder
{
    public static TeamUsageDto Build(
        IReadOnlyCollection<Pick> revealedPicks,
        IReadOnlyDictionary<int, Team> teams,
        PickRulesResponse rules,
        int? forGameweek = null)
    {
        // The half is taken from the gameweek being looked at where the caller knows it, and
        // otherwise from the latest revealed pick. Falling back to the first gameweek keeps a
        // player with no picks yet in the first half rather than out of both.
        var referenceGameweek = forGameweek
            ?? (revealedPicks.Count > 0 ? revealedPicks.Max(p => p.GameweekNumber) : GameRules.FirstHalfStart);

        var half = GameRules.GetHalfForGameweek(referenceGameweek);
        var firstGameweek = GameRules.GetHalfStart(half);
        var lastGameweek = GameRules.GetHalfEnd(half);

        var rule = half == GameRules.FirstHalf ? rules.FirstHalf : rules.SecondHalf;
        var maxPerTeam = rule?.MaxTimesTeamCanBePicked ?? 1;

        var timesPicked = revealedPicks
            .Where(p => p.GameweekNumber >= firstGameweek && p.GameweekNumber <= lastGameweek)
            .GroupBy(p => p.TeamId)
            .ToDictionary(g => g.Key, g => g.Count());

        var usage = new TeamUsageDto
        {
            Half = half,
            FirstGameweek = firstGameweek,
            LastGameweek = lastGameweek,
            MaxTimesTeamCanBePicked = maxPerTeam
        };

        foreach (var team in teams.Values.Where(t => t.IsActive).OrderBy(t => t.Name))
        {
            var picked = timesPicked.TryGetValue(team.Id, out var count) ? count : 0;
            var entry = new TeamUsageEntryDto
            {
                TeamId = team.Id,
                TeamName = team.Name,
                TeamShortName = team.MediumName ?? team.Code,
                LogoUrl = team.LogoUrl,
                TimesPicked = picked
            };

            // Spent means picked as many times as the rules allow, not picked at all — which is
            // what makes the second half's allowance work without a special case here.
            if (picked >= maxPerTeam)
                usage.Used.Add(entry);
            else
                usage.Available.Add(entry);
        }

        return usage;
    }
}
