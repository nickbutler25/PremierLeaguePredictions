using Microsoft.Extensions.Logging;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Core.Entities;
using PremierLeaguePredictions.Core.Interfaces;

namespace PremierLeaguePredictions.Infrastructure.Services;

/// <summary>
/// Finalises gameweeks whose football is over. See <see cref="IGameweekCompletionService"/>.
/// </summary>
public class GameweekCompletionService : IGameweekCompletionService
{
    /// <summary>
    /// Statuses a fixture cannot move on from. POSTPONED is absent on purpose: a postponed match
    /// still has to be played, so its gameweek is not finished and nobody should be eliminated on
    /// the strength of results that are missing one.
    /// </summary>
    private static readonly string[] SettledStatuses = ["FINISHED", "AWARDED", "CANCELLED"];

    /// <summary>
    /// Eliminations performed by the scheduler are attributed to nobody. Matches what the
    /// automatic path in ResultsService records.
    /// </summary>
    /// <remarks>
    /// Null, not <see cref="Guid.Empty"/>. EliminatedBy is a foreign key to users, so the empty
    /// guid is not a stand-in for "no admin" — it is a value matching no row, and every automatic
    /// elimination died on a 23503 against FK_user_eliminations_users_eliminated_by until this
    /// became null. The column is already nullable; nothing needed a migration.
    /// </remarks>
    private static readonly Guid? SystemAdminId = null;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IResultsService _resultsService;
    private readonly IEliminationService _eliminationService;
    private readonly ILogger<GameweekCompletionService> _logger;

    public GameweekCompletionService(
        IUnitOfWork unitOfWork,
        IResultsService resultsService,
        IEliminationService eliminationService,
        ILogger<GameweekCompletionService> logger)
    {
        _unitOfWork = unitOfWork;
        _resultsService = resultsService;
        _eliminationService = eliminationService;
        _logger = logger;
    }

    public async Task<GameweekCompletionResponse> CompleteFinishedGameweeksAsync(
        CancellationToken cancellationToken = default)
    {
        var response = new GameweekCompletionResponse();
        var now = DateTime.UtcNow;

        var seasons = await _unitOfWork.Seasons.FindAsync(s => s.IsActive, cancellationToken);
        var season = seasons.FirstOrDefault();
        if (season == null)
        {
            _logger.LogInformation("No active season — nothing to complete");
            return response;
        }

        // Every gameweek whose deadline has passed and which is still open. Normally that is
        // just the one that has finished; anything older means a previous completion was skipped,
        // and this run is its retry.
        var candidates = (await _unitOfWork.Gameweeks.FindAsync(
                g => g.SeasonId == season.Name && !g.IsLocked && g.Deadline <= now,
                cancellationToken))
            .OrderBy(g => g.Deadline)
            .ToList();

        if (candidates.Count == 0)
        {
            _logger.LogInformation("No open gameweeks with a passed deadline in season {SeasonId}", season.Name);
            return response;
        }

        _logger.LogInformation("Checking {Count} open gameweek(s) for completion in season {SeasonId}",
            candidates.Count, season.Name);

        foreach (var gameweek in candidates)
        {
            try
            {
                await CompleteOneAsync(gameweek, response, cancellationToken);
            }
            catch (Exception ex)
            {
                // One bad gameweek must not stop the others; the next run retries it.
                _logger.LogError(ex, "Failed to complete GW{WeekNumber}", gameweek.WeekNumber);
                response.Skipped.Add(new GameweekCompletionDetail
                {
                    SeasonId = gameweek.SeasonId,
                    GameweekNumber = gameweek.WeekNumber,
                    Reason = $"Error: {ex.Message}"
                });
            }
        }

        _logger.LogInformation("Gameweek completion run finished: {Message}", response.Message);
        return response;
    }

    private async Task CompleteOneAsync(
        Gameweek gameweek, GameweekCompletionResponse response, CancellationToken cancellationToken)
    {
        // Pull results once more before judging the gameweek finished. The scheduled sync window
        // has closed by now, so if a late goal or a full-time whistle landed after the last sync
        // this is the only chance to record it.
        await _resultsService.SyncGameweekResultsAsync(gameweek.SeasonId, gameweek.WeekNumber, cancellationToken);

        var fixtures = (await _unitOfWork.Fixtures.FindAsync(
            f => f.SeasonId == gameweek.SeasonId && f.GameweekNumber == gameweek.WeekNumber,
            cancellationToken)).ToList();

        if (fixtures.Count == 0)
        {
            _logger.LogWarning("GW{WeekNumber} has no fixtures — leaving it open", gameweek.WeekNumber);
            response.Skipped.Add(new GameweekCompletionDetail
            {
                SeasonId = gameweek.SeasonId,
                GameweekNumber = gameweek.WeekNumber,
                Reason = "No fixtures"
            });
            return;
        }

        var unsettled = fixtures
            .Where(f => !SettledStatuses.Contains(f.Status, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (unsettled.Count > 0)
        {
            var statuses = string.Join(", ", unsettled.Select(f => f.Status).Distinct());

            // Loud on purpose: the gameweek stays open, players are not eliminated, and the UI
            // keeps showing it as in progress until this resolves.
            _logger.LogWarning(
                "GW{WeekNumber} not complete — {Count} of {Total} fixtures unsettled ({Statuses}). " +
                "Leaving it open; the next weekly generate will schedule another attempt.",
                gameweek.WeekNumber, unsettled.Count, fixtures.Count, statuses);

            response.Skipped.Add(new GameweekCompletionDetail
            {
                SeasonId = gameweek.SeasonId,
                GameweekNumber = gameweek.WeekNumber,
                Reason = $"{unsettled.Count} of {fixtures.Count} fixtures unsettled ({statuses})"
            });
            return;
        }

        var eliminated = await ProcessEliminationsAsync(gameweek, cancellationToken);

        // Locked last, so a failure above leaves the gameweek eligible for the next attempt
        // rather than closing it having done only half the work.
        gameweek.IsLocked = true;
        gameweek.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Gameweeks.Update(gameweek);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("GW{WeekNumber} completed and locked; {Count} player(s) eliminated",
            gameweek.WeekNumber, eliminated);

        response.Completed.Add(new GameweekCompletionDetail
        {
            SeasonId = gameweek.SeasonId,
            GameweekNumber = gameweek.WeekNumber,
            PlayersEliminated = eliminated
        });
    }

    private async Task<int> ProcessEliminationsAsync(Gameweek gameweek, CancellationToken cancellationToken)
    {
        if (gameweek.EliminationCount == 0)
        {
            _logger.LogDebug("GW{WeekNumber} eliminates nobody by configuration", gameweek.WeekNumber);
            return 0;
        }

        // The sync above may already have run them — ResultsService processes eliminations as
        // soon as it sees a gameweek's last fixture finish. Running twice would eliminate a
        // second batch of players.
        var existing = await _unitOfWork.UserEliminations.FindAsync(
            e => e.SeasonId == gameweek.SeasonId && e.GameweekNumber == gameweek.WeekNumber,
            cancellationToken);

        var alreadyEliminated = existing.Count();
        if (alreadyEliminated > 0)
        {
            _logger.LogInformation("GW{WeekNumber} eliminations already processed ({Count} player(s))",
                gameweek.WeekNumber, alreadyEliminated);
            return alreadyEliminated;
        }

        var result = await _eliminationService.ProcessGameweekEliminationsAsync(
            gameweek.SeasonId, gameweek.WeekNumber, SystemAdminId, cancellationToken);

        _logger.LogInformation("GW{WeekNumber} eliminations: {Message}", gameweek.WeekNumber, result.Message);
        return result.PlayersEliminated;
    }
}
