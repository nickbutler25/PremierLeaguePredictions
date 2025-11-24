namespace PremierLeaguePredictions.Application.DTOs;

public class LeagueStandingsDto
{
    public List<StandingEntryDto> Standings { get; set; } = new();
    public int TotalPlayers { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class StandingEntryDto
{
    public int Position { get; set; }
    public int Rank { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int TotalPoints { get; set; }
    public int PicksMade { get; set; }
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int GoalDifference { get; set; }
    public bool IsEliminated { get; set; }
    public int? EliminatedInGameweek { get; set; }
    public int? EliminationPosition { get; set; }
}
