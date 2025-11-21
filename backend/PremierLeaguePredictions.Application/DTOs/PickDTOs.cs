namespace PremierLeaguePredictions.Application.DTOs;

public class PickDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid GameweekId { get; set; }
    public Guid TeamId { get; set; }
    public int Points { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public bool IsAutoAssigned { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Related data
    public TeamDto? Team { get; set; }
    public string? GameweekName { get; set; }
    public int? GameweekNumber { get; set; }
}

public class CreatePickRequest
{
    public Guid GameweekId { get; set; }
    public Guid TeamId { get; set; }
}

public class UpdatePickRequest
{
    public Guid TeamId { get; set; }
}

public class PickWithDetailsDto : PickDto
{
    public UserDto? User { get; set; }
    public GameweekDto? Gameweek { get; set; }
}
