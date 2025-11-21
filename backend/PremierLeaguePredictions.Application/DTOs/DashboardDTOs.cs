namespace PremierLeaguePredictions.Application.DTOs;

public class DashboardDto
{
    public UserStatsDto User { get; set; } = null!;
    public Guid? CurrentGameweekId { get; set; }
    public List<PickDto> RecentPicks { get; set; } = new();
    public List<GameweekDto> UpcomingGameweeks { get; set; } = new();
}

public class UserStatsDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int TotalPoints { get; set; }
    public int TotalPicks { get; set; }
    public int TotalWins { get; set; }
    public int TotalDraws { get; set; }
    public int TotalLosses { get; set; }
}
