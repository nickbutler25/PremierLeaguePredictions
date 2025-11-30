namespace PremierLeaguePredictions.Application.DTOs;

public class PickDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string SeasonId { get; set; } = string.Empty;
    public int GameweekNumber { get; set; }
    public int TeamId { get; set; }
    public int Points { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public bool IsAutoAssigned { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Related data
    public TeamDto? Team { get; set; }
    public string? GameweekName { get; set; }
}

public class CreatePickRequest
{
    public string SeasonId { get; set; } = string.Empty;
    public int GameweekNumber { get; set; }
    public int TeamId { get; set; }
}

public class UpdatePickRequest
{
    public int TeamId { get; set; }
}

public class PickWithDetailsDto : PickDto
{
    public UserDto? User { get; set; }
    public GameweekDto? Gameweek { get; set; }
}
