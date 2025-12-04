using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Core.Interfaces;

namespace PremierLeaguePredictions.Infrastructure.Services;

public class ResultsSyncBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ResultsSyncBackgroundService> _logger;
    private readonly TimeSpan _gameLength = TimeSpan.FromMinutes(105); // Average game length (90 min + 15 min extra time/stoppage)
    private readonly TimeSpan _fallbackInterval = TimeSpan.FromHours(6); // Fallback if no games scheduled

    public ResultsSyncBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ResultsSyncBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Smart Results Sync Background Service is starting");

        // Wait 30 seconds before first sync to allow app to fully start
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Calculate when the next sync should occur
                var nextSyncTime = await CalculateNextSyncTimeAsync(stoppingToken);
                var delay = nextSyncTime - DateTime.UtcNow;

                if (delay > TimeSpan.Zero)
                {
                    _logger.LogInformation("Next results sync scheduled for {NextSyncTime} (in {Minutes} minutes)",
                        nextSyncTime, delay.TotalMinutes);
                    await Task.Delay(delay, stoppingToken);
                }

                // Sync results
                await SyncResultsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while syncing results in background service");
                // Wait a bit before retrying on error
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        _logger.LogInformation("Smart Results Sync Background Service is stopping");
    }

    private async Task<DateTime> CalculateNextSyncTimeAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var now = DateTime.UtcNow;

        // Get fixtures from the past 24 hours and next 7 days
        var recentFixtures = await unitOfWork.Fixtures.FindAsync(
            f => f.KickoffTime >= now.AddHours(-24) && f.KickoffTime <= now.AddDays(7),
            cancellationToken);

        var fixturesList = recentFixtures.OrderBy(f => f.KickoffTime).ToList();

        if (!fixturesList.Any())
        {
            _logger.LogInformation("No upcoming fixtures found, using fallback interval");
            return now.Add(_fallbackInterval);
        }

        // Check if there are any games currently in progress or just finished
        // Premier League games typically last ~2 hours, but can go longer with injury time/delays
        var currentlyPlayingOrRecent = fixturesList
            .Where(f => f.KickoffTime <= now && f.KickoffTime >= now.AddHours(-3.5)) // Games that started in last 3.5 hours
            .Where(f => f.Status != "FINISHED" && f.Status != "CANCELLED" && f.Status != "POSTPONED")
            .ToList();

        if (currentlyPlayingOrRecent.Any())
        {
            // There are games in progress, sync in 2 minutes to check for updates
            _logger.LogInformation("Found {Count} games currently in progress, will sync in 2 minutes",
                currentlyPlayingOrRecent.Count);
            return now.AddMinutes(2);
        }

        // Find the next fixture that will finish
        var upcomingFixtures = fixturesList
            .Where(f => f.KickoffTime > now)
            .Where(f => f.Status != "CANCELLED" && f.Status != "POSTPONED")
            .ToList();

        if (upcomingFixtures.Any())
        {
            var nextFixture = upcomingFixtures.First();
            var expectedEndTime = nextFixture.KickoffTime.Add(_gameLength);

            _logger.LogInformation("Next fixture kicks off at {KickoffTime}, expected to finish at {EndTime}",
                nextFixture.KickoffTime, expectedEndTime);

            // Sync 5 minutes after expected end time
            return expectedEndTime.AddMinutes(5);
        }

        // Check if there are recent fixtures that might not be fully updated yet
        var recentlyFinished = fixturesList
            .Where(f => f.KickoffTime <= now && f.KickoffTime >= now.AddHours(-3))
            .Where(f => f.Status == "FINISHED")
            .ToList();

        if (recentlyFinished.Any())
        {
            // Sync again in 15 minutes to ensure all scores are final
            _logger.LogInformation("Found {Count} recently finished games, will sync again in 15 minutes",
                recentlyFinished.Count);
            return now.AddMinutes(15);
        }

        // No games found, use fallback
        _logger.LogInformation("No active or upcoming games in next 7 days, using fallback interval");
        return now.Add(_fallbackInterval);
    }

    private async Task SyncResultsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var resultsService = scope.ServiceProvider.GetRequiredService<IResultsService>();

        _logger.LogInformation("Starting automatic results sync");

        try
        {
            var response = await resultsService.SyncRecentResultsAsync(cancellationToken);

            if (response.FixturesUpdated > 0)
            {
                _logger.LogInformation("Auto-sync completed: {Message}", response.Message);
            }
            else
            {
                _logger.LogDebug("Auto-sync completed: No fixture updates");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync results automatically");
        }
    }
}
