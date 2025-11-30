namespace PremierLeaguePredictions.Application.DTOs;

public class AdminActionDto
{
    public Guid Id { get; set; }
    public Guid AdminUserId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class BackfillPickRequest
{
    public int GameweekNumber { get; set; }
    public int TeamId { get; set; }
}

public class BackfillPicksResponse
{
    public int PicksCreated { get; set; }
    public int PicksUpdated { get; set; }
    public int PicksSkipped { get; set; }
    public string Message { get; set; } = string.Empty;
}
