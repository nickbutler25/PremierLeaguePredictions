namespace PremierLeaguePredictions.Core.Entities;

public class UserElimination
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string SeasonId { get; set; } = string.Empty;
    public int GameweekNumber { get; set; }
    /// <summary>
    /// Where the player finished: their place in the league at the moment they were eliminated.
    /// </summary>
    /// <remarks>
    /// Written once and never revisited. An eliminated player's season is over, so their
    /// position is settled — reading it from the live standings instead would have it move
    /// every time somebody still in scored.
    ///
    /// Positions do not collide between gameweeks: each run counts down from the number of
    /// players still in, and anyone eliminated earlier sits below that, having lasted less long.
    ///
    /// It previously held the order within the batch (1, 2, 3) while carrying this same
    /// description, so the eliminations page showed the bottom two as having finished first
    /// and second.
    /// </remarks>
    public int Position { get; set; }
    public int TotalPoints { get; set; } // Total points when eliminated
    public DateTime EliminatedAt { get; set; }
    public Guid? EliminatedBy { get; set; } // Admin who triggered elimination

    // Navigation properties
    public User User { get; set; } = null!;
    public Season Season { get; set; } = null!;
    public Gameweek Gameweek { get; set; } = null!;
    public User? EliminatedByUser { get; set; }
}
