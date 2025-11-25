using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Core.Entities;
using PremierLeaguePredictions.Core.Interfaces;

namespace PremierLeaguePredictions.Application.Services;

public class AdminService : IAdminService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHubContext<Hub> _hubContext;
    private readonly ILogger<AdminService> _logger;

    public AdminService(
        IUnitOfWork unitOfWork,
        IHubContext<Hub> hubContext,
        ILogger<AdminService> logger)
    {
        _unitOfWork = unitOfWork;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task OverridePickAsync(Guid pickId, Guid newTeamId, string reason, CancellationToken cancellationToken = default)
    {
        var pick = await _unitOfWork.Picks.GetByIdAsync(pickId, cancellationToken);
        if (pick == null) throw new KeyNotFoundException("Pick not found");

        var oldTeamId = pick.TeamId;
        pick.TeamId = newTeamId;
        pick.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Picks.Update(pick);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogWarning("Admin override: Pick {PickId} changed from team {OldTeamId} to {NewTeamId}. Reason: {Reason}",
            pickId, oldTeamId, newTeamId, reason);
    }

    public async Task RecalculatePointsForGameweekAsync(Guid gameweekId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Recalculating points for gameweek {GameweekId}", gameweekId);
        
        var picks = await _unitOfWork.Picks.FindAsync(p => p.GameweekId == gameweekId, cancellationToken);
        var fixtures = await _unitOfWork.Fixtures.FindAsync(f => f.GameweekId == gameweekId, cancellationToken);
        
        foreach (var pick in picks)
        {
            var teamFixtures = fixtures.Where(f => 
                (f.HomeTeamId == pick.TeamId || f.AwayTeamId == pick.TeamId) && 
                f.Status == "FINISHED");

            int points = 0;
            int goalsFor = 0;
            int goalsAgainst = 0;

            foreach (var fixture in teamFixtures)
            {
                bool isHome = fixture.HomeTeamId == pick.TeamId;
                int teamScore = isHome ? fixture.HomeScore ?? 0 : fixture.AwayScore ?? 0;
                int opponentScore = isHome ? fixture.AwayScore ?? 0 : fixture.HomeScore ?? 0;

                goalsFor += teamScore;
                goalsAgainst += opponentScore;

                if (teamScore > opponentScore) points += 3;
                else if (teamScore == opponentScore) points += 1;
            }

            pick.Points = points;
            pick.GoalsFor = goalsFor;
            pick.GoalsAgainst = goalsAgainst;
            pick.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Picks.Update(pick);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Points recalculated for gameweek {GameweekId}", gameweekId);
    }

    public async Task RecalculateAllPointsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Recalculating all points");
        
        var gameweeks = await _unitOfWork.Gameweeks.GetAllAsync(cancellationToken);
        
        foreach (var gameweek in gameweeks)
        {
            await RecalculatePointsForGameweekAsync(gameweek.Id, cancellationToken);
        }
        
        _logger.LogInformation("All points recalculated");
    }

    public async Task<IEnumerable<AdminActionDto>> GetAdminActionsAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        var actions = await _unitOfWork.AdminActions.GetAllAsync(cancellationToken);

        return actions
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .Select(a => new AdminActionDto
            {
                Id = a.Id,
                AdminUserId = a.AdminUserId,
                ActionType = a.ActionType,
                Description = a.Details ?? string.Empty,
                CreatedAt = a.CreatedAt
            });
    }

    public async Task<Guid> CreateSeasonAsync(CreateSeasonRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating new season: {SeasonName}", request.Name);

        // Check if a season with the same name already exists
        var existingSeasons = await _unitOfWork.Seasons.FindAsync(s => s.Name == request.Name, cancellationToken);
        if (existingSeasons.Any())
        {
            _logger.LogWarning("Attempted to create duplicate season: {SeasonName}", request.Name);
            throw new InvalidOperationException($"A season with the name '{request.Name}' already exists.");
        }

        // Deactivate the current active season
        var activeSeasons = await _unitOfWork.Seasons.FindAsync(s => s.IsActive, cancellationToken);
        foreach (var activeSeason in activeSeasons)
        {
            activeSeason.IsActive = false;
            activeSeason.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Seasons.Update(activeSeason);
        }

        // Create the new season
        var newSeason = new Season
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = true,
            IsArchived = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Seasons.AddAsync(newSeason, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Season {SeasonName} created with ID {SeasonId}", request.Name, newSeason.Id);

        // Send SignalR notification to all clients to refresh dashboard
        try
        {
            await _hubContext.Clients.All.SendAsync(
                "SeasonCreated",
                new
                {
                    seasonId = newSeason.Id,
                    seasonName = newSeason.Name,
                    message = $"New season {newSeason.Name} has been created"
                },
                cancellationToken);

            _logger.LogInformation("SignalR notification sent for season creation: {SeasonName}", request.Name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send SignalR notification for season creation");
            // Don't throw - season was created successfully, notification failure is not critical
        }

        return newSeason.Id;
    }

    public async Task<IEnumerable<SeasonDto>> GetAllSeasonsAsync(CancellationToken cancellationToken = default)
    {
        var seasons = await _unitOfWork.Seasons.GetAllAsync(cancellationToken);
        return seasons
            .OrderByDescending(s => s.StartDate)
            .Select(s => new SeasonDto
            {
                Id = s.Id,
                Name = s.Name,
                StartDate = s.StartDate,
                EndDate = s.EndDate,
                IsActive = s.IsActive,
                IsArchived = s.IsArchived,
                CreatedAt = s.CreatedAt
            });
    }

    public async Task<SeasonDto?> GetActiveSeasonAsync(CancellationToken cancellationToken = default)
    {
        var seasons = await GetAllSeasonsAsync(cancellationToken);
        return seasons.FirstOrDefault(s => s.IsActive);
    }

    public async Task<IEnumerable<TeamStatusDto>> GetTeamStatusesAsync(CancellationToken cancellationToken = default)
    {
        var teams = await _unitOfWork.Teams.GetAllAsync(cancellationToken);
        return teams
            .OrderBy(t => t.Name)
            .Select(t => new TeamStatusDto
            {
                Id = t.Id,
                Name = t.Name,
                ShortName = t.ShortName,
                LogoUrl = t.LogoUrl,
                IsActive = t.IsActive
            });
    }

    public async Task UpdateTeamStatusAsync(Guid teamId, bool isActive, CancellationToken cancellationToken = default)
    {
        var team = await _unitOfWork.Teams.GetByIdAsync(teamId, cancellationToken);
        if (team == null) throw new KeyNotFoundException("Team not found");

        team.IsActive = isActive;
        team.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Teams.Update(team);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Team {TeamName} status updated to {Status}", team.Name, isActive ? "Active" : "Inactive");
    }

    public async Task<BackfillPicksResponse> BackfillPicksAsync(Guid userId, List<BackfillPickRequest> picks, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Backfilling picks for user {UserId}", userId);

        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
        if (user == null) throw new KeyNotFoundException("User not found");

        // Get all gameweeks
        var allGameweeks = await _unitOfWork.Gameweeks.GetAllAsync(cancellationToken);
        var gameweeksByNumber = allGameweeks.ToDictionary(g => g.WeekNumber);

        // Get all fixtures to calculate points
        var allFixtures = await _unitOfWork.Fixtures.GetAllAsync(cancellationToken);
        var fixturesByGameweek = allFixtures.GroupBy(f => f.GameweekId).ToDictionary(g => g.Key, g => g.ToList());

        // Get existing picks for this user
        var existingPicks = await _unitOfWork.Picks.FindAsync(p => p.UserId == userId, cancellationToken);
        var existingPicksByGameweek = existingPicks.ToDictionary(p => p.GameweekId);

        int picksCreated = 0;
        int picksUpdated = 0;
        int picksSkipped = 0;

        foreach (var pickRequest in picks)
        {
            if (!gameweeksByNumber.TryGetValue(pickRequest.GameweekNumber, out var gameweek))
            {
                _logger.LogWarning("Gameweek {GameweekNumber} not found, skipping", pickRequest.GameweekNumber);
                picksSkipped++;
                continue;
            }

            // Check if pick already exists
            if (existingPicksByGameweek.TryGetValue(gameweek.Id, out var existingPick))
            {
                // Update existing pick
                existingPick.TeamId = pickRequest.TeamId;
                existingPick.UpdatedAt = DateTime.UtcNow;

                // Recalculate points
                CalculatePickPoints(existingPick, fixturesByGameweek.GetValueOrDefault(gameweek.Id, new List<Fixture>()));

                _unitOfWork.Picks.Update(existingPick);
                picksUpdated++;
            }
            else
            {
                // Create new pick
                var newPick = new Pick
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    GameweekId = gameweek.Id,
                    TeamId = pickRequest.TeamId,
                    IsAutoAssigned = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // Calculate points
                CalculatePickPoints(newPick, fixturesByGameweek.GetValueOrDefault(gameweek.Id, new List<Fixture>()));

                await _unitOfWork.Picks.AddAsync(newPick, cancellationToken);
                picksCreated++;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Backfill completed. Created: {Created}, Updated: {Updated}, Skipped: {Skipped}",
            picksCreated, picksUpdated, picksSkipped);

        return new BackfillPicksResponse
        {
            PicksCreated = picksCreated,
            PicksUpdated = picksUpdated,
            PicksSkipped = picksSkipped,
            Message = $"Backfilled {picksCreated + picksUpdated} picks successfully"
        };
    }

    private void CalculatePickPoints(Pick pick, List<Fixture> gameweekFixtures)
    {
        var teamFixtures = gameweekFixtures.Where(f =>
            (f.HomeTeamId == pick.TeamId || f.AwayTeamId == pick.TeamId) &&
            f.Status == "FINISHED");

        int points = 0;
        int goalsFor = 0;
        int goalsAgainst = 0;

        foreach (var fixture in teamFixtures)
        {
            bool isHome = fixture.HomeTeamId == pick.TeamId;
            int teamScore = isHome ? fixture.HomeScore ?? 0 : fixture.AwayScore ?? 0;
            int opponentScore = isHome ? fixture.AwayScore ?? 0 : fixture.HomeScore ?? 0;

            goalsFor += teamScore;
            goalsAgainst += opponentScore;

            if (teamScore > opponentScore) points += 3;
            else if (teamScore == opponentScore) points += 1;
        }

        pick.Points = points;
        pick.GoalsFor = goalsFor;
        pick.GoalsAgainst = goalsAgainst;
    }

    public async Task<object> GetGameweeksDebugInfoAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var allGameweeks = await _unitOfWork.Gameweeks.GetAllAsync(cancellationToken);
        var allFixtures = await _unitOfWork.Fixtures.GetAllAsync(cancellationToken);

        var gameweeksInfo = allGameweeks.OrderBy(g => g.WeekNumber).Select(g =>
        {
            var fixtures = allFixtures.Where(f => f.GameweekId == g.Id).ToList();
            var fixtureStatuses = fixtures.GroupBy(f => f.Status)
                .ToDictionary(grp => grp.Key, grp => grp.Count());

            var hasUnfinishedFixtures = fixtures.Any(f =>
                f.Status != "FINISHED" &&
                f.Status != "CANCELLED" &&
                f.Status != "POSTPONED");

            var deadlinePassed = g.Deadline < now;
            var isInProgress = deadlinePassed && hasUnfinishedFixtures;

            return new
            {
                WeekNumber = g.WeekNumber,
                Deadline = g.Deadline,
                DeadlinePassed = deadlinePassed,
                IsLocked = g.IsLocked,
                TotalFixtures = fixtures.Count,
                FixtureStatuses = fixtureStatuses,
                HasUnfinishedFixtures = hasUnfinishedFixtures,
                IsInProgress = isInProgress,
                FixtureDetails = fixtures.Select(f => new
                {
                    KickoffTime = f.KickoffTime,
                    Status = f.Status,
                    HomeScore = f.HomeScore,
                    AwayScore = f.AwayScore
                }).ToList()
            };
        }).ToList();

        return new
        {
            CurrentTime = now,
            Gameweeks = gameweeksInfo
        };
    }
}
