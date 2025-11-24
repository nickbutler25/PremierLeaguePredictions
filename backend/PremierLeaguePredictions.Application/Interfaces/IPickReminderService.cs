namespace PremierLeaguePredictions.Application.Interfaces;

public interface IPickReminderService
{
    /// <summary>
    /// Send pick reminder emails to users who haven't made picks for upcoming gameweeks
    /// </summary>
    Task SendPickRemindersAsync(CancellationToken cancellationToken = default);
}
