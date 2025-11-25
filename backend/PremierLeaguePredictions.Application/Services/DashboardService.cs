using Microsoft.Extensions.Logging;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Core.Interfaces;

namespace PremierLeaguePredictions.Application.Services;

public class DashboardService : IDashboardService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(IUnitOfWork unitOfWork, ILogger<DashboardService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<DashboardDto> GetUserDashboardAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
        if (user == null) throw new KeyNotFoundException("User not found");

        // Check if user has approved participation for the active season (applies to all users including admins)
        var activeSeason = await _unitOfWork.Seasons.FindAsync(s => s.IsActive, cancellationToken);
        var activeSeasonId = activeSeason.FirstOrDefault()?.Id;

        if (activeSeasonId.HasValue)
        {
            var participation = await _unitOfWork.SeasonParticipations.FindAsync(
                sp => sp.UserId == userId &&
                      sp.SeasonId == activeSeasonId.Value &&
                      sp.IsApproved,
                cancellationToken);

            if (!participation.Any())
            {
                _logger.LogWarning("User {UserId} attempted to access dashboard without approved participation", userId);
                throw new UnauthorizedAccessException("You must be approved to participate in the current season");
            }
        }

        var picks = await _unitOfWork.Picks.FindAsync(p => p.UserId == userId, cancellationToken);
        var picksList = picks.ToList();

        // Get all gameweeks to determine which are completed
        var allGameweeks = await _unitOfWork.Gameweeks.GetAllAsync(cancellationToken);
        var completedGameweekIds = allGameweeks
            .Where(g => g.Deadline < DateTime.UtcNow)
            .Select(g => g.Id)
            .ToHashSet();

        // Only count W/D/L for picks in completed gameweeks
        var completedPicks = picksList.Where(p => completedGameweekIds.Contains(p.GameweekId)).ToList();

        int totalWins = 0;
        int totalDraws = 0;
        int totalLosses = 0;

        foreach (var pick in completedPicks)
        {
            if (pick.Points == 3) totalWins++;
            else if (pick.Points == 1) totalDraws++;
            else if (pick.Points == 0) totalLosses++;
        }

        var totalPoints = picksList.Sum(p => p.Points);
        var totalPicks = completedPicks.Count; // Only count completed picks

        // Get current/upcoming gameweeks
        // Current gameweek could be:
        // 1. In Progress: Deadline passed but not all fixtures finished
        // 2. Upcoming: Deadline not yet passed

        var now = DateTime.UtcNow;
        var allGameweeksOrdered = allGameweeks.OrderBy(g => g.WeekNumber).ToList();

        _logger.LogInformation("Current UTC time: {Now}, checking {GameweekCount} gameweeks for user {UserId}",
            now, allGameweeksOrdered.Count, userId);

        // Get all fixtures to check gameweek status
        var allFixtures = await _unitOfWork.Fixtures.GetAllAsync(cancellationToken);
        var fixturesByGameweek = allFixtures.GroupBy(f => f.GameweekId).ToDictionary(g => g.Key, g => g.ToList());

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
                if (fixturesByGameweek.TryGetValue(g.Id, out var fixtures))
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
                Id = inProgressGameweek.Id,
                SeasonId = inProgressGameweek.SeasonId,
                WeekNumber = inProgressGameweek.WeekNumber,
                Deadline = inProgressGameweek.Deadline,
                IsLocked = inProgressGameweek.IsLocked,
                Status = "InProgress"
            };

            // Add the in-progress gameweek as the first item in upcomingGameweeks
            upcomingGameweeksList.Add(currentGameweek);

            // Then add the next upcoming gameweeks
            var upcoming = await _unitOfWork.Gameweeks.FindAsync(
                g => !g.IsLocked && g.Deadline > now,
                cancellationToken);
            upcomingGameweeksList.AddRange(upcoming
                .OrderBy(g => g.Deadline)
                .Take(2) // Take 2 more since we already have the in-progress one
                .Select(g => new GameweekDto
                {
                    Id = g.Id,
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
            var upcoming = await _unitOfWork.Gameweeks.FindAsync(
                g => !g.IsLocked && g.Deadline > now,
                cancellationToken);
            upcomingGameweeksList = upcoming
                .OrderBy(g => g.Deadline)
                .Take(3)
                .Select(g => new GameweekDto
                {
                    Id = g.Id,
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
                GameweekId = p.GameweekId,
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
            CurrentGameweekId = currentGameweek?.Id,
            UpcomingGameweeks = upcomingGameweeksList,
            RecentPicks = recentPicks
        };
    }
}
