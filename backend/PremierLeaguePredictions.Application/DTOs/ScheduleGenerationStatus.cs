using System.Text.Json.Serialization;

namespace PremierLeaguePredictions.Application.DTOs;

/// <summary>
/// State of a schedule generation run. Serialised as a string ("Running", "Succeeded",
/// "Failed") so the polling script in run-api-task.yml can match on it.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ScheduleRunState>))]
public enum ScheduleRunState
{
    Running,
    Succeeded,
    Failed
}

/// <summary>
/// Progress of the cron-jobs.org sync, reported as jobs are deleted and created.
/// </summary>
public record ScheduleSyncProgress(
    int JobsDeleted,
    int JobsToDelete,
    int JobsCreated,
    int JobsToCreate);

/// <summary>
/// Snapshot of a schedule generation run, returned by POST /schedule/generate (which starts
/// the run) and polled from GET /schedule/generate/status until it reaches a terminal state.
/// </summary>
public class ScheduleGenerationStatus
{
    public Guid RunId { get; set; }

    public ScheduleRunState State { get; set; } = ScheduleRunState.Running;

    /// <summary>What the plan covers, e.g. "2026-27-GW1". Null until the plan is built.</summary>
    public string? Scope { get; set; }

    /// <summary>Jobs in the generated plan. Null until the plan is built.</summary>
    public int? JobsPlanned { get; set; }

    public int JobsToDelete { get; set; }
    public int JobsDeleted { get; set; }
    public int JobsToCreate { get; set; }
    public int JobsCreated { get; set; }

    public string? Message { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>True once the run has finished, successfully or not.</summary>
    public bool IsComplete => State != ScheduleRunState.Running;

    public ScheduleGenerationStatus Clone() => (ScheduleGenerationStatus)MemberwiseClone();
}
