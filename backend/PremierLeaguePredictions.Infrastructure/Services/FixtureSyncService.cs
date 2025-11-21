using Microsoft.Extensions.Logging;
using PremierLeaguePredictions.Core.Entities;
using PremierLeaguePredictions.Core.Interfaces;

namespace PremierLeaguePredictions.Infrastructure.Services;

public interface IFixtureSyncService
{
    Task<(int created, int updated)> SyncTeamsAsync(CancellationToken cancellationToken = default);
    Task<(int fixturesCreated, int fixturesUpdated, int gameweeksCreated)> SyncFixturesAsync(int? season = null, CancellationToken cancellationToken = default);
    Task SyncFixtureResultsAsync(CancellationToken cancellationToken = default);
}

public class FixtureSyncService : IFixtureSyncService
{
    private readonly IFootballDataService _footballDataService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FixtureSyncService> _logger;

    public FixtureSyncService(
        IFootballDataService footballDataService,
        IUnitOfWork unitOfWork,
        ILogger<FixtureSyncService> logger)
    {
        _footballDataService = footballDataService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<(int created, int updated)> SyncTeamsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting teams sync from external API");

        var externalTeams = await _footballDataService.GetTeamsAsync(cancellationToken);
        var existingTeams = await _unitOfWork.Teams.GetAllAsync(cancellationToken);
        var existingTeamDict = existingTeams.ToDictionary(t => t.ExternalId);

        int created = 0;
        int updated = 0;

        foreach (var externalTeam in externalTeams)
        {
            if (existingTeamDict.TryGetValue(externalTeam.Id, out var existingTeam))
            {
                // Update existing team
                var hasChanges = false;
                var cleanedName = CleanTeamName(externalTeam.Name);
                var cleanedShortName = CleanTeamName(externalTeam.ShortName);

                if (existingTeam.Name != cleanedName)
                {
                    existingTeam.Name = cleanedName;
                    hasChanges = true;
                }
                if (existingTeam.ShortName != cleanedShortName)
                {
                    existingTeam.ShortName = cleanedShortName;
                    hasChanges = true;
                }
                if (existingTeam.LogoUrl != externalTeam.Crest)
                {
                    existingTeam.LogoUrl = externalTeam.Crest;
                    hasChanges = true;
                }

                if (hasChanges)
                {
                    existingTeam.UpdatedAt = DateTime.UtcNow;
                    _unitOfWork.Teams.Update(existingTeam);
                    updated++;
                }
            }
            else
            {
                // Create new team
                var newTeam = new Team
                {
                    Id = Guid.NewGuid(),
                    ExternalId = externalTeam.Id,
                    Name = CleanTeamName(externalTeam.Name),
                    ShortName = CleanTeamName(externalTeam.ShortName),
                    LogoUrl = externalTeam.Crest,
                    IsActive = true, // New teams are active by default
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _unitOfWork.Teams.AddAsync(newTeam, cancellationToken);
                created++;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Teams sync completed. Created: {Created}, Updated: {Updated}", created, updated);

        return (created, updated);
    }

    public async Task<(int fixturesCreated, int fixturesUpdated, int gameweeksCreated)> SyncFixturesAsync(int? season = null, CancellationToken cancellationToken = default)
    {
        if (season.HasValue)
        {
            _logger.LogInformation("Starting fixtures sync from external API for season {Season}", season);
        }
        else
        {
            _logger.LogInformation("Starting fixtures sync from external API for current season");
        }

        var externalFixtures = await _footballDataService.GetFixturesAsync(season, cancellationToken);
        var teams = await _unitOfWork.Teams.GetAllAsync(cancellationToken);
        var teamsByExternalId = teams.ToDictionary(t => t.ExternalId);

        var existingFixtures = await _unitOfWork.Fixtures.GetAllAsync(cancellationToken);
        var existingFixtureDict = existingFixtures
            .Where(f => f.ExternalId.HasValue)
            .ToDictionary(f => f.ExternalId!.Value);

        int fixturesCreated = 0;
        int fixturesUpdated = 0;
        int gameweeksCreated = 0;
        var createdGameweekNumbers = new HashSet<int>();

        foreach (var externalFixture in externalFixtures)
        {
            if (!teamsByExternalId.TryGetValue(externalFixture.HomeTeam.Id, out var homeTeam) ||
                !teamsByExternalId.TryGetValue(externalFixture.AwayTeam.Id, out var awayTeam))
            {
                _logger.LogWarning("Teams not found for fixture {FixtureId}", externalFixture.Id);
                continue;
            }

            // Find or create gameweek
            var gameweekNumber = externalFixture.Matchday ?? 1;
            var gameweeks = await _unitOfWork.Gameweeks.FindAsync(
                g => g.WeekNumber == gameweekNumber,
                cancellationToken);
            var gameweek = gameweeks.FirstOrDefault();

            if (gameweek == null)
            {
                // Get or create the current season
                var seasonYear = season ?? DateTime.UtcNow.Year;
                var seasonName = $"{seasonYear}/{seasonYear + 1}";

                var seasons = await _unitOfWork.Seasons.FindAsync(
                    s => s.IsActive,
                    cancellationToken);
                var currentSeason = seasons.FirstOrDefault();

                if (currentSeason == null)
                {
                    // Create the season if it doesn't exist
                    var startDate = new DateTime(seasonYear, 8, 1, 0, 0, 0, DateTimeKind.Utc); // Premier League typically starts in August
                    var endDate = new DateTime(seasonYear + 1, 5, 31, 23, 59, 59, DateTimeKind.Utc); // Ends in May

                    currentSeason = new Season
                    {
                        Id = Guid.NewGuid(),
                        Name = seasonName,
                        StartDate = startDate,
                        EndDate = endDate,
                        IsActive = true,
                        IsArchived = false,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    await _unitOfWork.Seasons.AddAsync(currentSeason, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Created season {SeasonName}", currentSeason.Name);
                }

                // Create the gameweek
                // Set deadline to the earliest fixture kickoff time for this gameweek
                var deadline = externalFixture.UtcDate.AddHours(-1); // 1 hour before first match

                gameweek = new Gameweek
                {
                    Id = Guid.NewGuid(),
                    SeasonId = currentSeason.Id,
                    WeekNumber = gameweekNumber,
                    Deadline = deadline,
                    IsLocked = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _unitOfWork.Gameweeks.AddAsync(gameweek, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Created gameweek {WeekNumber} for season {SeasonName}", gameweekNumber, currentSeason.Name);

                // Track created gameweek
                if (!createdGameweekNumbers.Contains(gameweekNumber))
                {
                    createdGameweekNumbers.Add(gameweekNumber);
                    gameweeksCreated++;
                }
            }

            if (existingFixtureDict.TryGetValue(externalFixture.Id, out var existingFixture))
            {
                // Update existing fixture
                var hasChanges = false;
                if (existingFixture.Status != externalFixture.Status)
                {
                    existingFixture.Status = externalFixture.Status;
                    hasChanges = true;
                }
                if (existingFixture.KickoffTime != externalFixture.UtcDate)
                {
                    existingFixture.KickoffTime = externalFixture.UtcDate;
                    hasChanges = true;
                }
                if (externalFixture.Score?.FullTime != null)
                {
                    if (existingFixture.HomeScore != externalFixture.Score.FullTime.Home)
                    {
                        existingFixture.HomeScore = externalFixture.Score.FullTime.Home;
                        hasChanges = true;
                    }
                    if (existingFixture.AwayScore != externalFixture.Score.FullTime.Away)
                    {
                        existingFixture.AwayScore = externalFixture.Score.FullTime.Away;
                        hasChanges = true;
                    }
                }

                if (hasChanges)
                {
                    existingFixture.UpdatedAt = DateTime.UtcNow;
                    _unitOfWork.Fixtures.Update(existingFixture);
                    fixturesUpdated++;
                }
            }
            else
            {
                // Create new fixture
                var newFixture = new Fixture
                {
                    Id = Guid.NewGuid(),
                    ExternalId = externalFixture.Id,
                    GameweekId = gameweek.Id,
                    HomeTeamId = homeTeam.Id,
                    AwayTeamId = awayTeam.Id,
                    HomeScore = externalFixture.Score?.FullTime?.Home,
                    AwayScore = externalFixture.Score?.FullTime?.Away,
                    KickoffTime = externalFixture.UtcDate,
                    Status = externalFixture.Status,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _unitOfWork.Fixtures.AddAsync(newFixture, cancellationToken);
                fixturesCreated++;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Fixtures sync completed. Created: {Created}, Updated: {Updated}, Gameweeks Created: {GameweeksCreated}",
            fixturesCreated, fixturesUpdated, gameweeksCreated);

        return (fixturesCreated, fixturesUpdated, gameweeksCreated);
    }

    public async Task SyncFixtureResultsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting fixture results sync");

        // Sync current season fixtures to get latest results
        await SyncFixturesAsync(null, cancellationToken);

        _logger.LogInformation("Fixture results sync completed");
    }

    private static string CleanTeamName(string? teamName)
    {
        if (string.IsNullOrWhiteSpace(teamName))
            return string.Empty;

        // Remove " FC" from the end of team names
        if (teamName.EndsWith(" FC", StringComparison.OrdinalIgnoreCase))
        {
            return teamName.Substring(0, teamName.Length - 3).Trim();
        }

        return teamName.Trim();
    }
}
