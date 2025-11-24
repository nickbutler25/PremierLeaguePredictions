namespace PremierLeaguePredictions.Core.Entities;

public class SeasonParticipation
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid SeasonId { get; set; }
    public bool IsApproved { get; set; } = false;
    public DateTime RequestedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public Season Season { get; set; } = null!;
    public User? ApprovedByUser { get; set; }
}
