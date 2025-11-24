namespace PremierLeaguePredictions.Application.DTOs;

public class SeasonParticipationDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid SeasonId { get; set; }
    public bool IsApproved { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }

    // Additional info
    public string? UserFirstName { get; set; }
    public string? UserLastName { get; set; }
    public string? UserEmail { get; set; }
    public string? SeasonName { get; set; }
    public string? ApprovedByUserName { get; set; }
}

public class CreateSeasonParticipationRequest
{
    public Guid SeasonId { get; set; }
}

public class ApproveSeasonParticipationRequest
{
    public Guid ParticipationId { get; set; }
    public bool IsApproved { get; set; }
}

public class PendingApprovalDto
{
    public Guid ParticipationId { get; set; }
    public Guid UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public Guid SeasonId { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public bool IsPaid { get; set; }
}
