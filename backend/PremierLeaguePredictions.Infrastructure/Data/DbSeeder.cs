using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using PremierLeaguePredictions.Core.Entities;

namespace PremierLeaguePredictions.Infrastructure.Data;

public class DbSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DbSeeder> _logger;

    // Dedicated E2E test season — separate from real production seasons
    private const string E2eSeasonName = "E2E-TEST";

    public DbSeeder(ApplicationDbContext context, ILogger<DbSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            await SeedUsersAsync();
            await SeedTeamsAsync();
            await SeedE2eSeasonAsync();
            await SeedSeasonParticipationsAsync();

            _logger.LogInformation("Database seeding completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding database");
            throw;
        }
    }

    private async Task SeedUsersAsync()
    {
        var adminExists = await _context.Users.AnyAsync(u => u.IsAdmin);

        if (!adminExists)
        {
            var adminUser = new User
            {
                Email = "admin@plpredictions.com",
                FirstName = "Admin",
                LastName = "User",
                IsAdmin = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Users.Add(adminUser);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Admin user created: {Email}", adminUser.Email);
        }

        var testUserExists = await _context.Users.AnyAsync(u => u.Email == "test@plpredictions.com");

        if (!testUserExists)
        {
            var testUser = new User
            {
                Email = "test@plpredictions.com",
                FirstName = "Test",
                LastName = "User",
                IsAdmin = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Users.Add(testUser);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Test user created: {Email}", testUser.Email);
        }
    }

    private async Task SeedTeamsAsync()
    {
        var teamsExist = await _context.Teams.AnyAsync();

        if (!teamsExist)
        {
            var teams = new[]
            {
                new Team { Name = "Arsenal", MediumName = "Arsenal", Code = "ARS", ExternalId = 57, LogoUrl = "https://resources.premierleague.com/premierleague/badges/50/t3.png", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new Team { Name = "Aston Villa", MediumName = "Aston Villa", Code = "AVL", ExternalId = 58, LogoUrl = "https://resources.premierleague.com/premierleague/badges/50/t7.png", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new Team { Name = "Chelsea", MediumName = "Chelsea", Code = "CHE", ExternalId = 61, LogoUrl = "https://resources.premierleague.com/premierleague/badges/50/t8.png", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new Team { Name = "Liverpool", MediumName = "Liverpool", Code = "LIV", ExternalId = 64, LogoUrl = "https://resources.premierleague.com/premierleague/badges/50/t14.png", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new Team { Name = "Manchester City", MediumName = "Man City", Code = "MCI", ExternalId = 65, LogoUrl = "https://resources.premierleague.com/premierleague/badges/50/t43.png", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new Team { Name = "Manchester United", MediumName = "Man United", Code = "MUN", ExternalId = 66, LogoUrl = "https://resources.premierleague.com/premierleague/badges/50/t1.png", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new Team { Name = "Newcastle United", MediumName = "Newcastle", Code = "NEW", ExternalId = 67, LogoUrl = "https://resources.premierleague.com/premierleague/badges/50/t4.png", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new Team { Name = "Tottenham Hotspur", MediumName = "Spurs", Code = "TOT", ExternalId = 73, LogoUrl = "https://resources.premierleague.com/premierleague/badges/50/t6.png", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            };

            _context.Teams.AddRange(teams);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Seeded {Count} teams", teams.Length);
        }
    }

    /// <summary>
    /// Creates a dedicated E2E test season with rolling future gameweek deadlines.
    /// Uses a fixed season name to avoid polluting production seasons.
    /// The E2E season is made active whenever no real active season exists (always the
    /// case in the isolated E2E/CI database), so picks/fixtures/gameweeks — all scoped to
    /// the active season — have a working current season. In dev, where a real active
    /// season exists, it stays inactive so it never shadows the real one.
    /// Gameweek deadlines are always regenerated to be in the future so the
    /// dashboard always has upcoming content regardless of when CI runs.
    /// </summary>
    private async Task SeedE2eSeasonAsync()
    {
        var now = DateTime.UtcNow;

        var season = await _context.Seasons.FirstOrDefaultAsync(s => s.Name == E2eSeasonName);

        if (season == null)
        {
            season = new Season
            {
                Name = E2eSeasonName,
                StartDate = now.Date,
                EndDate = now.Date.AddYears(1),
                IsActive = false, // set below once we know whether a real active season exists
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.Seasons.Add(season);
            _logger.LogInformation("E2E test season created: {SeasonName}", E2eSeasonName);
        }

        // Idempotently ensure the E2E season is active iff there's no real active season.
        // Runs whether the season was just created or already existed (e.g. a local re-seed
        // where it was previously left inactive), so the E2E database always has exactly one
        // active season for the app to scope against.
        var otherActiveSeasonExists = await _context.Seasons
            .AnyAsync(s => s.IsActive && s.Name != E2eSeasonName);
        var shouldBeActive = !otherActiveSeasonExists;
        if (season.IsActive != shouldBeActive)
        {
            season.IsActive = shouldBeActive;
            season.UpdatedAt = now;
        }

        await _context.SaveChangesAsync();

        // Ensure there are upcoming gameweeks with future deadlines.
        // If none exist with a future deadline, add new ones (rolling forward).
        var futureGameweeksExist = await _context.Gameweeks
            .AnyAsync(g => g.SeasonId == E2eSeasonName && g.Deadline > now);

        if (!futureGameweeksExist)
        {
            var maxWeekNumber = await _context.Gameweeks
                .Where(g => g.SeasonId == E2eSeasonName)
                .Select(g => (int?)g.WeekNumber)
                .MaxAsync() ?? 0;

            var newGameweeks = Enumerable.Range(1, 3).Select(i => new Gameweek
            {
                SeasonId = E2eSeasonName,
                WeekNumber = maxWeekNumber + i,
                Deadline = now.AddDays(i * 7),
                IsLocked = false,
                CreatedAt = now,
                UpdatedAt = now
            }).ToList();

            _context.Gameweeks.AddRange(newGameweeks);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created {Count} upcoming E2E gameweeks (starting from week {Start})",
                newGameweeks.Count, maxWeekNumber + 1);
        }
    }

    /// <summary>
    /// Ensures both test users have approved season participation in:
    /// 1. All currently active seasons (so DashboardService participation check passes)
    /// 2. The E2E test season (so future gameweeks are accessible)
    /// </summary>
    private async Task SeedSeasonParticipationsAsync()
    {
        var now = DateTime.UtcNow;

        var adminUser = await _context.Users.FirstOrDefaultAsync(u => u.IsAdmin);
        var testUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == "test@plpredictions.com");

        var usersToSeed = new[] { adminUser, testUser }.Where(u => u != null).ToList();

        // Cover all active seasons (real ones) + the E2E test season
        var activeSeasonIds = await _context.Seasons
            .Where(s => s.IsActive)
            .Select(s => s.Name)
            .ToListAsync();

        var seasonIds = activeSeasonIds.Union(new[] { E2eSeasonName }).ToList();

        foreach (var user in usersToSeed)
        {
            foreach (var seasonId in seasonIds)
            {
                var participationExists = await _context.SeasonParticipations
                    .AnyAsync(sp => sp.UserId == user!.Id && sp.SeasonId == seasonId);

                if (!participationExists)
                {
                    _context.SeasonParticipations.Add(new SeasonParticipation
                    {
                        UserId = user!.Id,
                        SeasonId = seasonId,
                        IsApproved = true,
                        RequestedAt = now,
                        ApprovedAt = now,
                        ApprovedByUserId = adminUser?.Id,
                        CreatedAt = now,
                        UpdatedAt = now
                    });

                    _logger.LogInformation("Created approved season participation for {Email} in {Season}",
                        user.Email, seasonId);
                }
            }
        }

        await _context.SaveChangesAsync();
    }
}
