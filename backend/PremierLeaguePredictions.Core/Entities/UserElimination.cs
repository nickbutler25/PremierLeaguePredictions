namespace PremierLeaguePredictions.Core.Entities;

public class UserElimination
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid SeasonId { get; set; }
    public Guid GameweekId { get; set; }
    public int Position { get; set; } // Position in league when eliminated
    public int TotalPoints { get; set; } // Total points when eliminated
    public DateTime EliminatedAt { get; set; }
    public Guid? EliminatedBy { get; set; } // Admin who triggered elimination

    // Navigation properties
    public User User { get; set; } = null!;
    public Season Season { get; set; } = null!;
    public Gameweek Gameweek { get; set; } = null!;
    public User? EliminatedByUser { get; set; }
}
