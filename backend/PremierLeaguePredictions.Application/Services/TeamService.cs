using Microsoft.Extensions.Logging;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Core.Entities;
using PremierLeaguePredictions.Core.Interfaces;

namespace PremierLeaguePredictions.Application.Services;

public class TeamService : ITeamService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TeamService> _logger;

    public TeamService(IUnitOfWork unitOfWork, ILogger<TeamService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<TeamDto?> GetTeamByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var team = await _unitOfWork.Teams.GetByIdAsync(id, cancellationToken);
        return team != null ? MapToDto(team) : null;
    }

    public async Task<IEnumerable<TeamDto>> GetAllTeamsAsync(CancellationToken cancellationToken = default)
    {
        var teams = await _unitOfWork.Teams.GetAllAsync(cancellationToken);
        return teams.Select(MapToDto);
    }

    public async Task<IEnumerable<TeamDto>> GetAvailableTeamsForGameweekAsync(Guid userId, Guid gameweekId, CancellationToken cancellationToken = default)
    {
        var allTeams = await _unitOfWork.Teams.GetAllAsync(cancellationToken);
        var userPicks = await _unitOfWork.Picks.FindAsync(p => p.UserId == userId, cancellationToken);
        var usedTeamIds = userPicks.Select(p => p.TeamId).ToHashSet();

        var availableTeams = allTeams.Where(t => !usedTeamIds.Contains(t.Id));
        return availableTeams.Select(MapToDto);
    }

    public async Task<TeamDto> CreateTeamAsync(CreateTeamRequest request, CancellationToken cancellationToken = default)
    {
        var team = new Team
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            ShortName = request.ShortName,
            LogoUrl = request.LogoUrl,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Teams.AddAsync(team, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Team created: {TeamName}", team.Name);
        return MapToDto(team);
    }

    public async Task<TeamDto> UpdateTeamAsync(Guid id, UpdateTeamRequest request, CancellationToken cancellationToken = default)
    {
        var team = await _unitOfWork.Teams.GetByIdAsync(id, cancellationToken);
        if (team == null) throw new KeyNotFoundException("Team not found");

        team.Name = request.Name;
        team.ShortName = request.ShortName;
        team.LogoUrl = request.LogoUrl;
        team.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Teams.Update(team);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Team updated: {TeamId}", id);
        return MapToDto(team);
    }

    public async Task DeleteTeamAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var team = await _unitOfWork.Teams.GetByIdAsync(id, cancellationToken);
        if (team == null) throw new KeyNotFoundException("Team not found");

        _unitOfWork.Teams.Remove(team);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Team deleted: {TeamId}", id);
    }

    private static TeamDto MapToDto(Team team) => new()
    {
        Id = team.Id,
        Name = team.Name,
        ShortName = team.ShortName,
        LogoUrl = team.LogoUrl
    };
}
