using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PremierLeaguePredictions.Application.Interfaces;

namespace PremierLeaguePredictions.Infrastructure.Services;

public class PickReminderBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PickReminderBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(30); // Check every 30 minutes

    public PickReminderBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<PickReminderBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Pick Reminder Background Service is starting");

        // Wait 2 minutes before first check to allow app to fully start
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendPickRemindersAsync(stoppingToken);

                // Wait for the next check interval
                _logger.LogDebug("Next pick reminder check scheduled in {Minutes} minutes", _checkInterval.TotalMinutes);
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while sending pick reminders in background service");
                // Wait a bit before retrying on error
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        _logger.LogInformation("Pick Reminder Background Service is stopping");
    }

    private async Task SendPickRemindersAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var pickReminderService = scope.ServiceProvider.GetRequiredService<IPickReminderService>();

        _logger.LogDebug("Checking for pick reminders to send");

        try
        {
            await pickReminderService.SendPickRemindersAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send pick reminders");
        }
    }
}
