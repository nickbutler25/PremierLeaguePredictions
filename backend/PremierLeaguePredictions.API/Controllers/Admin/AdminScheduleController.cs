using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using PremierLeaguePredictions.API.Authorization;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;

namespace PremierLeaguePredictions.API.Controllers.Admin;

/// <summary>
/// Controller for managing dynamic cron job schedules
/// Called by the master scheduler job on cron-jobs.org every Monday at 9 AM UTC
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/schedule")]
[Authorize(Policy = AdminPolicies.ExternalSync)]
public class AdminScheduleController : ControllerBase
{
    private readonly ICronSchedulerService _cronSchedulerService;
    private readonly ICronJobsOrgService _cronJobsOrgService;
    private readonly IPickReminderService _reminderService;
    private readonly IAutoPickService _autoPickService;
    private readonly ILogger<AdminScheduleController> _logger;

    public AdminScheduleController(
        ICronSchedulerService cronSchedulerService,
        ICronJobsOrgService cronJobsOrgService,
        IPickReminderService reminderService,
        IAutoPickService autoPickService,
        ILogger<AdminScheduleController> logger)
    {
        _cronSchedulerService = cronSchedulerService;
        _cronJobsOrgService = cronJobsOrgService;
        _reminderService = reminderService;
        _autoPickService = autoPickService;
        _logger = logger;
    }

    /// <summary>
    /// Generate weekly schedule and sync jobs to cron-jobs.org
    /// Called by the master scheduler job on cron-jobs.org every Monday at 9 AM UTC
    /// </summary>
    [HttpPost("generate")]
    public async Task<ActionResult<ApiResponse<ScheduleGenerationResponse>>> GenerateWeeklySchedule(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting weekly schedule generation");

            var plan = await _cronSchedulerService.GenerateWeeklyScheduleAsync(cancellationToken);

            _logger.LogInformation("Generated schedule plan with {JobCount} jobs for week {WeekNumber}",
                plan.Jobs.Count, plan.WeekNumber);

            var response = await _cronJobsOrgService.SyncWeeklyJobsAsync(plan, cancellationToken);

            if (response.Success)
            {
                _logger.LogInformation("Successfully synced {JobCount} jobs to cron-jobs.org", response.JobCount);

                return Ok(ApiResponse<ScheduleGenerationResponse>.SuccessResult(
                    response,
                    $"Weekly schedule generated successfully with {response.JobCount} jobs"));
            }
            else
            {
                _logger.LogError("Failed to sync jobs to cron-jobs.org: {Message}", response.Message);
                return StatusCode(500, ApiResponse<ScheduleGenerationResponse>.FailureResult(
                    response.Message ?? "Unknown error occurred"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating weekly schedule");
            return StatusCode(500, ApiResponse<ScheduleGenerationResponse>.FailureResult(
                $"Failed to generate schedule: {ex.Message}"));
        }
    }

    /// <summary>
    /// Send pick reminder emails
    /// Called by GitHub Actions at scheduled times (24h, 12h, 3h before deadlines)
    /// </summary>
    [HttpPost("reminders")]
    public async Task<ActionResult<ApiResponse<object>>> SendReminders(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Sending pick reminders");

            var result = await _reminderService.SendPickRemindersAsync(cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("Pick reminders sent successfully: {Sent} sent, {Failed} failed",
                    result.EmailsSent, result.EmailsFailed);

                return Ok(ApiResponse<object>.SuccessResult(
                    new { emailsSent = result.EmailsSent, emailsFailed = result.EmailsFailed, timestamp = DateTime.UtcNow },
                    result.Message));
            }
            else
            {
                _logger.LogError("Failed to send some pick reminders: {Sent} sent, {Failed} failed",
                    result.EmailsSent, result.EmailsFailed);

                return StatusCode(500, ApiResponse<object>.FailureResult(result.Message));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending pick reminders");
            return StatusCode(500, ApiResponse<object>.FailureResult(
                $"Failed to send reminders: {ex.Message}"));
        }
    }

    /// <summary>
    /// Run auto-pick assignment for missed picks
    /// Called by GitHub Actions at gameweek deadlines
    /// </summary>
    [HttpPost("auto-pick")]
    public async Task<ActionResult<ApiResponse<object>>> RunAutoPick(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Running auto-pick assignment");

            var result = await _autoPickService.AssignAllMissedPicksAsync(cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("Auto-pick assignment completed: {Assigned} picks assigned, {Failed} failed, {Gameweeks} gameweeks processed",
                    result.PicksAssigned, result.PicksFailed, result.GameweeksProcessed);

                return Ok(ApiResponse<object>.SuccessResult(
                    new {
                        picksAssigned = result.PicksAssigned,
                        picksFailed = result.PicksFailed,
                        gameweeksProcessed = result.GameweeksProcessed,
                        timestamp = DateTime.UtcNow
                    },
                    result.Message));
            }
            else
            {
                _logger.LogError("Failed to assign some auto-picks: {Assigned} assigned, {Failed} failed, {Gameweeks} gameweeks processed",
                    result.PicksAssigned, result.PicksFailed, result.GameweeksProcessed);

                return StatusCode(500, ApiResponse<object>.FailureResult(result.Message));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running auto-pick");
            return StatusCode(500, ApiResponse<object>.FailureResult(
                $"Failed to run auto-pick: {ex.Message}"));
        }
    }
}
