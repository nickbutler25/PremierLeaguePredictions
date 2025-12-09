using System.Globalization;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PremierLeaguePredictions.Application.DTOs;

namespace PremierLeaguePredictions.Infrastructure.Services;

/// <summary>
/// Service for generating and managing GitHub Actions workflow files
/// Converts SchedulePlans into YAML workflows and commits them via GitHub API
/// </summary>
public class GitHubWorkflowService : IGitHubWorkflowService
{
    private readonly GitHubApiClient _githubClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GitHubWorkflowService> _logger;
    private readonly string _apiBaseUrl;

    public GitHubWorkflowService(
        GitHubApiClient githubClient,
        IConfiguration configuration,
        ILogger<GitHubWorkflowService> logger)
    {
        _githubClient = githubClient;
        _configuration = configuration;
        _logger = logger;

        _apiBaseUrl = configuration["ApiBaseUrl"]
            ?? throw new InvalidOperationException("ApiBaseUrl not configured");
    }

    public async Task<ScheduleGenerationResponse> GenerateAndCommitWorkflowAsync(
        SchedulePlan plan,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting workflow generation for week {WeekNumber}", plan.WeekNumber);

            // Generate YAML content
            var yamlContent = GenerateWorkflowYaml(plan);

            // Get filename for this week
            var filename = GetWeeklyWorkflowFilename(plan.StartDate);
            var workflowPath = $".github/workflows/{filename}";

            _logger.LogDebug("Generated workflow YAML ({Length} chars) for file: {Filename}",
                yamlContent.Length, filename);

            // Clean up old workflow files (keep only current week)
            await CleanupOldWorkflowsAsync(filename, cancellationToken);

            // Check if file already exists (for updates)
            var existingFile = await _githubClient.GetFileAsync(workflowPath, cancellationToken);

            // Create or update the workflow file
            var commitMessage = existingFile == null
                ? $"Create weekly jobs for {plan.WeekNumber} ({plan.Jobs.Count} jobs)"
                : $"Update weekly jobs for {plan.WeekNumber} ({plan.Jobs.Count} jobs)";

            var commitSha = await _githubClient.CreateOrUpdateFileAsync(
                workflowPath,
                yamlContent,
                commitMessage,
                existingFile?.Sha,
                cancellationToken
            );

            _logger.LogInformation("Successfully committed workflow file: {Path}, SHA: {Sha}",
                workflowPath, commitSha);

            return new ScheduleGenerationResponse
            {
                Success = true,
                Message = $"Generated {plan.Jobs.Count} jobs for week {plan.WeekNumber}",
                WorkflowFile = workflowPath,
                JobCount = plan.Jobs.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate and commit workflow");
            return new ScheduleGenerationResponse
            {
                Success = false,
                Message = $"Failed to generate workflow: {ex.Message}",
                WorkflowFile = null,
                JobCount = 0
            };
        }
    }

    public string GenerateWorkflowYaml(SchedulePlan plan)
    {
        var sb = new StringBuilder();

        // Workflow name and trigger
        sb.AppendLine($"name: Weekly Jobs - {plan.WeekNumber}");
        sb.AppendLine();
        sb.AppendLine("on:");
        sb.AppendLine("  schedule:");

        // Generate unique cron expressions for schedule triggers
        var cronExpressions = plan.Jobs
            .Select(j => j.ToCronExpression())
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        foreach (var cron in cronExpressions)
        {
            sb.AppendLine($"    - cron: '{cron}'");
        }

        // Add manual trigger support
        sb.AppendLine("  workflow_dispatch:");
        sb.AppendLine();

        // Group jobs by type
        var reminderJobs = plan.Jobs.Where(j => j.JobType == "send-reminders").ToList();
        var autoPickJobs = plan.Jobs.Where(j => j.JobType == "auto-pick").ToList();
        var syncJobs = plan.Jobs.Where(j => j.JobType == "sync-scores").ToList();

        sb.AppendLine("jobs:");

        // Send Reminders Job
        if (reminderJobs.Any())
        {
            sb.AppendLine("  send-reminders:");
            sb.AppendLine("    name: Send Pick Reminders");
            sb.AppendLine("    runs-on: ubuntu-latest");

            // Build conditional expression
            var reminderCrons = reminderJobs.Select(j => j.ToCronExpression()).Distinct().ToList();
            var condition = string.Join(" || ", reminderCrons.Select(c => $"github.event.schedule == '{c}'"));
            sb.AppendLine($"    if: {condition}");

            sb.AppendLine("    steps:");
            sb.AppendLine("      - name: Call Reminder API");
            sb.AppendLine("        run: |");
            sb.AppendLine($"          curl -X POST {_apiBaseUrl}/api/v1/admin/reminders \\");
            sb.AppendLine("            -H \"X-API-Key: ${{ secrets.EXTERNAL_SYNC_API_KEY }}\" \\");
            sb.AppendLine("            -H \"Content-Type: application/json\"");
            sb.AppendLine();
        }

        // Auto-Pick Job
        if (autoPickJobs.Any())
        {
            sb.AppendLine("  auto-pick:");
            sb.AppendLine("    name: Run Auto-Pick Assignment");
            sb.AppendLine("    runs-on: ubuntu-latest");

            var autoPickCrons = autoPickJobs.Select(j => j.ToCronExpression()).Distinct().ToList();
            var condition = string.Join(" || ", autoPickCrons.Select(c => $"github.event.schedule == '{c}'"));
            sb.AppendLine($"    if: {condition}");

            sb.AppendLine("    steps:");
            sb.AppendLine("      - name: Call Auto-Pick API");
            sb.AppendLine("        run: |");
            sb.AppendLine($"          curl -X POST {_apiBaseUrl}/api/v1/admin/auto-pick \\");
            sb.AppendLine("            -H \"X-API-Key: ${{ secrets.EXTERNAL_SYNC_API_KEY }}\" \\");
            sb.AppendLine("            -H \"Content-Type: application/json\"");
            sb.AppendLine();
        }

        // Sync Live Scores Job
        if (syncJobs.Any())
        {
            sb.AppendLine("  sync-scores:");
            sb.AppendLine("    name: Sync Live Scores");
            sb.AppendLine("    runs-on: ubuntu-latest");

            var syncCrons = syncJobs.Select(j => j.ToCronExpression()).Distinct().ToList();
            var condition = string.Join(" || ", syncCrons.Select(c => $"github.event.schedule == '{c}'"));
            sb.AppendLine($"    if: {condition}");

            sb.AppendLine("    steps:");
            sb.AppendLine("      - name: Call Results Sync API");
            sb.AppendLine("        run: |");
            sb.AppendLine($"          curl -X POST {_apiBaseUrl}/api/v1/admin/sync-results \\");
            sb.AppendLine("            -H \"X-API-Key: ${{ secrets.EXTERNAL_SYNC_API_KEY }}\" \\");
            sb.AppendLine("            -H \"Content-Type: application/json\"");
        }

        return sb.ToString();
    }

    public string GetWeeklyWorkflowFilename(DateTime date)
    {
        // ISO 8601 week number
        var calendar = CultureInfo.InvariantCulture.Calendar;
        var weekNumber = calendar.GetWeekOfYear(
            date,
            CalendarWeekRule.FirstFourDayWeek,
            DayOfWeek.Monday
        );

        return $"weekly-jobs-{date.Year}-W{weekNumber:D2}.yml";
    }

    /// <summary>
    /// Delete old workflow files except the current week's file
    /// </summary>
    private async Task CleanupOldWorkflowsAsync(string currentFilename, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Cleaning up old workflow files (keeping: {CurrentFile})", currentFilename);

            var workflowFiles = await _githubClient.ListWorkflowFilesAsync(cancellationToken);

            // Find old weekly-jobs-*.yml files (not the current one)
            var oldWorkflows = workflowFiles
                .Where(f => f.Name.StartsWith("weekly-jobs-") && f.Name.EndsWith(".yml"))
                .Where(f => f.Name != currentFilename)
                .ToList();

            if (!oldWorkflows.Any())
            {
                _logger.LogDebug("No old workflow files to clean up");
                return;
            }

            _logger.LogInformation("Found {Count} old workflow files to delete", oldWorkflows.Count);

            foreach (var oldWorkflow in oldWorkflows)
            {
                try
                {
                    await _githubClient.DeleteFileAsync(
                        oldWorkflow.Path,
                        $"Clean up old workflow: {oldWorkflow.Name}",
                        oldWorkflow.Sha,
                        cancellationToken
                    );

                    _logger.LogInformation("Deleted old workflow: {Name}", oldWorkflow.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete old workflow: {Name}", oldWorkflow.Name);
                    // Continue with other deletions
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup old workflows (non-fatal)");
            // Don't throw - this is a cleanup operation and shouldn't fail the main flow
        }
    }
}
