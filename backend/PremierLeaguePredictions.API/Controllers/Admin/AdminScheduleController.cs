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
    private readonly IScheduleGenerationRunner _generationRunner;
    private readonly IPickReminderService _reminderService;
    private readonly IAutoPickService _autoPickService;
    private readonly IGameweekCompletionService _completionService;
    private readonly ILogger<AdminScheduleController> _logger;

    public AdminScheduleController(
        IScheduleGenerationRunner generationRunner,
        IPickReminderService reminderService,
        IAutoPickService autoPickService,
        IGameweekCompletionService completionService,
        ILogger<AdminScheduleController> logger)
    {
        _generationRunner = generationRunner;
        _reminderService = reminderService;
        _autoPickService = autoPickService;
        _completionService = completionService;
        _logger = logger;
    }

    /// <summary>
    /// Finalise any gameweek whose football is over: lock it and process eliminations.
    /// Called by a scheduled job shortly after each gameweek's last kickoff.
    /// </summary>
    /// <remarks>
    /// Completes every eligible gameweek rather than a named one, so a run also picks up an
    /// earlier gameweek that a postponement left open. Safe to call repeatedly — a gameweek
    /// that is already locked, or that still has an unplayed fixture, is left alone.
    /// </remarks>
    [HttpPost("complete-gameweek")]
    public async Task<ActionResult<ApiResponse<GameweekCompletionResponse>>> CompleteGameweek(
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Running gameweek completion");

            var result = await _completionService.CompleteFinishedGameweeksAsync(cancellationToken);

            return Ok(ApiResponse<GameweekCompletionResponse>.SuccessResult(result, result.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing gameweeks");
            return StatusCode(500, ApiResponse<GameweekCompletionResponse>.FailureResult(
                $"Failed to complete gameweeks: {ex.Message}"));
        }
    }

    /// <summary>
    /// Start weekly schedule generation and syncing of jobs to cron-jobs.org.
    /// Returns 202 immediately — the run happens in the background because syncing is paced to
    /// stay under the cron-job.org API rate limit, which takes seconds per job. Poll
    /// GET generate/status for progress and the final outcome.
    /// </summary>
    [HttpPost("generate")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public ActionResult<ApiResponse<ScheduleGenerationStatus>> GenerateWeeklySchedule()
    {
        if (!_generationRunner.TryStart(out var status))
        {
            _logger.LogWarning("Schedule generation already in progress (run {RunId}) — ignoring new request",
                status.RunId);

            // Concurrent runs would delete the jobs each other is creating.
            return Conflict(ApiResponse<ScheduleGenerationStatus>.FailureResult(
                $"Schedule generation is already in progress (started {status.StartedAt:O})"));
        }

        return Accepted(ApiResponse<ScheduleGenerationStatus>.SuccessResult(
            status, "Schedule generation started — poll generate/status for progress"));
    }

    /// <summary>
    /// Progress and outcome of the current or most recent schedule generation run.
    /// </summary>
    [HttpGet("generate/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<ScheduleGenerationStatus>> GetGenerateStatus()
    {
        var status = _generationRunner.GetStatus();

        if (status is null)
        {
            return NotFound(ApiResponse<ScheduleGenerationStatus>.FailureResult(
                "No schedule generation has been started since the API last restarted"));
        }

        return Ok(ApiResponse<ScheduleGenerationStatus>.SuccessResult(status));
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
                    new
                    {
                        emailsSent = result.EmailsSent,
                        emailsFailed = result.EmailsFailed,
                        emailsSkipped = result.EmailsSkipped,
                        timestamp = DateTime.UtcNow
                    },
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
