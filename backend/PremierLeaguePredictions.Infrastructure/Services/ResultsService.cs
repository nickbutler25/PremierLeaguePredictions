using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Core.Entities;
using PremierLeaguePredictions.Core.Interfaces;

namespace PremierLeaguePredictions.Infrastructure.Services;

public class ResultsService : IResultsService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFootballDataService _footballDataService;
    private readonly IAdminService _adminService;
    private readonly IEliminationService _eliminationService;
    private readonly IHubContext<Hub> _hubContext;
    private readonly ILogger<ResultsService> _logger;

    public ResultsService(
        IUnitOfWork unitOfWork,
        IFootballDataService footballDataService,
        IAdminService adminService,
        IEliminationService eliminationService,
        IHubContext<Hub> hubContext,
        ILogger<ResultsService> logger)
    {
        _unitOfWork = unitOfWork;
        _footballDataService = footballDataService;
        _adminService = adminService;
        _eliminationService = eliminationService;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<ResultsSyncResponse> SyncRecentResultsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting sync of current gameweek results");

        var response = new ResultsSyncResponse();
        var now = DateTime.UtcNow;

        // Find the current gameweek (deadline has passed, but has unfinished or future fixtures)
        var allGameweeks = await _unitOfWork.Gameweeks.GetAllAsync(cancellationToken);
        var allFixtures = await _unitOfWork.Fixtures.GetAllAsync(cancellationToken);

        var currentGameweek = allGameweeks
            .Where(g => g.Deadline < now)
            .OrderByDescending(g => g.WeekNumber)
            .FirstOrDefault(g =>
            {
                var fixtures = allFixtures.Where(f => f.SeasonId == g.SeasonId && f.GameweekNumber == g.WeekNumber).ToList();
                var hasUnfinishedFixtures = fixtures.Any(f => f.Status != "FINISHED" && f.Status != "CANCELLED" && f.Status != "POSTPONED");
                var hasFutureFixtures = fixtures.Any(f => f.KickoffTime > now);
                return hasUnfinishedFixtures || hasFutureFixtures;
            });

        if (currentGameweek == null)
        {
            _logger.LogInformation("No current gameweek found with unfinished fixtures");
            response.Message = "No current gameweek with unfinished fixtures";
            return response;
        }

        _logger.LogInformation("Found current gameweek: GW{WeekNumber}", currentGameweek.WeekNumber);

        // Sync only the current gameweek
        var gameweekResponse = await SyncGameweekResultsAsync(currentGameweek.SeasonId, currentGameweek.WeekNumber, cancellationToken);
        response.FixturesUpdated = gameweekResponse.FixturesUpdated;
        response.PicksRecalculated = gameweekResponse.PicksRecalculated;
        response.UpdatedFixtures.AddRange(gameweekResponse.UpdatedFixtures);
        response.GameweeksProcessed = 1;

        response.Message = $"GW{currentGameweek.WeekNumber}: Updated {response.FixturesUpdated} fixtures, recalculated {response.PicksRecalculated} picks";
        _logger.LogInformation("Current gameweek sync completed: {Message}", response.Message);

        return response;
    }

    public async Task<ResultsSyncResponse> SyncGameweekResultsAsync(string seasonId, int gameweekNumber, CancellationToken cancellationToken = default)
    {
        var response = new ResultsSyncResponse { GameweeksProcessed = 1 };

        var gameweek = await _unitOfWork.Gameweeks.FirstOrDefaultAsync(g => g.SeasonId == seasonId && g.WeekNumber == gameweekNumber, cancellationToken);
        if (gameweek == null)
        {
            _logger.LogWarning("Gameweek {SeasonId}-{GameweekNumber} not found", seasonId, gameweekNumber);
            response.Message = "Gameweek not found";
            return response;
        }

        _logger.LogInformation("Syncing results for GW {WeekNumber}", gameweek.WeekNumber);

        // Track fixtures before sync to compare
        var fixturesBefore = await _unitOfWork.Fixtures.FindAsync(f => f.SeasonId == seasonId && f.GameweekNumber == gameweekNumber, cancellationToken);
        var fixturesSnapshot = fixturesBefore.Select(f => new
        {
            Id = f.Id,
            Status = f.Status,
            HomeScore = f.HomeScore,
            AwayScore = f.AwayScore,
            HomeTeamId = f.HomeTeamId,
            AwayTeamId = f.AwayTeamId
        }).ToList();

        // Update each fixture individually from external API
        _logger.LogInformation("Updating {Count} fixtures from external API for GW {WeekNumber}",
            fixturesSnapshot.Count, gameweek.WeekNumber);

        foreach (var fixtureSnapshot in fixturesSnapshot)
        {
            var fixture = fixturesBefore.FirstOrDefault(f => f.Id == fixtureSnapshot.Id);
            if (fixture?.ExternalId == null) continue;

            try
            {
                // Fetch this specific fixture from the API
                var externalFixture = await _footballDataService.GetFixtureByIdAsync(fixture.ExternalId.Value, cancellationToken);

                if (externalFixture != null)
                {
                    // Update status
                    fixture.Status = externalFixture.Status;

                    // Update scores if available
                    if (externalFixture.Score?.FullTime != null)
                    {
                        fixture.HomeScore = externalFixture.Score.FullTime.Home;
                        fixture.AwayScore = externalFixture.Score.FullTime.Away;
                    }

                    // Update kickoff time if changed
                    fixture.KickoffTime = externalFixture.UtcDate;
                    fixture.UpdatedAt = DateTime.UtcNow;

                    _unitOfWork.Fixtures.Update(fixture);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update fixture {FixtureId} (External: {ExternalId})",
                    fixture.Id, fixture.ExternalId);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Check what changed and build detailed response
        var fixturesAfter = await _unitOfWork.Fixtures.FindAsync(f => f.SeasonId == seasonId && f.GameweekNumber == gameweekNumber, cancellationToken);
        var fixturesAfterList = fixturesAfter.ToList();

        foreach (var fixtureBefore in fixturesSnapshot)
        {
            var fixtureAfter = fixturesAfterList.FirstOrDefault(f => f.Id == fixtureBefore.Id);
            if (fixtureAfter != null)
            {
                bool hasChanges = fixtureBefore.Status != fixtureAfter.Status ||
                                fixtureBefore.HomeScore != fixtureAfter.HomeScore ||
                                fixtureBefore.AwayScore != fixtureAfter.AwayScore;

                if (hasChanges)
                {
                    var homeTeam = await _unitOfWork.Teams.FirstOrDefaultAsync(t => t.Id == fixtureAfter.HomeTeamId, cancellationToken);
                    var awayTeam = await _unitOfWork.Teams.FirstOrDefaultAsync(t => t.Id == fixtureAfter.AwayTeamId, cancellationToken);

                    response.UpdatedFixtures.Add(new FixtureUpdateDetail
                    {
                        FixtureId = fixtureAfter.Id,
                        GameweekNumber = gameweek.WeekNumber,
                        HomeTeam = homeTeam?.Name ?? "Unknown",
                        AwayTeam = awayTeam?.Name ?? "Unknown",
                        OldStatus = fixtureBefore.Status,
                        NewStatus = fixtureAfter.Status,
                        HomeScore = fixtureAfter.HomeScore,
                        AwayScore = fixtureAfter.AwayScore
                    });

                    _logger.LogInformation("Updated fixture: {HomeTeam} {HomeScore} - {AwayScore} {AwayTeam} ({OldStatus} -> {NewStatus})",
                        homeTeam?.Name, fixtureAfter.HomeScore, fixtureAfter.AwayScore, awayTeam?.Name,
                        fixtureBefore.Status, fixtureAfter.Status);
                }
            }
        }

        // If any fixtures were updated, recalculate points for this gameweek
        if (response.UpdatedFixtures.Count > 0)
        {
            _logger.LogInformation("Recalculating points for GW {WeekNumber} due to {Count} fixture updates",
                gameweek.WeekNumber, response.UpdatedFixtures.Count);

            await _adminService.RecalculatePointsForGameweekAsync(seasonId, gameweekNumber, cancellationToken);

            // Count picks that were recalculated
            var picks = await _unitOfWork.Picks.FindAsync(p => p.SeasonId == seasonId && p.GameweekNumber == gameweekNumber, cancellationToken);
            response.PicksRecalculated = picks.Count();

            _logger.LogInformation("Recalculated {Count} picks for GW {WeekNumber}",
                response.PicksRecalculated, gameweek.WeekNumber);

            // Send SignalR notification to all connected clients
            await NotifyResultsUpdatedAsync(response, cancellationToken);
        }

        // Check if gameweek is finished and process eliminations if needed
        await ProcessEliminationsIfGameweekCompleteAsync(gameweek, cancellationToken);

        response.Message = $"GW{gameweek.WeekNumber}: Updated {response.UpdatedFixtures.Count} fixtures, recalculated {response.PicksRecalculated} picks";
        _logger.LogInformation("Results sync completed for GW {WeekNumber}: {Message}", gameweek.WeekNumber, response.Message);

        return response;
    }

    private async Task ProcessEliminationsIfGameweekCompleteAsync(Gameweek gameweek, CancellationToken cancellationToken)
    {
        // Skip if no eliminations configured for this gameweek
        if (gameweek.EliminationCount == 0)
        {
            return;
        }

        // Check if eliminations already processed
        var existingEliminations = await _unitOfWork.UserEliminations.FindAsync(
            e => e.SeasonId == gameweek.SeasonId && e.GameweekNumber == gameweek.WeekNumber,
            cancellationToken
        );

        if (existingEliminations.Any())
        {
            _logger.LogDebug("Eliminations already processed for GW{WeekNumber}", gameweek.WeekNumber);
            return;
        }

        // Check if all fixtures in this gameweek are finished
        var fixtures = await _unitOfWork.Fixtures.FindAsync(
            f => f.SeasonId == gameweek.SeasonId && f.GameweekNumber == gameweek.WeekNumber,
            cancellationToken
        );

        var fixturesList = fixtures.ToList();
        var allFinished = fixturesList.All(f =>
            f.Status == "FINISHED" || f.Status == "CANCELLED" || f.Status == "POSTPONED"
        );

        if (!allFinished)
        {
            _logger.LogDebug("Not all fixtures finished for GW{WeekNumber}, skipping elimination processing", gameweek.WeekNumber);
            return;
        }

        // All fixtures are finished, process eliminations automatically
        _logger.LogInformation("All fixtures finished for GW{WeekNumber}, processing eliminations automatically", gameweek.WeekNumber);

        try
        {
            // Use a system admin ID (Guid.Empty) for automatic eliminations
            var systemAdminId = Guid.Empty;
            var eliminationResponse = await _eliminationService.ProcessGameweekEliminationsAsync(
                gameweek.SeasonId,
                gameweek.WeekNumber,
                systemAdminId,
                cancellationToken
            );

            _logger.LogInformation(
                "Automatic elimination processing completed for GW{WeekNumber}: {Message}",
                gameweek.WeekNumber,
                eliminationResponse.Message
            );

            // Send notification about eliminations
            if (eliminationResponse.PlayersEliminated > 0)
            {
                await NotifyEliminationsProcessedAsync(gameweek, eliminationResponse, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to automatically process eliminations for GW{WeekNumber}", gameweek.WeekNumber);
        }
    }

    private async Task NotifyEliminationsProcessedAsync(
        Gameweek gameweek,
        ProcessEliminationsResponse response,
        CancellationToken cancellationToken)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync(
                "EliminationsProcessed",
                new
                {
                    gameweekNumber = gameweek.WeekNumber,
                    playersEliminated = response.PlayersEliminated,
                    eliminatedPlayers = response.EliminatedPlayers.Select(e => new
                    {
                        userId = e.UserId,
                        userName = e.UserName,
                        position = e.Position,
                        totalPoints = e.TotalPoints
                    }),
                    message = response.Message,
                    timestamp = DateTime.UtcNow
                },
                cancellationToken
            );

            _logger.LogInformation("Sent elimination notification for GW{WeekNumber}", gameweek.WeekNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send elimination notification for GW{WeekNumber}", gameweek.WeekNumber);
        }
    }

    private async Task NotifyResultsUpdatedAsync(ResultsSyncResponse response, CancellationToken cancellationToken)
    {
        try
        {
            // Send notification to all clients subscribed to ResultsUpdates group
            await _hubContext.Clients.Group("ResultsUpdates").SendAsync(
                "ResultsUpdated",
                new
                {
                    fixturesUpdated = response.FixturesUpdated,
                    picksRecalculated = response.PicksRecalculated,
                    gameweeksProcessed = response.GameweeksProcessed,
                    updatedFixtures = response.UpdatedFixtures.Select(f => new
                    {
                        fixtureId = f.FixtureId,
                        gameweekNumber = f.GameweekNumber,
                        homeTeam = f.HomeTeam,
                        awayTeam = f.AwayTeam,
                        homeScore = f.HomeScore,
                        awayScore = f.AwayScore,
                        status = f.NewStatus
                    }),
                    message = response.Message,
                    timestamp = DateTime.UtcNow
                },
                cancellationToken);

            _logger.LogInformation("Sent SignalR notification for {Count} fixture updates", response.UpdatedFixtures.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SignalR notification for results update");
        }
    }
}
