using Microsoft.Extensions.Logging;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Core.Constants;
using PremierLeaguePredictions.Core.Interfaces;

namespace PremierLeaguePredictions.Application.Services;

public class DashboardService : IDashboardService
{
    /// <summary>
    /// Statuses that mean a fixture has a score, so a pick on it counts. Matches
    /// <see cref="LeagueService"/> and the standings query — a pick is played or it is not, and
    /// two screens must not disagree about which.
    /// </summary>
    private static readonly HashSet<string> ScoringStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "FINISHED", "IN_PLAY", "PAUSED" };

    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(IUnitOfWork unitOfWork, ILogger<DashboardService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<DashboardDto> GetUserDashboardAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId, trackChanges: false, cancellationToken);
        if (user == null) throw new KeyNotFoundException("User not found");

        var activeSeason = await _unitOfWork.Seasons.FindAsync(s => s.IsActive, trackChanges: false, cancellationToken);
        var activeSeasonId = activeSeason.FirstOrDefault()?.Name;

        if (!string.IsNullOrEmpty(activeSeasonId) && !user.IsAdmin)
        {
            var participation = await _unitOfWork.SeasonParticipations.FindAsync(
                sp => sp.UserId.Equals(userId) &&
                      sp.SeasonId == activeSeasonId &&
                      sp.IsApproved,
                trackChanges: false,
                cancellationToken);

            if (!participation.Any())
            {
                _logger.LogWarning("User {UserId} attempted to access dashboard without approved participation", userId);
                throw new UnauthorizedAccessException("You must be approved to participate in the current season");
            }
        }

        var picks = await _unitOfWork.Picks.FindAsync(p => p.UserId == userId, trackChanges: false, cancellationToken);
        var picksList = picks.ToList();

        var allGameweeks = await _unitOfWork.Gameweeks.GetAllAsync(trackChanges: false, cancellationToken);
        var allFixtures = await _unitOfWork.Fixtures.GetAllAsync(trackChanges: false, cancellationToken);

        // A pick counts once its own fixture has a score — not merely because the deadline has
        // passed. Those are different moments: a Saturday deadline with a Monday kickoff leaves
        // a pick locked but unplayed for two days, and counting it then scored a nil-nothing as
        // a defeat and inflated the picks-made count. This is the same rule the standings use
        // for picks made, so the two now agree about what has been played.
        var playedPicks = allFixtures
            .Where(f => ScoringStatuses.Contains(f.Status))
            .SelectMany(f => new[]
            {
                (f.SeasonId, f.GameweekNumber, TeamId: f.HomeTeamId),
                (f.SeasonId, f.GameweekNumber, TeamId: f.AwayTeamId)
            })
            .ToHashSet();

        var completedPicks = picksList
            .Where(p => playedPicks.Contains((p.SeasonId, p.GameweekNumber, p.TeamId)))
            .ToList();

        int totalWins = 0;
        int totalDraws = 0;
        int totalLosses = 0;

        foreach (var pick in completedPicks)
        {
            if (pick.Points == GameRules.PointsForWin) totalWins++;
            else if (pick.Points == GameRules.PointsForDraw) totalDraws++;
            else if (pick.Points == GameRules.PointsForLoss) totalLosses++;
        }

        var totalPoints = picksList.Sum(p => p.Points);
        var totalPicks = completedPicks.Count; // Only count picks whose fixture has been played

        // Get current/upcoming gameweeks
        // Current gameweek could be:
        // 1. In Progress: Deadline passed but not all fixtures finished
        // 2. Upcoming: Deadline not yet passed

        var now = DateTime.UtcNow;
        var allGameweeksOrdered = allGameweeks.OrderBy(g => g.WeekNumber).ToList();

        _logger.LogInformation("Current UTC time: {Now}, checking {GameweekCount} gameweeks for user {UserId}",
            now, allGameweeksOrdered.Count, userId);

        // Fixtures were loaded above for the played-picks check; grouped here for gameweek status.
        var fixturesByGameweek = allFixtures.GroupBy(f => new { f.SeasonId, GameweekNumber = f.GameweekNumber }).ToDictionary(g => g.Key, g => g.ToList());

        GameweekDto? currentGameweek = null;
        var upcomingGameweeksList = new List<GameweekDto>();

        // First, check if there's a gameweek in progress (deadline passed but fixtures not all finished)
        // Strategy: Find the most recent gameweek whose deadline has passed and still has future or in-progress fixtures
        var inProgressGameweek = allGameweeksOrdered
            .Where(g => g.Deadline < now)
            .OrderByDescending(g => g.WeekNumber)
            .FirstOrDefault(g =>
            {
                // Check if this gameweek has any fixtures that are not finished
                if (fixturesByGameweek.TryGetValue(new { g.SeasonId, GameweekNumber = g.WeekNumber }, out var fixtures))
                {
                    // Consider a fixture "finished" if:
                    // 1. Status is explicitly FINISHED, CANCELLED, or POSTPONED, OR
                    // 2. Kickoff time + 3 hours has passed (safe buffer for any match)
                    var gameLength = TimeSpan.FromHours(3);

                    var hasUnfinishedFixtures = fixtures.Any(f =>
                    {
                        var isExplicitlyFinished = f.Status == "FINISHED" ||
                                                   f.Status == "CANCELLED" ||
                                                   f.Status == "POSTPONED";

                        if (isExplicitlyFinished) return false; // This fixture is done

                        // If kickoff time + game length hasn't passed, it's still in progress
                        var expectedEndTime = f.KickoffTime.Add(gameLength);
                        return expectedEndTime > now;
                    });

                    // Also check if there are any fixtures with future kickoff times (not yet played)
                    var hasFutureFixtures = fixtures.Any(f => f.KickoffTime > now);

                    _logger.LogInformation("GW {WeekNumber} (deadline: {Deadline}, locked: {IsLocked}): {FixtureCount} fixtures, unfinished: {HasUnfinished}, future: {HasFuture}, statuses: {Statuses}",
                        g.WeekNumber, g.Deadline, g.IsLocked, fixtures.Count, hasUnfinishedFixtures, hasFutureFixtures, string.Join(", ", fixtures.Select(f => f.Status)));

                    return hasUnfinishedFixtures || hasFutureFixtures;
                }
                _logger.LogInformation("GW {WeekNumber} (deadline: {Deadline}, locked: {IsLocked}): No fixtures found", g.WeekNumber, g.Deadline, g.IsLocked);
                return false;
            });

        if (inProgressGameweek != null)
        {
            _logger.LogInformation("Found in-progress gameweek: GW {WeekNumber}", inProgressGameweek.WeekNumber);
            currentGameweek = new GameweekDto
            {
                SeasonId = inProgressGameweek.SeasonId,
                WeekNumber = inProgressGameweek.WeekNumber,
                Deadline = inProgressGameweek.Deadline,
                IsLocked = inProgressGameweek.IsLocked,
                Status = "InProgress"
            };

            // Add the in-progress gameweek as the first item in upcomingGameweeks
            upcomingGameweeksList.Add(currentGameweek);

            // Then add the next upcoming gameweeks
            var upcoming = allGameweeks.Where(g => !g.IsLocked && g.Deadline > now);
            upcomingGameweeksList.AddRange(upcoming
                .OrderBy(g => g.Deadline)
                .Take(2) // Take 2 more since we already have the in-progress one
                .Select(g => new GameweekDto
                {
                    SeasonId = g.SeasonId,
                    WeekNumber = g.WeekNumber,
                    Deadline = g.Deadline,
                    IsLocked = g.IsLocked,
                    Status = "Upcoming"
                }));
        }
        else
        {
            _logger.LogInformation("No in-progress gameweek found, looking for upcoming");
            // No gameweek in progress, so get the next upcoming one
            var upcoming = allGameweeks.Where(g => !g.IsLocked && g.Deadline > now);
            upcomingGameweeksList = upcoming
                .OrderBy(g => g.Deadline)
                .Take(3)
                .Select(g => new GameweekDto
                {
                    SeasonId = g.SeasonId,
                    WeekNumber = g.WeekNumber,
                    Deadline = g.Deadline,
                    IsLocked = g.IsLocked,
                    Status = "Upcoming"
                })
                .ToList();

            currentGameweek = upcomingGameweeksList.FirstOrDefault();
        }

        // Get recent picks
        var recentPicks = picksList
            .OrderByDescending(p => p.CreatedAt)
            .Take(5)
            .Select(p => new PickDto
            {
                Id = p.Id,
                UserId = p.UserId,
                SeasonId = p.SeasonId,
                GameweekNumber = p.GameweekNumber,
                TeamId = p.TeamId,
                Points = p.Points,
                GoalsFor = p.GoalsFor,
                GoalsAgainst = p.GoalsAgainst,
                IsAutoAssigned = p.IsAutoAssigned
            })
            .ToList();

        return new DashboardDto
        {
            User = new UserStatsDto
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                TotalPoints = totalPoints,
                TotalPicks = totalPicks,
                TotalWins = totalWins,
                TotalDraws = totalDraws,
                TotalLosses = totalLosses
            },
            CurrentGameweek = currentGameweek,
            UpcomingGameweeks = upcomingGameweeksList,
            RecentPicks = recentPicks
        };
    }
}
