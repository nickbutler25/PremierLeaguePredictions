using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Core.Entities;

namespace PremierLeaguePredictions.Application.Services;

/// <summary>
/// Turns a pick into the shape the UI draws it in: a crest, and a result to colour it by.
/// Shared so the standings, the form guide and a player's profile all read a pick the same way.
/// </summary>
internal static class PickSummaryFactory
{
    public static PickSummaryDto? Build(
        int gameweekNumber, int teamId, IReadOnlyCollection<Fixture> fixtures, Dictionary<int, Team> teams)
    {
        if (!teams.TryGetValue(teamId, out var team))
            return null;

        var pick = new PickSummaryDto
        {
            GameweekNumber = gameweekNumber,
            TeamId = team.Id,
            TeamName = team.Name,
            TeamShortName = team.MediumName ?? team.Code,
            LogoUrl = team.LogoUrl
        };

        var fixture = fixtures.FirstOrDefault(f => f.HomeTeamId == teamId || f.AwayTeamId == teamId);
        if (fixture == null)
            return pick; // picked a team with no fixture this gameweek — nothing to report on

        var isHome = fixture.HomeTeamId == teamId;
        var opponentId = isHome ? fixture.AwayTeamId : fixture.HomeTeamId;
        pick.OpponentName = teams.TryGetValue(opponentId, out var opponent) ? opponent.Name : null;

        // PAUSED covers half time, where a score exists and the match is not over.
        var isLive = fixture.Status is "IN_PLAY" or "PAUSED";
        var isFinished = fixture.Status == "FINISHED";

        if (!isLive && !isFinished)
            return pick; // not kicked off — revealed, but no result to colour it by

        var teamScore = (isHome ? fixture.HomeScore : fixture.AwayScore) ?? 0;
        var opponentScore = (isHome ? fixture.AwayScore : fixture.HomeScore) ?? 0;

        pick.TeamScore = teamScore;
        pick.OpponentScore = opponentScore;
        pick.IsLive = isLive;
        pick.Outcome = teamScore > opponentScore ? PickOutcome.Win
                     : teamScore == opponentScore ? PickOutcome.Draw
                     : PickOutcome.Loss;

        return pick;
    }
}
