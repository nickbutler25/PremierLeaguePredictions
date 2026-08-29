using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Core.Constants;
using PremierLeaguePredictions.Core.Entities;
using PremierLeaguePredictions.Core.Interfaces;

namespace PremierLeaguePredictions.Application.Services;

/// <inheritdoc cref="IUserProfileService"/>
public class UserProfileService : IUserProfileService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILeagueService _leagueService;
    private readonly IPickRuleService _pickRuleService;

    public UserProfileService(
        IUnitOfWork unitOfWork,
        ILeagueService leagueService,
        IPickRuleService pickRuleService)
    {
        _unitOfWork = unitOfWork;
        _leagueService = leagueService;
        _pickRuleService = pickRuleService;
    }

    public async Task<UserProfileDto?> GetUserProfileAsync(
        Guid userId, Guid viewerId, string? seasonId = null, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
            return null;

        seasonId ??= (await _unitOfWork.Seasons.FindAsync(s => s.IsActive, trackChanges: false, cancellationToken))
            .FirstOrDefault()?.Name;

        var profile = new UserProfileDto
        {
            UserId = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhotoUrl = user.PhotoUrl,
            SeasonId = seasonId ?? string.Empty
        };

        if (string.IsNullOrEmpty(seasonId))
            return profile; // no active season — the identity is all there is to show

        // The gameweeks whose picks have stopped being private. Everything below is built from
        // these and nothing else, so no pick still open to change can be read or inferred.
        var now = DateTime.UtcNow;
        var revealedGameweeks = (await _unitOfWork.Gameweeks.FindAsync(
                g => g.SeasonId == seasonId && g.Deadline <= now, trackChanges: false, cancellationToken))
            .Select(g => g.WeekNumber)
            .ToHashSet();

        await AttachStandingAsync(profile, userId, seasonId, cancellationToken);

        if (revealedGameweeks.Count == 0)
            return profile;

        var picks = (await _unitOfWork.Picks.FindAsync(
                p => p.SeasonId == seasonId && (p.UserId == userId || p.UserId == viewerId),
                trackChanges: false, cancellationToken))
            .Where(p => revealedGameweeks.Contains(p.GameweekNumber))
            .ToList();

        var playerPicks = picks
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.GameweekNumber)
            .ToList();

        var fixturesByGameweek = (await _unitOfWork.Fixtures.FindAsync(
                f => f.SeasonId == seasonId, trackChanges: false, cancellationToken))
            .Where(f => revealedGameweeks.Contains(f.GameweekNumber))
            .GroupBy(f => f.GameweekNumber)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<Fixture>)g.ToList());

        var teams = (await _unitOfWork.Teams.FindAsync(t => true, trackChanges: false, cancellationToken))
            .ToDictionary(t => t.Id);

        PickSummaryDto? Summarise(Pick pick) => PickSummaryFactory.Build(
            pick.GameweekNumber,
            pick.TeamId,
            fixturesByGameweek.TryGetValue(pick.GameweekNumber, out var f) ? f : Array.Empty<Fixture>(),
            teams);

        // The standings decide which gameweek is in progress and carry the pick for it, gated
        // the same way. Taking it from there keeps the profile and the table in step.
        profile.CurrentGameweek = profile.CurrentPick?.GameweekNumber;

        profile.PreviousPicks = playerPicks
            .Where(p => p.GameweekNumber != profile.CurrentGameweek)
            .Select(Summarise)
            .OfType<PickSummaryDto>()
            .ToList();

        var pickRules = await _pickRuleService.GetPickRulesForSeasonAsync(seasonId);
        profile.TeamUsage = TeamUsageBuilder.Build(playerPicks, teams, pickRules);

        if (viewerId != userId)
        {
            profile.HeadToHead = BuildHeadToHead(
                picks.Where(p => p.UserId == viewerId).ToList(), playerPicks, Summarise);
        }

        return profile;
    }

    /// <summary>
    /// Copies the player's row out of the league standings.
    /// </summary>
    /// <remarks>
    /// Reusing the standings rather than recounting means a profile can never disagree with the
    /// table, and it brings the correctly gated current pick with it. The standings are cached,
    /// so on a warm cache this costs nothing.
    /// </remarks>
    private async Task AttachStandingAsync(
        UserProfileDto profile, Guid userId, string seasonId, CancellationToken cancellationToken)
    {
        var standings = await _leagueService.GetLeagueStandingsAsync(seasonId, cancellationToken);
        var entry = standings.Standings.FirstOrDefault(s => s.UserId == userId);
        if (entry == null)
            return;

        profile.Standing = new UserStandingDto
        {
            Position = entry.Position,
            TotalPoints = entry.TotalPoints,
            PicksMade = entry.PicksMade,
            Wins = entry.Wins,
            Draws = entry.Draws,
            Losses = entry.Losses,
            GoalsFor = entry.GoalsFor,
            GoalsAgainst = entry.GoalsAgainst,
            GoalDifference = entry.GoalDifference,
            AveragePointsPerGame = entry.PicksMade == 0
                ? 0m
                : Math.Round((decimal)entry.TotalPoints / entry.PicksMade, 2),
            IsEliminated = entry.IsEliminated,
            EliminatedInGameweek = entry.EliminatedInGameweek
        };

        profile.CurrentPick = entry.CurrentPick;
    }


    /// <summary>
    /// The two players over the gameweeks both have a revealed pick for, and the points each
    /// took from the ones they saw differently.
    /// </summary>
    private static HeadToHeadDto BuildHeadToHead(
        List<Pick> viewerPicks, List<Pick> playerPicks, Func<Pick, PickSummaryDto?> summarise)
    {
        var viewerByGameweek = viewerPicks.ToDictionary(p => p.GameweekNumber);
        var headToHead = new HeadToHeadDto();

        foreach (var playerPick in playerPicks.OrderByDescending(p => p.GameweekNumber))
        {
            if (!viewerByGameweek.TryGetValue(playerPick.GameweekNumber, out var viewerPick))
                continue; // one of them has no pick that gameweek — nothing to compare

            headToHead.GameweeksCompared++;
            headToHead.ViewerPoints += viewerPick.Points;
            headToHead.PlayerPoints += playerPick.Points;

            if (viewerPick.TeamId == playerPick.TeamId)
            {
                headToHead.SamePickCount++;
                continue;
            }

            var viewerSummary = summarise(viewerPick);
            var playerSummary = summarise(playerPick);
            if (viewerSummary == null || playerSummary == null)
                continue;

            headToHead.Differences.Add(new HeadToHeadGameweekDto
            {
                GameweekNumber = playerPick.GameweekNumber,
                ViewerPick = viewerSummary,
                PlayerPick = playerSummary,
                ViewerPoints = viewerPick.Points,
                PlayerPoints = playerPick.Points
            });
        }

        return headToHead;
    }
}
