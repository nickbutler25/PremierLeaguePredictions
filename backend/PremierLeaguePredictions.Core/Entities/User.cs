namespace PremierLeaguePredictions.Core.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? PhotoUrl { get; set; }
    public string? GoogleId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsAdmin { get; set; } = false;
    public bool IsPaid { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<Pick> Picks { get; set; } = new List<Pick>();
    public ICollection<TeamSelection> TeamSelections { get; set; } = new List<TeamSelection>();
    public ICollection<EmailNotification> EmailNotifications { get; set; } = new List<EmailNotification>();
    public ICollection<AdminAction> AdminActionsPerformed { get; set; } = new List<AdminAction>();
    public ICollection<AdminAction> AdminActionsReceived { get; set; } = new List<AdminAction>();
    public ICollection<SeasonParticipation> SeasonParticipations { get; set; } = new List<SeasonParticipation>();
    public ICollection<SeasonParticipation> ApprovedParticipations { get; set; } = new List<SeasonParticipation>();
    public ICollection<UserElimination> Eliminations { get; set; } = new List<UserElimination>();
    public ICollection<UserElimination> EliminationsTriggered { get; set; } = new List<UserElimination>();
}
