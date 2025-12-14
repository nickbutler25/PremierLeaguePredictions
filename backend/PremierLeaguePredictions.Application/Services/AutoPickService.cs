using Microsoft.Extensions.Logging;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Core.Constants;
using PremierLeaguePredictions.Core.Entities;
using PremierLeaguePredictions.Core.Interfaces;

namespace PremierLeaguePredictions.Application.Services;

public class AutoPickService : IAutoPickService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly ILogger<AutoPickService> _logger;

    public AutoPickService(
        IUnitOfWork unitOfWork,
        INotificationService notificationService,
        ILogger<AutoPickService> logger)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<AutoPickResult> AssignMissedPicksForGameweekAsync(string seasonId, int gameweekNumber, CancellationToken cancellationToken = default)
    {
        var gameweek = await _unitOfWork.Gameweeks.FirstOrDefaultAsync(g => g.SeasonId == seasonId && g.WeekNumber == gameweekNumber, cancellationToken);
        if (gameweek == null)
        {
            _logger.LogWarning("Gameweek {SeasonId}-{GameweekNumber} not found", seasonId, gameweekNumber);
            throw new KeyNotFoundException($"Gameweek {seasonId}-{gameweekNumber} not found");
        }

        // Only process if deadline has passed and gameweek is still in progress (not locked)
        var now = DateTime.UtcNow;
        if (gameweek.Deadline >= now)
        {
            _logger.LogInformation("Gameweek {SeasonId}-{GameweekNumber} deadline has not passed yet (Deadline: {Deadline}, Now: {Now})",
                seasonId, gameweekNumber, gameweek.Deadline, now);
            throw new InvalidOperationException($"Gameweek {seasonId}-{gameweekNumber} deadline has not passed yet. Deadline is {gameweek.Deadline:u}");
        }

        if (gameweek.IsLocked)
        {
            _logger.LogInformation("Gameweek {SeasonId}-{GameweekNumber} is already locked and finalized",
                seasonId, gameweekNumber);
            throw new InvalidOperationException($"Gameweek {seasonId}-{gameweekNumber} is already locked and cannot have auto-picks assigned");
        }

        _logger.LogInformation("Processing auto-pick assignments for Gameweek {WeekNumber}", gameweek.WeekNumber);

        // Get all approved users for this season
        var seasonParticipations = await _unitOfWork.SeasonParticipations.FindAsync(
            sp => sp.SeasonId == gameweek.SeasonId && sp.IsApproved,
            cancellationToken);

        var approvedUserIds = seasonParticipations.Select(sp => sp.UserId).ToHashSet();

        // Get users who are already eliminated
        var eliminations = await _unitOfWork.UserEliminations.FindAsync(
            e => e.SeasonId == gameweek.SeasonId,
            cancellationToken);

        var eliminatedUserIds = eliminations.Select(e => e.UserId).ToHashSet();

        // Get existing picks for this gameweek
        var existingPicks = await _unitOfWork.Picks.FindAsync(
            p => p.SeasonId == seasonId && p.GameweekNumber == gameweekNumber,
            cancellationToken);

        var usersWithPicks = existingPicks.Select(p => p.UserId).ToHashSet();

        // Find users who need auto-picks (approved, not eliminated, no pick)
        var usersNeedingPicks = approvedUserIds
            .Where(userId => !eliminatedUserIds.Contains(userId) && !usersWithPicks.Contains(userId))
            .ToList();

        if (!usersNeedingPicks.Any())
        {
            _logger.LogInformation("No users need auto-pick assignments for Gameweek {WeekNumber}", gameweek.WeekNumber);
            return new AutoPickResult
            {
                PicksAssigned = 0,
                PicksFailed = 0,
                GameweeksProcessed = 1
            };
        }

        _logger.LogInformation("Found {Count} users needing auto-picks for Gameweek {WeekNumber}",
            usersNeedingPicks.Count, gameweek.WeekNumber);

        // Get team standings (calculate based on fixture results up to this gameweek)
        var teamStandings = await CalculateTeamStandingsAsync(gameweek.SeasonId, gameweek.WeekNumber, cancellationToken);

        _logger.LogDebug("Calculated standings for Gameweek {WeekNumber}: {@Standings}",
            gameweek.WeekNumber, teamStandings.Select(s => new { s.Position, s.TeamId, s.Points }));

        // Assign picks for each user
        int assignedCount = 0;
        int failedCount = 0;
        foreach (var userId in usersNeedingPicks)
        {
            try
            {
                var assignedTeam = await GetLowestAvailableTeamAsync(
                    userId,
                    gameweek.SeasonId,
                    gameweek.WeekNumber,
                    teamStandings,
                    cancellationToken);

                if (assignedTeam != null)
                {
                    var pick = new Pick
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        SeasonId = seasonId,
                        GameweekNumber = gameweekNumber,
                        TeamId = assignedTeam.Id,
                        Points = 0,
                        GoalsFor = 0,
                        GoalsAgainst = 0,
                        IsAutoAssigned = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    await _unitOfWork.Picks.AddAsync(pick, cancellationToken);
                    assignedCount++;

                    var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
                    _logger.LogInformation("Auto-assigned {TeamName} to {UserName} for Gameweek {WeekNumber}",
                        assignedTeam.Name, user != null ? $"{user.FirstName} {user.LastName}" : "Unknown", gameweek.WeekNumber);

                    // Send real-time notification to user
                    await _notificationService.SendAutoPickAssignedNotificationAsync(
                        userId,
                        assignedTeam.Name,
                        gameweek.WeekNumber);
                }
                else
                {
                    failedCount++;
                    _logger.LogError("Could not find available team for user {UserId} in Gameweek {WeekNumber}",
                        userId, gameweek.WeekNumber);
                }
            }
            catch (Exception ex)
            {
                failedCount++;
                _logger.LogError(ex, "Failed to auto-assign pick for user {UserId} in Gameweek {WeekNumber}",
                    userId, gameweek.WeekNumber);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Auto-assigned {AssignedCount} picks for Gameweek {WeekNumber}, {FailedCount} failed",
            assignedCount, gameweek.WeekNumber, failedCount);

        return new AutoPickResult
        {
            PicksAssigned = assignedCount,
            PicksFailed = failedCount,
            GameweeksProcessed = 1
        };
    }

    public async Task<AutoPickResult> AssignAllMissedPicksAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // Find gameweeks where the deadline has passed but the gameweek is still in progress (not locked)
        // This allows auto-pick to run at any time during the gameweek after the deadline
        var activeGameweeksWithPassedDeadlines = await _unitOfWork.Gameweeks.FindAsync(
            g => g.Deadline < now && !g.IsLocked,
            cancellationToken);

        var gameweeksList = activeGameweeksWithPassedDeadlines.OrderBy(g => g.Deadline).ToList();

        var totalPicksAssigned = 0;
        var totalPicksFailed = 0;
        var gameweeksProcessed = 0;

        if (gameweeksList.Any())
        {
            _logger.LogInformation("Processing auto-picks for {Count} gameweeks with passed deadlines that are still in progress",
                gameweeksList.Count);

            foreach (var gameweek in gameweeksList)
            {
                var result = await AssignMissedPicksForGameweekAsync(gameweek.SeasonId, gameweek.WeekNumber, cancellationToken);
                totalPicksAssigned += result.PicksAssigned;
                totalPicksFailed += result.PicksFailed;
                gameweeksProcessed += result.GameweeksProcessed;
            }
        }
        else
        {
            _logger.LogInformation("No in-progress gameweeks with passed deadlines found at {Now}", now);
        }

        return new AutoPickResult
        {
            PicksAssigned = totalPicksAssigned,
            PicksFailed = totalPicksFailed,
            GameweeksProcessed = gameweeksProcessed
        };
    }

    private async Task<List<TeamStanding>> CalculateTeamStandingsAsync(
        string seasonId,
        int upToWeekNumber,
        CancellationToken cancellationToken)
    {
        // Get all gameweeks up to the specified week
        var gameweeks = await _unitOfWork.Gameweeks.FindAsync(
            g => g.SeasonId == seasonId && g.WeekNumber < upToWeekNumber,
            cancellationToken);

        var gameweekKeys = gameweeks.Select(g => new { g.SeasonId, g.WeekNumber }).ToHashSet();

        // Get all finished fixtures from those gameweeks
        // Note: This is inefficient but necessary without a better query capability
        var allFixtures = await _unitOfWork.Fixtures.FindAsync(f => f.SeasonId == seasonId && f.Status == "FINISHED", cancellationToken);
        var fixtures = allFixtures.Where(f => gameweekKeys.Contains(new { f.SeasonId, WeekNumber = f.GameweekNumber }));

        var fixturesList = fixtures.ToList();

        // Calculate points for each team
        var teamStats = new Dictionary<int, TeamStanding>();

        foreach (var fixture in fixturesList)
        {
            if (!fixture.HomeScore.HasValue || !fixture.AwayScore.HasValue)
                continue;

            // Initialize team stats if needed
            if (!teamStats.ContainsKey(fixture.HomeTeamId))
            {
                teamStats[fixture.HomeTeamId] = new TeamStanding { TeamId = fixture.HomeTeamId };
            }
            if (!teamStats.ContainsKey(fixture.AwayTeamId))
            {
                teamStats[fixture.AwayTeamId] = new TeamStanding { TeamId = fixture.AwayTeamId };
            }

            var homeStats = teamStats[fixture.HomeTeamId];
            var awayStats = teamStats[fixture.AwayTeamId];

            homeStats.Played++;
            awayStats.Played++;

            homeStats.GoalsFor += fixture.HomeScore.Value;
            homeStats.GoalsAgainst += fixture.AwayScore.Value;
            awayStats.GoalsFor += fixture.AwayScore.Value;
            awayStats.GoalsAgainst += fixture.HomeScore.Value;

            if (fixture.HomeScore > fixture.AwayScore)
            {
                homeStats.Won++;
                homeStats.Points += 3;
                awayStats.Lost++;
            }
            else if (fixture.HomeScore < fixture.AwayScore)
            {
                awayStats.Won++;
                awayStats.Points += 3;
                homeStats.Lost++;
            }
            else
            {
                homeStats.Drawn++;
                homeStats.Points++;
                awayStats.Drawn++;
                awayStats.Points++;
            }
        }

        // Get all active teams and ensure they're in the standings
        var activeTeams = await _unitOfWork.Teams.FindAsync(
            t => t.IsActive,
            cancellationToken);

        var activeTeamsList = activeTeams.ToList();

        foreach (var team in activeTeamsList)
        {
            if (!teamStats.ContainsKey(team.Id))
            {
                teamStats[team.Id] = new TeamStanding { TeamId = team.Id, TeamName = team.Name };
            }
            else
            {
                teamStats[team.Id].TeamName = team.Name;
            }
        }

        // Sort by points DESC, then goal difference DESC, then goals for DESC, then team name ASC
        // When all teams have 0 points (start of season), teams are sorted A-Z
        // Arsenal (A) = Position 1, Wolverhampton (W) = Position 20 (worst)
        var standings = teamStats.Values
            .OrderByDescending(s => s.Points)
            .ThenByDescending(s => s.GoalDifference)
            .ThenByDescending(s => s.GoalsFor)
            .ThenBy(s => s.TeamName) // A comes before Z, so Arsenal ranks better than Wolves
            .ToList();

        // Assign positions
        for (int i = 0; i < standings.Count; i++)
        {
            standings[i].Position = i + 1;
        }

        return standings;
    }

    private async Task<Team?> GetLowestAvailableTeamAsync(
        Guid userId,
        string seasonId,
        int currentWeekNumber,
        List<TeamStanding> teamStandings,
        CancellationToken cancellationToken)
    {
        // Get teams already picked by user in the current half
        var userPicks = await _unitOfWork.Picks.FindAsync(
            p => p.UserId == userId,
            cancellationToken);

        // Get all gameweeks for this season
        var allGameweeks = await _unitOfWork.Gameweeks.FindAsync(
            g => g.SeasonId == seasonId,
            cancellationToken);

        var gameweeksList = allGameweeks.ToList();

        // Determine which half we're in
        int half = GameRules.GetHalfForGameweek(currentWeekNumber);
        int halfStartWeek = GameRules.GetHalfStart(half);
        int halfEndWeek = GameRules.GetHalfEnd(half);

        // Get gameweeks for current half
        var currentHalfGameweeks = gameweeksList
            .Where(g => g.WeekNumber >= halfStartWeek && g.WeekNumber <= halfEndWeek)
            .Select(g => new { g.SeasonId, GameweekNumber = g.WeekNumber })
            .ToHashSet();

        // Get teams already picked in this half
        var pickedTeamIds = userPicks
            .Where(p => currentHalfGameweeks.Contains(new { p.SeasonId, p.GameweekNumber }))
            .Select(p => p.TeamId)
            .ToHashSet();

        // Find the lowest ranked team not yet picked
        // Start from the bottom (highest position number = worst team)
        for (int i = teamStandings.Count - 1; i >= 0; i--)
        {
            var standing = teamStandings[i];
            if (!pickedTeamIds.Contains(standing.TeamId))
            {
                var team = await _unitOfWork.Teams.FirstOrDefaultAsync(t => t.Id == standing.TeamId, cancellationToken);
                if (team != null && team.IsActive)
                {
                    _logger.LogDebug("Selected team {TeamName} (Position {Position}, Points {Points}) for auto-pick",
                        team.Name, standing.Position, standing.Points);
                    return team;
                }
            }
        }

        _logger.LogWarning("No available team found for user {UserId} - all teams may be picked", userId);
        return null;
    }

    private class TeamStanding
    {
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public int Position { get; set; }
        public int Played { get; set; }
        public int Won { get; set; }
        public int Drawn { get; set; }
        public int Lost { get; set; }
        public int GoalsFor { get; set; }
        public int GoalsAgainst { get; set; }
        public int GoalDifference => GoalsFor - GoalsAgainst;
        public int Points { get; set; }
    }
}
