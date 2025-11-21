namespace PremierLeaguePredictions.Core.Entities;

public class Team
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? Code { get; set; }
    public string? LogoUrl { get; set; }
    public int ExternalId { get; set; } // Football Data API ID
    public bool IsActive { get; set; } = true; // Teams are active by default
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<Fixture> HomeFixtures { get; set; } = new List<Fixture>();
    public ICollection<Fixture> AwayFixtures { get; set; } = new List<Fixture>();
    public ICollection<Pick> Picks { get; set; } = new List<Pick>();
    public ICollection<TeamSelection> TeamSelections { get; set; } = new List<TeamSelection>();
}
