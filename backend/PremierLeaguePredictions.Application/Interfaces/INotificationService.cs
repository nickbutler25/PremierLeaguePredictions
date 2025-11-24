namespace PremierLeaguePredictions.Application.Interfaces;

public interface INotificationService
{
    Task SendSeasonApprovalNotificationAsync(Guid userId, bool isApproved, string seasonName);
    Task SendPickReminderNotificationAsync(Guid userId, string gameweekInfo);
    Task SendGeneralNotificationAsync(Guid userId, string message, string? type = null);
    Task SendSeasonApprovalEmailAsync(string toEmail, string userName, bool isApproved, string seasonName);
    Task SendAutoPickAssignedNotificationAsync(Guid userId, string teamName, int gameweekNumber);
}
