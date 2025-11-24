using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using PremierLeaguePredictions.Application.Interfaces;

namespace PremierLeaguePredictions.Infrastructure.Services;

// Note: This will be injected via DI with the actual NotificationHub type from API project
public class SignalRNotificationService : INotificationService
{
    private readonly IHubContext<Hub> _hubContext;
    private readonly IEmailService _emailService;
    private readonly ILogger<SignalRNotificationService> _logger;

    public SignalRNotificationService(
        IHubContext<Hub> hubContext,
        IEmailService emailService,
        ILogger<SignalRNotificationService> logger)
    {
        _hubContext = hubContext;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task SendSeasonApprovalNotificationAsync(Guid userId, bool isApproved, string seasonName)
    {
        try
        {
            await _hubContext.Clients.User(userId.ToString()).SendAsync(
                "SeasonApprovalUpdate",
                new
                {
                    isApproved,
                    seasonName,
                    timestamp = DateTime.UtcNow
                });

            _logger.LogInformation(
                "Sent season approval notification to user {UserId}: {Status} for {Season}",
                userId,
                isApproved ? "Approved" : "Rejected",
                seasonName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send season approval notification to user {UserId}", userId);
        }
    }

    public async Task SendPickReminderNotificationAsync(Guid userId, string gameweekInfo)
    {
        try
        {
            await _hubContext.Clients.User(userId.ToString()).SendAsync(
                "PickReminder",
                new
                {
                    message = $"Don't forget to make your pick for {gameweekInfo}!",
                    gameweekInfo,
                    timestamp = DateTime.UtcNow
                });

            _logger.LogInformation("Sent pick reminder to user {UserId} for {Gameweek}", userId, gameweekInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send pick reminder to user {UserId}", userId);
        }
    }

    public async Task SendGeneralNotificationAsync(Guid userId, string message, string? type = null)
    {
        try
        {
            await _hubContext.Clients.User(userId.ToString()).SendAsync(
                "Notification",
                new
                {
                    message,
                    type = type ?? "info",
                    timestamp = DateTime.UtcNow
                });

            _logger.LogInformation("Sent general notification to user {UserId}: {Message}", userId, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification to user {UserId}", userId);
        }
    }

    public async Task SendSeasonApprovalEmailAsync(string toEmail, string userName, bool isApproved, string seasonName)
    {
        try
        {
            var subject = isApproved
                ? $"Welcome to {seasonName}!"
                : $"Season Participation Update - {seasonName}";

            var htmlBody = isApproved
                ? GetApprovalEmailHtml(userName, seasonName)
                : GetRejectionEmailHtml(userName, seasonName);

            var plainTextBody = isApproved
                ? GetApprovalEmailPlainText(userName, seasonName)
                : GetRejectionEmailPlainText(userName, seasonName);

            await _emailService.SendEmailAsync(toEmail, subject, htmlBody, plainTextBody);

            _logger.LogInformation(
                "Sent season approval email to {Email}: {Status} for {Season}",
                toEmail,
                isApproved ? "Approved" : "Rejected",
                seasonName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send season approval email to {Email}", toEmail);
        }
    }

    public async Task SendAutoPickAssignedNotificationAsync(Guid userId, string teamName, int gameweekNumber)
    {
        try
        {
            await _hubContext.Clients.User(userId.ToString()).SendAsync(
                "AutoPickAssigned",
                new
                {
                    teamName,
                    gameweekNumber,
                    message = $"{teamName} has been automatically assigned for Gameweek {gameweekNumber}",
                    timestamp = DateTime.UtcNow
                });

            _logger.LogInformation(
                "Sent auto-pick notification to user {UserId}: {TeamName} for GW{GameweekNumber}",
                userId,
                teamName,
                gameweekNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send auto-pick notification to user {UserId}", userId);
        }
    }

    private static string GetApprovalEmailHtml(string userName, string seasonName)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #37003c; color: white; padding: 20px; text-align: center; }}
        .content {{ background-color: #f9f9f9; padding: 30px; }}
        .button {{ background-color: #37003c; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; margin-top: 20px; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>🎉 You're Approved!</h1>
        </div>
        <div class=""content"">
            <h2>Welcome {userName}!</h2>
            <p>Great news! Your participation request for <strong>{seasonName}</strong> has been approved.</p>
            <p>You can now:</p>
            <ul>
                <li>Make your team picks for each gameweek</li>
                <li>View the league standings</li>
                <li>Track your progress throughout the season</li>
            </ul>
            <p>Remember, you can pick each team once per half of the season. Choose wisely!</p>
            <a href=""https://your-app-url.com"" class=""button"">Go to Dashboard</a>
        </div>
        <div class=""footer"">
            <p>Premier League Predictions</p>
            <p>Good luck this season!</p>
        </div>
    </div>
</body>
</html>";
    }

    private static string GetApprovalEmailPlainText(string userName, string seasonName)
    {
        return $@"
Welcome {userName}!

Great news! Your participation request for {seasonName} has been approved.

You can now:
- Make your team picks for each gameweek
- View the league standings
- Track your progress throughout the season

Remember, you can pick each team once per half of the season. Choose wisely!

Visit the dashboard to get started.

Good luck this season!

Premier League Predictions
";
    }

    private static string GetRejectionEmailHtml(string userName, string seasonName)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #666; color: white; padding: 20px; text-align: center; }}
        .content {{ background-color: #f9f9f9; padding: 30px; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Participation Update</h1>
        </div>
        <div class=""content"">
            <h2>Hi {userName},</h2>
            <p>We regret to inform you that your participation request for <strong>{seasonName}</strong> was not approved at this time.</p>
            <p>If you believe this is an error or would like more information, please contact the administrator.</p>
        </div>
        <div class=""footer"">
            <p>Premier League Predictions</p>
        </div>
    </div>
</body>
</html>";
    }

    private static string GetRejectionEmailPlainText(string userName, string seasonName)
    {
        return $@"
Hi {userName},

We regret to inform you that your participation request for {seasonName} was not approved at this time.

If you believe this is an error or would like more information, please contact the administrator.

Premier League Predictions
";
    }
}
