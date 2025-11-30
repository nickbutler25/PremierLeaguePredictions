using Microsoft.Extensions.Logging;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Core.Entities;
using PremierLeaguePredictions.Core.Interfaces;

namespace PremierLeaguePredictions.Application.Services;

public class FixtureService : IFixtureService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FixtureService> _logger;

    public FixtureService(IUnitOfWork unitOfWork, ILogger<FixtureService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<FixtureDto?> GetFixtureByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var fixture = await _unitOfWork.Fixtures.GetByIdAsync(id, cancellationToken);
        return fixture != null ? MapToDto(fixture) : null;
    }

    public async Task<IEnumerable<FixtureDto>> GetAllFixturesAsync(CancellationToken cancellationToken = default)
    {
        var fixtures = await _unitOfWork.Fixtures.GetAllAsync(cancellationToken);
        var teams = await _unitOfWork.Teams.GetAllAsync(cancellationToken);
        var gameweeks = await _unitOfWork.Gameweeks.GetAllAsync(cancellationToken);

        var teamDict = teams.ToDictionary(t => t.Id);
        var gameweekDict = gameweeks.ToDictionary(g => $"{g.SeasonId}-{g.WeekNumber}");

        return fixtures.Select(f => MapToDto(f, teamDict, gameweekDict));
    }

    public async Task<IEnumerable<FixtureDto>> GetFixturesByGameweekAsync(string seasonId, int gameweekNumber, CancellationToken cancellationToken = default)
    {
        var fixtures = await _unitOfWork.Fixtures.FindAsync(f => f.SeasonId == seasonId && f.GameweekNumber == gameweekNumber, cancellationToken);
        return fixtures.Select(MapToDto);
    }

    public async Task<IEnumerable<FixtureDto>> GetFixturesByTeamIdAsync(int teamId, CancellationToken cancellationToken = default)
    {
        var fixtures = await _unitOfWork.Fixtures.FindAsync(
            f => f.HomeTeamId == teamId || f.AwayTeamId == teamId,
            cancellationToken);
        return fixtures.Select(MapToDto);
    }

    public async Task<FixtureDto> CreateFixtureAsync(CreateFixtureRequest request, CancellationToken cancellationToken = default)
    {
        var fixture = new Fixture
        {
            Id = Guid.NewGuid(),
            SeasonId = request.SeasonId,
            GameweekNumber = request.GameweekNumber,
            HomeTeamId = request.HomeTeamId,
            AwayTeamId = request.AwayTeamId,
            KickoffTime = request.KickoffTime,
            Status = "SCHEDULED",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Fixtures.AddAsync(fixture, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Fixture created: {FixtureId}", fixture.Id);
        return MapToDto(fixture);
    }

    public async Task<FixtureDto> UpdateFixtureAsync(Guid id, UpdateFixtureRequest request, CancellationToken cancellationToken = default)
    {
        var fixture = await _unitOfWork.Fixtures.GetByIdAsync(id, cancellationToken);
        if (fixture == null) throw new KeyNotFoundException("Fixture not found");

        if (request.HomeScore.HasValue) fixture.HomeScore = request.HomeScore.Value;
        if (request.AwayScore.HasValue) fixture.AwayScore = request.AwayScore.Value;
        if (request.Status != null) fixture.Status = request.Status;
        if (request.KickoffTime.HasValue) fixture.KickoffTime = request.KickoffTime.Value;

        fixture.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Fixtures.Update(fixture);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Fixture updated: {FixtureId}", id);
        return MapToDto(fixture);
    }

    public async Task DeleteFixtureAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var fixture = await _unitOfWork.Fixtures.GetByIdAsync(id, cancellationToken);
        if (fixture == null) throw new KeyNotFoundException("Fixture not found");

        _unitOfWork.Fixtures.Remove(fixture);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Fixture deleted: {FixtureId}", id);
    }

    public async Task UpdateFixtureScoresAsync(Guid id, int homeScore, int awayScore, CancellationToken cancellationToken = default)
    {
        var fixture = await _unitOfWork.Fixtures.GetByIdAsync(id, cancellationToken);
        if (fixture == null) throw new KeyNotFoundException("Fixture not found");

        fixture.HomeScore = homeScore;
        fixture.AwayScore = awayScore;
        fixture.Status = "FINISHED";
        fixture.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Fixtures.Update(fixture);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Fixture scores updated: {FixtureId} - {HomeScore}:{AwayScore}", id, homeScore, awayScore);
    }

    private static FixtureDto MapToDto(Fixture fixture) => new()
    {
        Id = fixture.Id,
        SeasonId = fixture.SeasonId,
        GameweekNumber = fixture.GameweekNumber,
        HomeTeamId = fixture.HomeTeamId,
        AwayTeamId = fixture.AwayTeamId,
        HomeScore = fixture.HomeScore,
        AwayScore = fixture.AwayScore,
        KickoffTime = fixture.KickoffTime,
        Status = fixture.Status
    };

    private static FixtureDto MapToDto(Fixture fixture, Dictionary<int, Team> teamDict, Dictionary<string, Gameweek> gameweekDict)
    {
        var dto = MapToDto(fixture);

        // Populate gameweek number
        if (gameweekDict.TryGetValue($"{fixture.SeasonId}-{fixture.GameweekNumber}", out var gameweek))
        {
            dto.GameweekNumber = gameweek.WeekNumber;
        }

        // Populate team details if available
        if (teamDict.TryGetValue(fixture.HomeTeamId, out var homeTeam))
        {
            dto.HomeTeam = new TeamDto
            {
                Id = homeTeam.Id,
                Name = homeTeam.Name,
                ShortName = homeTeam.ShortName,
                LogoUrl = homeTeam.LogoUrl,
                ExternalApiId = homeTeam.ExternalId
            };
        }

        if (teamDict.TryGetValue(fixture.AwayTeamId, out var awayTeam))
        {
            dto.AwayTeam = new TeamDto
            {
                Id = awayTeam.Id,
                Name = awayTeam.Name,
                ShortName = awayTeam.ShortName,
                LogoUrl = awayTeam.LogoUrl,
                ExternalApiId = awayTeam.ExternalId
            };
        }

        return dto;
    }
}
