using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Core.Interfaces;

namespace PremierLeaguePredictions.Application.Services;

public class PickReminderService : IPickReminderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PickReminderService> _logger;

    // Reminder windows (in hours before deadline). A 12h reminder was dropped: it lands in the
    // middle of the night for a Saturday deadline and adds a full send to the same calendar day
    // as the 24h one, which is what pushes a gameweek over a provider's daily allowance.
    private static readonly int[] ReminderWindows = { 24, 3 };

    public PickReminderService(
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<PickReminderService> logger)
    {
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ReminderResult> SendPickRemindersAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var totalEmailsSent = 0;
        var totalEmailsFailed = 0;
        var totalEmailsSkipped = 0;

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
                    var (sent, failed, skipped) = await SendRemindersForGameweekAsync(gameweek, reminderWindow, cancellationToken);
                    totalEmailsSent += sent;
                    totalEmailsFailed += failed;
                    totalEmailsSkipped += skipped;
                    break; // Only send one reminder per check
                }
            }
        }

        _logger.LogInformation("Pick reminder check completed: {Sent} sent, {Failed} failed, {Skipped} skipped",
            totalEmailsSent, totalEmailsFailed, totalEmailsSkipped);

        return new ReminderResult
        {
            EmailsSent = totalEmailsSent,
            EmailsFailed = totalEmailsFailed,
            EmailsSkipped = totalEmailsSkipped
        };
    }

    private async Task<(int sent, int failed, int skipped)> SendRemindersForGameweekAsync(
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
            return (0, 0, 0);
        }

        _logger.LogInformation("Sending {Count} reminder emails for GW{WeekNumber} ({Hours}h before deadline)",
            usersNeedingReminders.Count, gameweek.WeekNumber, hoursBeforeDeadline);

        // Load every recipient in one query. Fetching users one at a time inside the send
        // loop cost a round trip each — a few hundred reminders meant a few hundred round
        // trips before a single email was even attempted.
        var users = await _unitOfWork.Users.FindAsync(
            u => usersNeedingReminders.Contains(u.Id), cancellationToken);

        var recipients = users
            .Where(u => !string.IsNullOrEmpty(u.Email))
            .ToList();

        var missing = usersNeedingReminders.Count - recipients.Count;
        if (missing > 0)
            _logger.LogWarning("{Count} user(s) needing reminders have no record or no email address", missing);

        var dashboardUrl = AppLinks.Dashboard(_configuration);
        if (dashboardUrl == null)
        {
            // The mail still sends, but without the one thing it exists to provide: a way in.
            _logger.LogWarning(
                "{Key} is not configured, so pick reminders go out with no link to the site",
                AppLinks.ConfigurationKey);
        }

        var messages = recipients
            .Select(u => BuildPickReminderMessage(
                u.Email,
                $"{u.FirstName} {u.LastName}",
                gameweek.WeekNumber,
                gameweek.Deadline,
                hoursBeforeDeadline,
                dashboardUrl))
            .ToList();

        // One batched call rather than one request per recipient: at a few hundred players
        // the sequential version outlived the caller's HTTP timeout before finishing.
        var outcomes = await _emailService.SendBulkAsync(messages, cancellationToken);

        int emailsSent = 0;
        int emailsFailed = 0;
        int emailsSkipped = 0;

        for (var i = 0; i < recipients.Count; i++)
        {
            switch (outcomes[i])
            {
                case EmailSendResult.Sent:
                    emailsSent++;
                    break;

                case EmailSendResult.Skipped:
                    emailsSkipped++;
                    _logger.LogInformation(
                        "Skipped pick reminder for {Email} (GW{WeekNumber}) — test address",
                        recipients[i].Email, gameweek.WeekNumber);
                    break;

                default:
                    emailsFailed++;
                    _logger.LogError(
                        "Pick reminder to {Email} for GW{WeekNumber} was not accepted by the email provider",
                        recipients[i].Email, gameweek.WeekNumber);
                    break;
            }
        }

        _logger.LogInformation("Pick reminders for GW{WeekNumber}: {Sent} sent, {Failed} failed, {Skipped} skipped",
            gameweek.WeekNumber, emailsSent, emailsFailed, emailsSkipped);

        return (emailsSent, emailsFailed, emailsSkipped);
    }

    private static EmailMessage BuildPickReminderMessage(
        string toEmail,
        string userName,
        int gameweekNumber,
        DateTime deadline,
        int hoursBeforeDeadline,
        string? dashboardUrl)
    {
        var subject = $"⚽ Reminder: Make your pick for Gameweek {gameweekNumber}";

        return new EmailMessage(
            toEmail,
            subject,
            GetReminderEmailHtml(userName, gameweekNumber, deadline, hoursBeforeDeadline, dashboardUrl),
            GetReminderEmailPlainText(userName, gameweekNumber, deadline, hoursBeforeDeadline, dashboardUrl));
    }

    private static string GetReminderEmailHtml(
        string userName,
        int gameweekNumber,
        DateTime deadline,
        int hoursBeforeDeadline,
        string? dashboardUrl)
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

            {(dashboardUrl == null ? "" : $@"<a href=""{dashboardUrl}"" class=""button"">Make Your Pick Now</a>")}
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
        int hoursBeforeDeadline,
        string? dashboardUrl)
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

{(dashboardUrl == null ? "Visit the dashboard to make your pick now." : $"Make your pick now: {dashboardUrl}")}

Premier League Predictions
You're receiving this because you're participating in the current season.
";
    }
}
