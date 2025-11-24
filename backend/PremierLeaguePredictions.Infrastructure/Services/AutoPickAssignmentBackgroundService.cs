using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PremierLeaguePredictions.Application.Interfaces;

namespace PremierLeaguePredictions.Infrastructure.Services;

public class AutoPickAssignmentBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AutoPickAssignmentBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5); // Check every 5 minutes

    public AutoPickAssignmentBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<AutoPickAssignmentBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Auto Pick Assignment Background Service is starting");

        // Wait 60 seconds before first check to allow app to fully start
        await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await AssignMissedPicksAsync(stoppingToken);

                // Wait for the next check interval
                _logger.LogDebug("Next auto-pick check scheduled in {Minutes} minutes", _checkInterval.TotalMinutes);
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while assigning missed picks in background service");
                // Wait a bit before retrying on error
                await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
            }
        }

        _logger.LogInformation("Auto Pick Assignment Background Service is stopping");
    }

    private async Task AssignMissedPicksAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var autoPickService = scope.ServiceProvider.GetRequiredService<IAutoPickService>();

        _logger.LogDebug("Checking for gameweeks requiring auto-pick assignments");

        try
        {
            await autoPickService.AssignAllMissedPicksAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to assign missed picks automatically");
        }
    }
}
