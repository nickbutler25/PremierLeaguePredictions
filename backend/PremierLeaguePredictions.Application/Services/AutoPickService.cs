using Microsoft.Extensions.Logging;
using PremierLeaguePredictions.Application.Interfaces;
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

    public async Task AssignMissedPicksForGameweekAsync(Guid gameweekId, CancellationToken cancellationToken = default)
    {
        var gameweek = await _unitOfWork.Gameweeks.GetByIdAsync(gameweekId, cancellationToken);
        if (gameweek == null)
        {
            _logger.LogWarning("Gameweek {GameweekId} not found", gameweekId);
            return;
        }

        // Only process if deadline has passed
        if (gameweek.Deadline >= DateTime.UtcNow)
        {
            _logger.LogDebug("Gameweek {WeekNumber} deadline has not passed yet", gameweek.WeekNumber);
            return;
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
            p => p.GameweekId == gameweekId,
            cancellationToken);

        var usersWithPicks = existingPicks.Select(p => p.UserId).ToHashSet();

        // Find users who need auto-picks (approved, not eliminated, no pick)
        var usersNeedingPicks = approvedUserIds
            .Where(userId => !eliminatedUserIds.Contains(userId) && !usersWithPicks.Contains(userId))
            .ToList();

        if (!usersNeedingPicks.Any())
        {
            _logger.LogInformation("No users need auto-pick assignments for Gameweek {WeekNumber}", gameweek.WeekNumber);
            return;
        }

        _logger.LogInformation("Found {Count} users needing auto-picks for Gameweek {WeekNumber}",
            usersNeedingPicks.Count, gameweek.WeekNumber);

        // Get team standings (calculate based on fixture results up to this gameweek)
        var teamStandings = await CalculateTeamStandingsAsync(gameweek.SeasonId, gameweek.WeekNumber, cancellationToken);

        // Assign picks for each user
        int assignedCount = 0;
        foreach (var userId in usersNeedingPicks)
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
                    GameweekId = gameweekId,
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
                _logger.LogWarning("Could not find available team for user {UserId} in Gameweek {WeekNumber}",
                    userId, gameweek.WeekNumber);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Auto-assigned {AssignedCount} picks for Gameweek {WeekNumber}",
            assignedCount, gameweek.WeekNumber);
    }

    public async Task AssignAllMissedPicksAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // Get all gameweeks with passed deadlines
        var gameweeksWithPassedDeadlines = await _unitOfWork.Gameweeks.FindAsync(
            g => g.Deadline < now,
            cancellationToken);

        var gameweeksList = gameweeksWithPassedDeadlines.OrderBy(g => g.Deadline).ToList();

        _logger.LogInformation("Processing auto-picks for {Count} gameweeks with passed deadlines",
            gameweeksList.Count);

        foreach (var gameweek in gameweeksList)
        {
            await AssignMissedPicksForGameweekAsync(gameweek.Id, cancellationToken);
        }
    }

    private async Task<List<TeamStanding>> CalculateTeamStandingsAsync(
        Guid seasonId,
        int upToWeekNumber,
        CancellationToken cancellationToken)
    {
        // Get all gameweeks up to the specified week
        var gameweeks = await _unitOfWork.Gameweeks.FindAsync(
            g => g.SeasonId == seasonId && g.WeekNumber < upToWeekNumber,
            cancellationToken);

        var gameweekIds = gameweeks.Select(g => g.Id).ToHashSet();

        // Get all finished fixtures from those gameweeks
        var fixtures = await _unitOfWork.Fixtures.FindAsync(
            f => gameweekIds.Contains(f.GameweekId) && f.Status == "FINISHED",
            cancellationToken);

        var fixturesList = fixtures.ToList();

        // Calculate points for each team
        var teamStats = new Dictionary<Guid, TeamStanding>();

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

        foreach (var team in activeTeams)
        {
            if (!teamStats.ContainsKey(team.Id))
            {
                teamStats[team.Id] = new TeamStanding { TeamId = team.Id };
            }
        }

        // Sort by points, then goal difference, then goals for
        var standings = teamStats.Values
            .OrderByDescending(s => s.Points)
            .ThenByDescending(s => s.GoalDifference)
            .ThenByDescending(s => s.GoalsFor)
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
        Guid seasonId,
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
        int half = currentWeekNumber <= 20 ? 1 : 2;
        int halfStartWeek = half == 1 ? 1 : 21;
        int halfEndWeek = half == 1 ? 20 : 38;

        // Get gameweeks for current half
        var currentHalfGameweeks = gameweeksList
            .Where(g => g.WeekNumber >= halfStartWeek && g.WeekNumber <= halfEndWeek)
            .Select(g => g.Id)
            .ToHashSet();

        // Get teams already picked in this half
        var pickedTeamIds = userPicks
            .Where(p => currentHalfGameweeks.Contains(p.GameweekId))
            .Select(p => p.TeamId)
            .ToHashSet();

        // Find the lowest ranked team not yet picked
        // Start from the bottom (highest position number)
        for (int i = teamStandings.Count - 1; i >= 0; i--)
        {
            var standing = teamStandings[i];
            if (!pickedTeamIds.Contains(standing.TeamId))
            {
                var team = await _unitOfWork.Teams.GetByIdAsync(standing.TeamId, cancellationToken);
                if (team != null && team.IsActive)
                {
                    return team;
                }
            }
        }

        return null;
    }

    private class TeamStanding
    {
        public Guid TeamId { get; set; }
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
