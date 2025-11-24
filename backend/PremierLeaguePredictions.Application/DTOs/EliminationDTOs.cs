namespace PremierLeaguePredictions.Application.DTOs;

public class UserEliminationDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public Guid SeasonId { get; set; }
    public Guid GameweekId { get; set; }
    public int GameweekNumber { get; set; }
    public int Position { get; set; }
    public int TotalPoints { get; set; }
    public DateTime EliminatedAt { get; set; }
    public Guid? EliminatedBy { get; set; }
    public string? EliminatedByName { get; set; }
}

public class ProcessEliminationsRequest
{
    public Guid GameweekId { get; set; }
}

public class ProcessEliminationsResponse
{
    public int PlayersEliminated { get; set; }
    public List<UserEliminationDto> EliminatedPlayers { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}

public class UpdateGameweekEliminationRequest
{
    public Guid GameweekId { get; set; }
    public int EliminationCount { get; set; }
}

public class EliminationConfigDto
{
    public Guid GameweekId { get; set; }
    public int WeekNumber { get; set; }
    public int EliminationCount { get; set; }
    public bool HasBeenProcessed { get; set; }
    public DateTime Deadline { get; set; }
}
