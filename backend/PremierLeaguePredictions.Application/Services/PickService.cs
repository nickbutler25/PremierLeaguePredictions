using Microsoft.Extensions.Logging;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Core.Entities;
using PremierLeaguePredictions.Core.Interfaces;

namespace PremierLeaguePredictions.Application.Services;

public class PickService : IPickService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PickService> _logger;

    public PickService(IUnitOfWork unitOfWork, ILogger<PickService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PickDto?> GetPickByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var pick = await _unitOfWork.Picks.GetByIdAsync(id, cancellationToken);
        if (pick == null) return null;

        var team = await _unitOfWork.Teams.GetByIdAsync(pick.TeamId, cancellationToken);

        return MapToDto(pick, team);
    }

    public async Task<IEnumerable<PickDto>> GetUserPicksAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var picks = await _unitOfWork.Picks.FindAsync(p => p.UserId == userId, cancellationToken);
        var picksList = picks.ToList();

        var teamIds = picksList.Select(p => p.TeamId).Distinct();
        var teams = new List<Team>();
        foreach (var teamId in teamIds)
        {
            var team = await _unitOfWork.Teams.GetByIdAsync(teamId, cancellationToken);
            if (team != null) teams.Add(team);
        }

        var gameweekIds = picksList.Select(p => p.GameweekId).Distinct();
        var gameweeks = new List<Gameweek>();
        foreach (var gameweekId in gameweekIds)
        {
            var gameweek = await _unitOfWork.Gameweeks.GetByIdAsync(gameweekId, cancellationToken);
            if (gameweek != null) gameweeks.Add(gameweek);
        }

        return picksList.Select(p => MapToDto(p, teams.FirstOrDefault(t => t.Id == p.TeamId), gameweeks.FirstOrDefault(g => g.Id == p.GameweekId)));
    }

    public async Task<PickDto> CreatePickAsync(Guid userId, CreatePickRequest request, CancellationToken cancellationToken = default)
    {
        // Verify gameweek exists first (to get season)
        var gameweek = await _unitOfWork.Gameweeks.GetByIdAsync(request.GameweekId, cancellationToken);
        if (gameweek == null)
        {
            throw new KeyNotFoundException("Gameweek not found");
        }

        // Check if user is approved for this season (applies to all users including admins)
        var participation = await _unitOfWork.SeasonParticipations.FindAsync(
            sp => sp.UserId == userId &&
                  sp.SeasonId == gameweek.SeasonId &&
                  sp.IsApproved,
            cancellationToken);

        if (!participation.Any())
        {
            _logger.LogWarning("User {UserId} attempted to create pick without approved participation", userId);
            throw new UnauthorizedAccessException("You must be approved to participate in this season");
        }

        // Check if pick already exists for this gameweek
        var existingPick = (await _unitOfWork.Picks.FindAsync(
            p => p.UserId == userId && p.GameweekId == request.GameweekId, cancellationToken)).FirstOrDefault();

        if (existingPick != null)
        {
            throw new InvalidOperationException("Pick already exists for this gameweek");
        }

        if (gameweek.Deadline < DateTime.UtcNow)
        {
            throw new InvalidOperationException("Gameweek deadline has passed");
        }

        // Verify team exists
        var team = await _unitOfWork.Teams.GetByIdAsync(request.TeamId, cancellationToken);
        if (team == null)
        {
            throw new KeyNotFoundException("Team not found");
        }

        var pick = new Pick
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GameweekId = request.GameweekId,
            TeamId = request.TeamId,
            Points = 0,
            GoalsFor = 0,
            GoalsAgainst = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Picks.AddAsync(pick, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Pick created for user {UserId} in gameweek {GameweekId}", userId, request.GameweekId);

        return MapToDto(pick, team);
    }

    public async Task<PickDto> UpdatePickAsync(Guid id, Guid userId, UpdatePickRequest request, CancellationToken cancellationToken = default)
    {
        var pick = await _unitOfWork.Picks.GetByIdAsync(id, cancellationToken);
        if (pick == null)
        {
            throw new KeyNotFoundException("Pick not found");
        }

        if (pick.UserId != userId)
        {
            throw new UnauthorizedAccessException("Not authorized to update this pick");
        }

        // Verify gameweek hasn't started
        var gameweek = await _unitOfWork.Gameweeks.GetByIdAsync(pick.GameweekId, cancellationToken);
        if (gameweek == null || gameweek.Deadline < DateTime.UtcNow)
        {
            throw new InvalidOperationException("Cannot update pick after gameweek deadline");
        }

        // Check if user is approved for this season (applies to all users including admins)
        var participation = await _unitOfWork.SeasonParticipations.FindAsync(
            sp => sp.UserId == userId &&
                  sp.SeasonId == gameweek.SeasonId &&
                  sp.IsApproved,
            cancellationToken);

        if (!participation.Any())
        {
            _logger.LogWarning("User {UserId} attempted to update pick without approved participation", userId);
            throw new UnauthorizedAccessException("You must be approved to participate in this season");
        }

        // Verify new team exists
        var team = await _unitOfWork.Teams.GetByIdAsync(request.TeamId, cancellationToken);
        if (team == null)
        {
            throw new KeyNotFoundException("Team not found");
        }

        pick.TeamId = request.TeamId;
        pick.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Picks.Update(pick);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Pick {PickId} updated by user {UserId}", id, userId);

        return MapToDto(pick, team);
    }

    public async Task DeletePickAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var pick = await _unitOfWork.Picks.GetByIdAsync(id, cancellationToken);
        if (pick == null)
        {
            throw new KeyNotFoundException("Pick not found");
        }

        if (pick.UserId != userId)
        {
            throw new UnauthorizedAccessException("Not authorized to delete this pick");
        }

        // Verify gameweek hasn't started
        var gameweek = await _unitOfWork.Gameweeks.GetByIdAsync(pick.GameweekId, cancellationToken);
        if (gameweek == null || gameweek.Deadline < DateTime.UtcNow)
        {
            throw new InvalidOperationException("Cannot delete pick after gameweek deadline");
        }

        _unitOfWork.Picks.Remove(pick);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Pick {PickId} deleted by user {UserId}", id, userId);
    }

    public async Task<IEnumerable<PickDto>> GetPicksByGameweekAsync(Guid gameweekId, CancellationToken cancellationToken = default)
    {
        var picks = await _unitOfWork.Picks.FindAsync(p => p.GameweekId == gameweekId, cancellationToken);
        var picksList = picks.ToList();

        var teamIds = picksList.Select(p => p.TeamId).Distinct();
        var teams = new List<Team>();
        foreach (var teamId in teamIds)
        {
            var team = await _unitOfWork.Teams.GetByIdAsync(teamId, cancellationToken);
            if (team != null) teams.Add(team);
        }

        return picksList.Select(p => MapToDto(p, teams.FirstOrDefault(t => t.Id == p.TeamId)));
    }

    private static PickDto MapToDto(Pick pick, Team? team, Gameweek? gameweek = null)
    {
        return new PickDto
        {
            Id = pick.Id,
            UserId = pick.UserId,
            GameweekId = pick.GameweekId,
            TeamId = pick.TeamId,
            Points = pick.Points,
            GoalsFor = pick.GoalsFor,
            GoalsAgainst = pick.GoalsAgainst,
            IsAutoAssigned = pick.IsAutoAssigned,
            CreatedAt = pick.CreatedAt,
            UpdatedAt = pick.UpdatedAt,
            Team = team != null ? new TeamDto
            {
                Id = team.Id,
                Name = team.Name,
                ShortName = team.ShortName,
                LogoUrl = team.LogoUrl
            } : null,
            GameweekName = gameweek != null ? $"Gameweek {gameweek.WeekNumber}" : null,
            GameweekNumber = gameweek?.WeekNumber
        };
    }
}
