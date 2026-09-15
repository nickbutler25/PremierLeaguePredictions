using Microsoft.Extensions.Configuration;
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
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AutoPickService> _logger;

    public AutoPickService(
        IUnitOfWork unitOfWork,
        INotificationService notificationService,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<AutoPickService> logger)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// How far back a run will reach for an unassigned pick. Comfortably clears a normal
    /// gameweek (deadline Friday, last fixture Monday) without reaching the one before it.
    /// </summary>
    private static readonly TimeSpan MaxAutoPickAge = TimeSpan.FromDays(14);

    /// <summary>One assignment, held back until the picks are safely saved.</summary>
    private sealed record Assignment(Guid UserId, string TeamName);

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
        var assignments = new List<Assignment>();
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
                    assignments.Add(new Assignment(userId, assignedTeam.Name));

                    _logger.LogInformation("Auto-assigned {TeamName} to {UserId} for Gameweek {WeekNumber}",
                        assignedTeam.Name, userId, gameweek.WeekNumber);
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

        // Only once the picks are actually persisted. Telling a player which team they have been
        // given and then failing to save it is worse than telling them nothing.
        await NotifyAssignedPlayersAsync(gameweek, assignments, cancellationToken);

        return new AutoPickResult
        {
            PicksAssigned = assignedCount,
            PicksFailed = failedCount,
            GameweeksProcessed = 1
        };
    }

    /// <summary>
    /// Tells each player which team they were given, and why — live on the site, and by email.
    /// </summary>
    /// <remarks>
    /// Both happen only after the picks are committed. The SignalR notification used to be sent
    /// inside the assignment loop, before <c>SaveChangesAsync</c>: a client that acted on it
    /// refetched and read the database before the transaction landed, got back its old
    /// pickless state and cached that for five minutes — so the pick did not appear until the
    /// page was reloaded by hand, which is exactly the bug this ordering fixes.
    ///
    /// The email exists because the SignalR event only reaches someone who happens to have the
    /// site open at the moment the deadline passes, which is nobody's normal Saturday.
    ///
    /// Emails go in one batch: auto-pick runs for the whole field at once, and at a few hundred
    /// players one request per recipient outlives cron-job.org's 30s request timeout long
    /// before the last is attempted. Failures are logged, never thrown — a mailer problem must
    /// not fail a run whose picks are already saved.
    /// </remarks>
    private async Task NotifyAssignedPlayersAsync(
        Gameweek gameweek, List<Assignment> assignments, CancellationToken cancellationToken)
    {
        if (assignments.Count == 0)
            return;

        foreach (var assignment in assignments)
        {
            try
            {
                await _notificationService.SendAutoPickAssignedNotificationAsync(
                    assignment.UserId, assignment.TeamName, gameweek.WeekNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Could not push the auto-pick notification to user {UserId} for GW{WeekNumber}",
                    assignment.UserId, gameweek.WeekNumber);
            }
        }

        var userIds = assignments.Select(a => a.UserId).ToList();
        var users = (await _unitOfWork.Users.FindAsync(
                u => userIds.Contains(u.Id), trackChanges: false, cancellationToken))
            .ToDictionary(u => u.Id);

        var gameweekUrl = AppLinks.Gameweek(_configuration);
        if (gameweekUrl == null)
        {
            _logger.LogWarning(
                "{Key} is not configured, so auto-pick emails go out with no link to the site",
                AppLinks.ConfigurationKey);
        }

        var recipients = new List<Assignment>();
        var messages = new List<EmailMessage>();

        foreach (var assignment in assignments)
        {
            if (!users.TryGetValue(assignment.UserId, out var user) || string.IsNullOrWhiteSpace(user.Email))
            {
                _logger.LogWarning(
                    "Auto-picked user {UserId} for GW{WeekNumber} has no record or no email address",
                    assignment.UserId, gameweek.WeekNumber);
                continue;
            }

            recipients.Add(assignment);
            messages.Add(BuildAutoPickMessage(
                user.Email,
                $"{user.FirstName} {user.LastName}",
                assignment.TeamName,
                gameweek.WeekNumber,
                gameweek.Deadline,
                gameweekUrl));
        }

        if (messages.Count == 0)
            return;

        IReadOnlyList<EmailSendResult> outcomes;
        try
        {
            outcomes = await _emailService.SendBulkAsync(messages, cancellationToken);
        }
        catch (Exception ex)
        {
            // The picks stand regardless. A player who hears nothing still finds the pick on the
            // gameweek page, which beats the run reporting a failure over an email.
            _logger.LogError(ex, "Auto-pick emails for GW{WeekNumber} could not be sent", gameweek.WeekNumber);
            return;
        }

        int sent = 0, failed = 0, skipped = 0;
        for (var i = 0; i < recipients.Count; i++)
        {
            switch (outcomes[i])
            {
                case EmailSendResult.Sent:
                    sent++;
                    break;

                case EmailSendResult.Skipped:
                    skipped++;
                    break;

                default:
                    failed++;
                    _logger.LogError(
                        "Auto-pick email to user {UserId} for GW{WeekNumber} was not accepted by the email provider",
                        recipients[i].UserId, gameweek.WeekNumber);
                    break;
            }
        }

        _logger.LogInformation("Auto-pick emails for GW{WeekNumber}: {Sent} sent, {Failed} failed, {Skipped} skipped",
            gameweek.WeekNumber, sent, failed, skipped);
    }

    private static EmailMessage BuildAutoPickMessage(
        string toEmail,
        string userName,
        string teamName,
        int gameweekNumber,
        DateTime deadline,
        string? gameweekUrl)
    {
        return new EmailMessage(
            toEmail,
            $"⚽ Gameweek {gameweekNumber}: {teamName} was picked for you",
            AutoPickEmailHtml(userName, teamName, gameweekNumber, deadline, gameweekUrl),
            AutoPickEmailPlainText(userName, teamName, gameweekNumber, deadline, gameweekUrl));
    }

    private static string AutoPickEmailHtml(
        string userName, string teamName, int gameweekNumber, DateTime deadline, string? gameweekUrl)
    {
        var deadlineFormatted = deadline.ToString("dddd, MMMM d 'at' h:mm tt 'UTC'");

        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #ea580c; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background-color: #f9f9f9; padding: 30px; border-radius: 0 0 8px 8px; }}
        .team-box {{ background-color: #fff; border-left: 4px solid #ea580c; padding: 15px; margin: 20px 0; border-radius: 4px; }}
        .team-box strong {{ color: #ea580c; font-size: 20px; }}
        .button {{ background-color: #37003c; color: white; padding: 14px 32px; text-decoration: none; border-radius: 6px; display: inline-block; margin-top: 20px; font-weight: bold; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>&#9917; A pick was made for you</h1>
        </div>
        <div class=""content"">
            <h2>Hi {userName},</h2>
            <p>The deadline for <strong>Gameweek {gameweekNumber}</strong> passed at {deadlineFormatted} without a pick from you, so one was assigned automatically.</p>

            <div class=""team-box"">
                <p style=""margin: 0; font-size: 14px; color: #666;"">Your Gameweek {gameweekNumber} pick</p>
                <p style=""margin: 4px 0 0 0;""><strong>{teamName}</strong></p>
            </div>

            <p><strong>Why this team?</strong></p>
            <p>An auto-pick takes the lowest-ranked team in the Premier League table that you have not already used in this half of the season.</p>

            <p><strong>It counts.</strong> It scores exactly like a pick you make yourself &mdash; 3 points for a win, 1 for a draw &mdash; and it uses up {teamName} for the rest of this half. It cannot be changed now the deadline has passed.</p>

            <p>To avoid this next time, make your pick before the deadline. Reminders go out 24 hours and 3 hours beforehand.</p>

            {(gameweekUrl == null ? "" : $@"<a href=""{gameweekUrl}"" class=""button"">See the gameweek</a>")}
        </div>
        <div class=""footer"">
            <p>Premier League Predictions</p>
            <p>You're receiving this because you're participating in the current season.</p>
        </div>
    </div>
</body>
</html>";
    }

    private static string AutoPickEmailPlainText(
        string userName, string teamName, int gameweekNumber, DateTime deadline, string? gameweekUrl)
    {
        var deadlineFormatted = deadline.ToString("dddd, MMMM d 'at' h:mm tt 'UTC'");

        return $@"
A PICK WAS MADE FOR YOU - GAMEWEEK {gameweekNumber}

Hi {userName},

The deadline for Gameweek {gameweekNumber} passed at {deadlineFormatted} without a pick from
you, so one was assigned automatically.

YOUR GAMEWEEK {gameweekNumber} PICK: {teamName}

WHY THIS TEAM?
An auto-pick takes the lowest-ranked team in the Premier League table that you have not
already used in this half of the season.

IT COUNTS. It scores exactly like a pick you make yourself - 3 points for a win, 1 for a
draw - and it uses up {teamName} for the rest of this half. It cannot be changed now the
deadline has passed.

To avoid this next time, make your pick before the deadline. Reminders go out 24 hours and
3 hours beforehand.

{(gameweekUrl == null ? "Visit the site to see the gameweek." : $"See the gameweek: {gameweekUrl}")}

Premier League Predictions
You're receiving this because you're participating in the current season.
";
    }

    public async Task<AutoPickResult> AssignAllMissedPicksAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // Gameweeks still in progress: deadline passed, not yet locked. Two bounds beyond that,
        // both learned the hard way on 2026-09-08.
        //
        // Season, because this ran across every season in the database and assigned a pick in a
        // leftover E2E-TEST season, emailing real players about a gameweek 1 they were not
        // playing while the real competition was in week 4.
        //
        // Age, because !IsLocked was doing all the work of deciding what is "in progress", and
        // nothing had locked a gameweek in weeks - GameweekCompletionService is the only writer
        // of that flag. Every gameweek since the start of the season therefore still qualified,
        // so a run reached back to week 1 indefinitely. A deadline more than a fortnight old is
        // not a missed pick anyone can still be waiting on; it is a gameweek that was never
        // closed off.
        var oldestWorthAssigning = now.Subtract(MaxAutoPickAge);

        var activeSeason = (await _unitOfWork.Seasons.FindAsync(s => s.IsActive, cancellationToken))
            .FirstOrDefault();
        if (activeSeason == null)
        {
            _logger.LogInformation("No active season; nothing to auto-pick");
            return new AutoPickResult { PicksAssigned = 0, PicksFailed = 0, GameweeksProcessed = 0 };
        }

        var activeGameweeksWithPassedDeadlines = await _unitOfWork.Gameweeks.FindAsync(
            g => g.SeasonId == activeSeason.Name
                 && g.Deadline < now
                 && g.Deadline >= oldestWorthAssigning
                 && !g.IsLocked,
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
