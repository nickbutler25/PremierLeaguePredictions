using PremierLeaguePredictions.Application.DTOs;

namespace PremierLeaguePredictions.Infrastructure.Services;

/// <summary>
/// Service for generating and managing GitHub Actions workflow files
/// </summary>
public interface IGitHubWorkflowService
{
    /// <summary>
    /// Generates a GitHub Actions workflow YAML from a schedule plan
    /// and commits it to the repository
    /// </summary>
    Task<ScheduleGenerationResponse> GenerateAndCommitWorkflowAsync(
        SchedulePlan plan,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates the YAML content for a GitHub Actions workflow
    /// </summary>
    string GenerateWorkflowYaml(SchedulePlan plan);

    /// <summary>
    /// Gets the filename for this week's workflow
    /// Format: weekly-jobs-YYYY-WW.yml (e.g., weekly-jobs-2025-W49.yml)
    /// </summary>
    string GetWeeklyWorkflowFilename(DateTime date);
}
