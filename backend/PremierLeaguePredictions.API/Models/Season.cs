namespace PremierLeaguePredictions.API.Models;

public class Season
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; } = false;
    public bool IsArchived { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<Gameweek> Gameweeks { get; set; } = new List<Gameweek>();
    public ICollection<TeamSelection> TeamSelections { get; set; } = new List<TeamSelection>();
}
