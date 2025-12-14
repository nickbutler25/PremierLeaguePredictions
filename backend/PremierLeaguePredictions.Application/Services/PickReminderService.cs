using Microsoft.Extensions.Logging;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Core.Interfaces;

namespace PremierLeaguePredictions.Application.Services;

public class PickReminderService : IPickReminderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly ILogger<PickReminderService> _logger;

    // Reminder windows (in hours before deadline)
    private static readonly int[] ReminderWindows = { 24, 12, 3 };

    public PickReminderService(
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        ILogger<PickReminderService> logger)
    {
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<ReminderResult> SendPickRemindersAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var totalEmailsSent = 0;
        var totalEmailsFailed = 0;

        _logger.LogInformation("Starting pick reminder check at {Time}", now);

        // Get all gameweeks with deadlines in the future
        var upcomingGameweeks = await _unitOfWork.Gameweeks.FindAsync(
            g => g.Deadline > now,
            cancellationToken);

        var gameweeksList = upcomingGameweeks.OrderBy(g => g.Deadline).ToList();

        foreach (var gameweek in gameweeksList)
        {
            var timeUntilDeadline = gameweek.Deadline - now;
            var hoursUntilDeadline = timeUntilDeadline.TotalHours;

            // Check if we're in one of the reminder windows
            foreach (var reminderWindow in ReminderWindows)
            {
                // Check if we're within 30 minutes of the reminder window
                // This gives us a window to send the reminder even if the background service doesn't run exactly on time
                if (hoursUntilDeadline <= reminderWindow && hoursUntilDeadline >= (reminderWindow - 0.5))
                {
                    var (sent, failed) = await SendRemindersForGameweekAsync(gameweek, reminderWindow, cancellationToken);
                    totalEmailsSent += sent;
                    totalEmailsFailed += failed;
                    break; // Only send one reminder per check
                }
            }
        }

        _logger.LogInformation("Pick reminder check completed: {Sent} sent, {Failed} failed",
            totalEmailsSent, totalEmailsFailed);

        return new ReminderResult
        {
            EmailsSent = totalEmailsSent,
            EmailsFailed = totalEmailsFailed
        };
    }

    private async Task<(int sent, int failed)> SendRemindersForGameweekAsync(
        Core.Entities.Gameweek gameweek,
        int hoursBeforeDeadline,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Checking reminders for GW{WeekNumber} ({Hours}h before deadline)",
            gameweek.WeekNumber, hoursBeforeDeadline);

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
            p => p.SeasonId == gameweek.SeasonId && p.GameweekNumber == gameweek.WeekNumber,
            cancellationToken);

        var usersWithPicks = existingPicks.Select(p => p.UserId).ToHashSet();

        // Find users who need reminders (approved, not eliminated, no pick)
        var usersNeedingReminders = approvedUserIds
            .Where(userId => !eliminatedUserIds.Contains(userId) && !usersWithPicks.Contains(userId))
            .ToList();

        if (!usersNeedingReminders.Any())
        {
            _logger.LogInformation("No users need reminders for GW{WeekNumber} ({Hours}h)",
                gameweek.WeekNumber, hoursBeforeDeadline);
            return (0, 0);
        }

        _logger.LogInformation("Sending {Count} reminder emails for GW{WeekNumber} ({Hours}h before deadline)",
            usersNeedingReminders.Count, gameweek.WeekNumber, hoursBeforeDeadline);

        int emailsSent = 0;
        int emailsFailed = 0;

        foreach (var userId in usersNeedingReminders)
        {
            try
            {
                var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
                if (user == null || string.IsNullOrEmpty(user.Email))
                {
                    _logger.LogWarning("User {UserId} not found or has no email", userId);
                    continue;
                }

                await SendPickReminderEmailAsync(
                    user.Email,
                    $"{user.FirstName} {user.LastName}",
                    gameweek.WeekNumber,
                    gameweek.Deadline,
                    hoursBeforeDeadline,
                    cancellationToken);

                emailsSent++;
                _logger.LogInformation("Sent pick reminder to {Email} for GW{WeekNumber}",
                    user.Email, gameweek.WeekNumber);
            }
            catch (Exception ex)
            {
                emailsFailed++;
                _logger.LogError(ex, "Failed to send pick reminder to user {UserId} for GW{WeekNumber}",
                    userId, gameweek.WeekNumber);
            }
        }

        _logger.LogInformation("Pick reminders for GW{WeekNumber}: {Sent} sent, {Failed} failed",
            gameweek.WeekNumber, emailsSent, emailsFailed);

        return (emailsSent, emailsFailed);
    }

    private async Task SendPickReminderEmailAsync(
        string toEmail,
        string userName,
        int gameweekNumber,
        DateTime deadline,
        int hoursBeforeDeadline,
        CancellationToken cancellationToken)
    {
        var subject = $"⚽ Reminder: Make your pick for Gameweek {gameweekNumber}";

        var htmlBody = GetReminderEmailHtml(userName, gameweekNumber, deadline, hoursBeforeDeadline);
        var plainTextBody = GetReminderEmailPlainText(userName, gameweekNumber, deadline, hoursBeforeDeadline);

        await _emailService.SendEmailAsync(toEmail, subject, htmlBody, plainTextBody);
    }

    private static string GetReminderEmailHtml(
        string userName,
        int gameweekNumber,
        DateTime deadline,
        int hoursBeforeDeadline)
    {
        var deadlineFormatted = deadline.ToString("dddd, MMMM d 'at' h:mm tt 'UTC'");
        var urgencyColor = hoursBeforeDeadline <= 3 ? "#dc2626" : hoursBeforeDeadline <= 12 ? "#ea580c" : "#37003c";
        var urgencyText = hoursBeforeDeadline <= 3 ? "URGENT" : hoursBeforeDeadline <= 12 ? "Important" : "Reminder";

        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: {urgencyColor}; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background-color: #f9f9f9; padding: 30px; border-radius: 0 0 8px 8px; }}
        .deadline-box {{ background-color: #fff; border-left: 4px solid {urgencyColor}; padding: 15px; margin: 20px 0; border-radius: 4px; }}
        .deadline-box strong {{ color: {urgencyColor}; font-size: 18px; }}
        .button {{ background-color: {urgencyColor}; color: white; padding: 14px 32px; text-decoration: none; border-radius: 6px; display: inline-block; margin-top: 20px; font-weight: bold; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
        ul {{ padding-left: 20px; }}
        li {{ margin: 8px 0; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>⚽ {urgencyText}: Pick Missing!</h1>
        </div>
        <div class=""content"">
            <h2>Hi {userName},</h2>
            <p>You haven't made your pick for <strong>Gameweek {gameweekNumber}</strong> yet!</p>

            <div class=""deadline-box"">
                <p style=""margin: 0;"">⏰ <strong>Time remaining: {hoursBeforeDeadline} hours</strong></p>
                <p style=""margin: 8px 0 0 0; font-size: 14px; color: #666;"">Deadline: {deadlineFormatted}</p>
            </div>

            <p><strong>What happens if you don't pick?</strong></p>
            <p>If you miss the deadline, the system will automatically assign you the <strong>lowest-ranked team</strong> in the Premier League table that you haven't picked yet in this half of the season.</p>

            <p><strong>Don't let that happen!</strong> Choose your team strategically:</p>
            <ul>
                <li>Pick a team you think will <strong>win</strong> this week (3 points for a win)</li>
                <li>Remember: Each team can only be picked <strong>once per half</strong></li>
                <li>Save the top teams for tough weeks</li>
            </ul>

            <a href=""https://your-app-url.com"" class=""button"">Make Your Pick Now</a>
        </div>
        <div class=""footer"">
            <p>Premier League Predictions</p>
            <p>You're receiving this because you're participating in the current season.</p>
        </div>
    </div>
</body>
</html>";
    }

    private static string GetReminderEmailPlainText(
        string userName,
        int gameweekNumber,
        DateTime deadline,
        int hoursBeforeDeadline)
    {
        var deadlineFormatted = deadline.ToString("dddd, MMMM d 'at' h:mm tt 'UTC'");
        var urgencyText = hoursBeforeDeadline <= 3 ? "URGENT" : hoursBeforeDeadline <= 12 ? "IMPORTANT" : "REMINDER";

        return $@"
{urgencyText}: PICK MISSING FOR GAMEWEEK {gameweekNumber}

Hi {userName},

You haven't made your pick for Gameweek {gameweekNumber} yet!

TIME REMAINING: {hoursBeforeDeadline} hours
Deadline: {deadlineFormatted}

WHAT HAPPENS IF YOU DON'T PICK?
If you miss the deadline, the system will automatically assign you the lowest-ranked team in the Premier League table that you haven't picked yet in this half of the season.

DON'T LET THAT HAPPEN! Choose your team strategically:
- Pick a team you think will WIN this week (3 points for a win)
- Remember: Each team can only be picked ONCE per half
- Save the top teams for tough weeks

Visit the dashboard to make your pick now.

Premier League Predictions
You're receiving this because you're participating in the current season.
";
    }
}
