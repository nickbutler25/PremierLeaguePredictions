using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using PremierLeaguePredictions.Core.Entities;

namespace PremierLeaguePredictions.Infrastructure.Data;

public class DbSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DbSeeder> _logger;

    public DbSeeder(ApplicationDbContext context, ILogger<DbSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            // Seed users
            await SeedUsersAsync();

            // Seed teams
            await SeedTeamsAsync();

            // Seed seasons and gameweeks
            await SeedSeasonsAndGameweeksAsync();

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
        // Check if admin user already exists
        var adminExists = await _context.Users.AnyAsync(u => u.IsAdmin);

        if (!adminExists)
        {
            var adminUser = new User
            {
                Email = "admin@plpredictions.com",
                FirstName = "Admin",
                LastName = "User",
                IsActive = true,
                IsAdmin = true,
                IsPaid = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Users.Add(adminUser);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Admin user created: {Email}", adminUser.Email);
        }

        // Check if test user exists
        var testUserExists = await _context.Users.AnyAsync(u => u.Email == "test@plpredictions.com");

        if (!testUserExists)
        {
            var testUser = new User
            {
                Email = "test@plpredictions.com",
                FirstName = "Test",
                LastName = "User",
                IsActive = true,
                IsAdmin = false,
                IsPaid = true,
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
        // Check if teams already exist
        var teamsExist = await _context.Teams.AnyAsync();

        if (!teamsExist)
        {
            var teams = new[]
            {
                new Team { Name = "Arsenal", ShortName = "Arsenal", Code = "ARS", ExternalId = 57, LogoUrl = "https://resources.premierleague.com/premierleague/badges/50/t3.png", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new Team { Name = "Aston Villa", ShortName = "Aston Villa", Code = "AVL", ExternalId = 58, LogoUrl = "https://resources.premierleague.com/premierleague/badges/50/t7.png", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new Team { Name = "Chelsea", ShortName = "Chelsea", Code = "CHE", ExternalId = 61, LogoUrl = "https://resources.premierleague.com/premierleague/badges/50/t8.png", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new Team { Name = "Liverpool", ShortName = "Liverpool", Code = "LIV", ExternalId = 64, LogoUrl = "https://resources.premierleague.com/premierleague/badges/50/t14.png", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new Team { Name = "Manchester City", ShortName = "Man City", Code = "MCI", ExternalId = 65, LogoUrl = "https://resources.premierleague.com/premierleague/badges/50/t43.png", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new Team { Name = "Manchester United", ShortName = "Man United", Code = "MUN", ExternalId = 66, LogoUrl = "https://resources.premierleague.com/premierleague/badges/50/t1.png", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new Team { Name = "Newcastle United", ShortName = "Newcastle", Code = "NEW", ExternalId = 67, LogoUrl = "https://resources.premierleague.com/premierleague/badges/50/t4.png", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new Team { Name = "Tottenham Hotspur", ShortName = "Spurs", Code = "TOT", ExternalId = 73, LogoUrl = "https://resources.premierleague.com/premierleague/badges/50/t6.png", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            };

            _context.Teams.AddRange(teams);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Seeded {Count} teams", teams.Length);
        }
    }

    private async Task SeedSeasonsAndGameweeksAsync()
    {
        // Check if current season exists
        var currentSeasonName = $"{DateTime.UtcNow.Year}/{DateTime.UtcNow.Year + 1}";
        var seasonExists = await _context.Seasons.AnyAsync(s => s.Name == currentSeasonName);

        if (!seasonExists)
        {
            var season = new Season
            {
                Name = currentSeasonName,
                StartDate = new DateTime(DateTime.UtcNow.Year, 8, 1),
                EndDate = new DateTime(DateTime.UtcNow.Year + 1, 5, 31),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Seasons.Add(season);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Season created: {SeasonName}", season.Name);

            // Create gameweeks for the season
            var gameweeks = new List<Gameweek>();
            var startDate = season.StartDate;

            for (int i = 1; i <= 38; i++)
            {
                var gameweek = new Gameweek
                {
                    SeasonId = season.Name, // Season.Name is the primary key
                    WeekNumber = i,
                    Deadline = startDate.AddDays((i - 1) * 7), // Weekly gameweeks
                    IsLocked = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                gameweeks.Add(gameweek);
            }

            _context.Gameweeks.AddRange(gameweeks);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created {Count} gameweeks for season {SeasonName}", gameweeks.Count, season.Name);
        }
    }
}
