namespace PremierLeaguePredictions.Core.Entities;

public class Season
{
    public string Name { get; set; } = string.Empty; // Primary Key
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; } = false;
    public bool IsArchived { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<Gameweek> Gameweeks { get; set; } = new List<Gameweek>();
    public ICollection<TeamSelection> TeamSelections { get; set; } = new List<TeamSelection>();
    public ICollection<SeasonParticipation> Participations { get; set; } = new List<SeasonParticipation>();
    public ICollection<UserElimination> Eliminations { get; set; } = new List<UserElimination>();
    public ICollection<PickRule> PickRules { get; set; } = new List<PickRule>();
}
