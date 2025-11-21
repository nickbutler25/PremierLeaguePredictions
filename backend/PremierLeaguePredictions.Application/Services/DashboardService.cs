using Microsoft.Extensions.Logging;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Core.Interfaces;

namespace PremierLeaguePredictions.Application.Services;

public class DashboardService : IDashboardService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(IUnitOfWork unitOfWork, ILogger<DashboardService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<DashboardDto> GetUserDashboardAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
        if (user == null) throw new KeyNotFoundException("User not found");

        var picks = await _unitOfWork.Picks.FindAsync(p => p.UserId == userId, cancellationToken);
        var picksList = picks.ToList();

        // Get all gameweeks to determine which are completed
        var allGameweeks = await _unitOfWork.Gameweeks.GetAllAsync(cancellationToken);
        var completedGameweekIds = allGameweeks
            .Where(g => g.Deadline < DateTime.UtcNow)
            .Select(g => g.Id)
            .ToHashSet();

        // Only count W/D/L for picks in completed gameweeks
        var completedPicks = picksList.Where(p => completedGameweekIds.Contains(p.GameweekId)).ToList();

        int totalWins = 0;
        int totalDraws = 0;
        int totalLosses = 0;

        foreach (var pick in completedPicks)
        {
            if (pick.Points == 3) totalWins++;
            else if (pick.Points == 1) totalDraws++;
            else if (pick.Points == 0) totalLosses++;
        }

        var totalPoints = picksList.Sum(p => p.Points);
        var totalPicks = completedPicks.Count; // Only count completed picks

        // Get upcoming gameweeks
        var upcomingGameweeks = await _unitOfWork.Gameweeks.FindAsync(
            g => !g.IsLocked && g.Deadline > DateTime.UtcNow,
            cancellationToken);
        var upcomingGameweeksList = upcomingGameweeks
            .OrderBy(g => g.Deadline)
            .Take(3)
            .Select(g => new GameweekDto
            {
                Id = g.Id,
                SeasonId = g.SeasonId,
                WeekNumber = g.WeekNumber,
                Deadline = g.Deadline,
                IsLocked = g.IsLocked
            })
            .ToList();

        var currentGameweek = upcomingGameweeksList.FirstOrDefault();

        // Get recent picks
        var recentPicks = picksList
            .OrderByDescending(p => p.CreatedAt)
            .Take(5)
            .Select(p => new PickDto
            {
                Id = p.Id,
                UserId = p.UserId,
                GameweekId = p.GameweekId,
                TeamId = p.TeamId,
                Points = p.Points,
                GoalsFor = p.GoalsFor,
                GoalsAgainst = p.GoalsAgainst,
                IsAutoAssigned = p.IsAutoAssigned
            })
            .ToList();

        return new DashboardDto
        {
            User = new UserStatsDto
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                TotalPoints = totalPoints,
                TotalPicks = totalPicks,
                TotalWins = totalWins,
                TotalDraws = totalDraws,
                TotalLosses = totalLosses
            },
            CurrentGameweekId = currentGameweek?.Id,
            UpcomingGameweeks = upcomingGameweeksList,
            RecentPicks = recentPicks
        };
    }
}
