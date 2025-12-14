using PremierLeaguePredictions.Application.DTOs;

namespace PremierLeaguePredictions.Application.Interfaces;

public interface IPickReminderService
{
    /// <summary>
    /// Send pick reminder emails to users who haven't made picks for upcoming gameweeks
    /// </summary>
    /// <returns>Result containing the count of emails sent and failed</returns>
    Task<ReminderResult> SendPickRemindersAsync(CancellationToken cancellationToken = default);
}
