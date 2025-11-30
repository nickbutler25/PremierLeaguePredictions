using Microsoft.Extensions.Logging;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Core.Interfaces;

namespace PremierLeaguePredictions.Application.Services;

public class GameweekService : IGameweekService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GameweekService> _logger;

    public GameweekService(IUnitOfWork unitOfWork, ILogger<GameweekService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<GameweekDto?> GetGameweekAsync(string seasonId, int weekNumber, CancellationToken cancellationToken = default)
    {
        var gameweek = await _unitOfWork.Gameweeks.FirstOrDefaultAsync(g => g.SeasonId == seasonId && g.WeekNumber == weekNumber, cancellationToken);
        return gameweek != null ? new GameweekDto
        {
            SeasonId = gameweek.SeasonId,
            WeekNumber = gameweek.WeekNumber,
            Deadline = gameweek.Deadline,
            IsLocked = gameweek.IsLocked,
            CreatedAt = gameweek.CreatedAt
        } : null;
    }

    public async Task<IEnumerable<GameweekDto>> GetAllGameweeksAsync(CancellationToken cancellationToken = default)
    {
        var gameweeks = await _unitOfWork.Gameweeks.GetAllAsync(cancellationToken);
        return gameweeks.Select(g => new GameweekDto
        {
            SeasonId = g.SeasonId,
            WeekNumber = g.WeekNumber,
            Deadline = g.Deadline,
            IsLocked = g.IsLocked,
            CreatedAt = g.CreatedAt
        });
    }

    public async Task<GameweekDto?> GetCurrentGameweekAsync(CancellationToken cancellationToken = default)
    {
        var gameweeks = await _unitOfWork.Gameweeks.FindAsync(
            g => !g.IsLocked && g.Deadline > DateTime.UtcNow,
            cancellationToken);

        var current = gameweeks.OrderBy(g => g.Deadline).FirstOrDefault();
        return current != null ? new GameweekDto
        {
            SeasonId = current.SeasonId,
            WeekNumber = current.WeekNumber,
            Deadline = current.Deadline,
            IsLocked = current.IsLocked,
            CreatedAt = current.CreatedAt
        } : null;
    }

    public async Task<GameweekWithFixturesDto?> GetGameweekWithFixturesAsync(string seasonId, int weekNumber, CancellationToken cancellationToken = default)
    {
        var gameweek = await _unitOfWork.Gameweeks.FirstOrDefaultAsync(g => g.SeasonId == seasonId && g.WeekNumber == weekNumber, cancellationToken);
        if (gameweek == null) return null;

        var fixtures = await _unitOfWork.Fixtures.FindAsync(f => f.SeasonId == seasonId && f.GameweekNumber == weekNumber, cancellationToken);

        return new GameweekWithFixturesDto
        {
            SeasonId = gameweek.SeasonId,
            WeekNumber = gameweek.WeekNumber,
            Deadline = gameweek.Deadline,
            IsLocked = gameweek.IsLocked,
            CreatedAt = gameweek.CreatedAt,
            Fixtures = fixtures.Select(f => new FixtureDto
            {
                Id = f.Id,
                SeasonId = f.SeasonId,
                GameweekNumber = f.GameweekNumber,
                HomeTeamId = f.HomeTeamId,
                AwayTeamId = f.AwayTeamId,
                HomeScore = f.HomeScore,
                AwayScore = f.AwayScore,
                KickoffTime = f.KickoffTime,
                Status = f.Status
            }).ToList()
        };
    }

    public async Task<IEnumerable<GameweekDto>> GetGameweeksBySeasonIdAsync(string seasonId, CancellationToken cancellationToken = default)
    {
        var gameweeks = await _unitOfWork.Gameweeks.FindAsync(g => g.SeasonId == seasonId, cancellationToken);
        return gameweeks.Select(g => new GameweekDto
        {
            SeasonId = g.SeasonId,
            WeekNumber = g.WeekNumber,
            Deadline = g.Deadline,
            IsLocked = g.IsLocked,
            CreatedAt = g.CreatedAt
        });
    }
}
