namespace PremierLeaguePredictions.Core.Entities;

/// <summary>
/// Defines the rules for picking teams in a season.
/// Rules can be configured separately for first half (weeks 1-19) and second half (weeks 20-38).
/// </summary>
public class PickRule
{
    public Guid Id { get; set; }
    public string SeasonId { get; set; } = string.Empty;

    /// <summary>
    /// Which half of the season this rule applies to: 1 (weeks 1-19) or 2 (weeks 20-38)
    /// </summary>
    public int Half { get; set; }

    /// <summary>
    /// Maximum number of times a team can be picked in this half.
    /// For example, 1 means each team can only be picked once.
    /// </summary>
    public int MaxTimesTeamCanBePicked { get; set; } = 1;

    /// <summary>
    /// Maximum number of times you can pick against the same opposition team in this half.
    /// For example, if set to 1, you can only target Manchester City as your opposition once.
    /// If set to 19, you could target them every week.
    /// </summary>
    public int MaxTimesOppositionCanBeTargeted { get; set; } = 1;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Season Season { get; set; } = null!;
}
