namespace PremierLeaguePredictions.Application.DTOs;

public class GameweekDto
{
    public Guid Id { get; set; }
    public Guid SeasonId { get; set; }
    public int WeekNumber { get; set; }
    public DateTime Deadline { get; set; }
    public bool IsLocked { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateGameweekRequest
{
    public Guid SeasonId { get; set; }
    public int WeekNumber { get; set; }
    public DateTime Deadline { get; set; }
}

public class UpdateGameweekRequest
{
    public DateTime? Deadline { get; set; }
    public bool? IsLocked { get; set; }
}

public class GameweekWithFixturesDto : GameweekDto
{
    public List<FixtureDto> Fixtures { get; set; } = new();
}
