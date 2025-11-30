namespace PremierLeaguePredictions.Application.DTOs;

public class UserEliminationDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string SeasonId { get; set; } = string.Empty;
    public int GameweekNumber { get; set; }
    public int Position { get; set; }
    public int TotalPoints { get; set; }
    public DateTime EliminatedAt { get; set; }
    public Guid? EliminatedBy { get; set; }
    public string? EliminatedByName { get; set; }
}

public class ProcessEliminationsRequest
{
    public string SeasonId { get; set; } = string.Empty;
    public int GameweekNumber { get; set; }
}

public class ProcessEliminationsResponse
{
    public int PlayersEliminated { get; set; }
    public List<UserEliminationDto> EliminatedPlayers { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}

public class UpdateGameweekEliminationRequest
{
    public string SeasonId { get; set; } = string.Empty;
    public int GameweekNumber { get; set; }
    public int EliminationCount { get; set; }
}

public class EliminationConfigDto
{
    public string GameweekId { get; set; } = string.Empty; // Format: "{SeasonId}-{WeekNumber}"
    public string SeasonId { get; set; } = string.Empty;
    public int WeekNumber { get; set; }
    public int EliminationCount { get; set; }
    public bool HasBeenProcessed { get; set; }
    public DateTime Deadline { get; set; }
}
