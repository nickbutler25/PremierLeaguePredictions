# Cron Job Scheduler Implementation Progress

## Goal
Replace all background services with a dynamic GitHub Actions-based cron job scheduler that runs every Monday at 9 AM UTC.

## Status: IN PROGRESS (Phase 1 Complete - Core Services)

## ✅ Completed

### Phase 1: Core Services & DTOs
1. **SchedulePlan.cs** - DTOs for scheduling jobs
   - `SchedulePlan` - Container for all scheduled jobs
   - `ScheduledJob` - Individual job with cron expression generation
   - `ScheduleGenerationResponse` - Response DTO

2. **ICronSchedulerService.cs** - Interface for schedule generation

3. **CronSchedulerService.cs** - Core scheduler logic
   - Queries gameweeks for next 7 days
   - Schedules reminders (24h, 12h, 3h before deadline)
   - Schedules auto-pick at deadline
   - Groups fixtures by kickoff time (15-min windows)
   - Creates recurring jobs for live score sync

4. **IGitHubWorkflowService.cs** - Interface for workflow generation

5. **GitHubApiClient.cs** - GitHub REST API client
   - Create/update/delete files in repository
   - List workflow files
   - Authentication with Personal Access Token

## 🚧 Remaining Work

### Phase 2: Workflow Generation (NEXT)
- [ ] **GitHubWorkflowService.cs** - Generate GitHub Actions YAML from SchedulePlan
  - Convert ScheduledJobs to cron expressions
  - Generate workflow with multiple jobs
  - Handle job conditionals (if: github.event.schedule ==)
  - Clean up old workflow files

### Phase 3: API Endpoints
- [ ] **AdminScheduleController.cs** - REST API for schedule management
  - POST /api/v1/admin/schedule/generate - Generate weekly schedule
  - POST /api/v1/admin/reminders - Send reminders (called by GitHub Actions)
  - POST /api/v1/admin/auto-pick - Run auto-pick (called by GitHub Actions)

### Phase 4: GitHub Actions Workflow
- [ ] **.github/workflows/master-scheduler.yml** - Master cron job
  - Runs every Monday at 9:00 AM UTC
  - Calls /api/v1/admin/schedule/generate endpoint
  - Manual trigger support (workflow_dispatch)

### Phase 5: Configuration & Integration
- [ ] **Program.cs** modifications:
  - Add `ICronSchedulerService` → `CronSchedulerService`
  - Add `IGitHubWorkflowService` → `GitHubWorkflowService`
  - Add `GitHubApiClient` as HttpClient
  - **REMOVE** background service registrations:
    - `ResultsSyncBackgroundService`
    - `AutoPickAssignmentBackgroundService`
    - `PickReminderBackgroundService`

- [ ] **appsettings.json** updates:
  ```json
  {
    "GitHub": {
      "Owner": "your-github-username",
      "Repository": "PremierLeaguePredictions",
      "PersonalAccessToken": "set-via-environment-variable"
    }
  }
  ```

### Phase 6: Cleanup
- [ ] Delete old background service files:
  - `ResultsSyncBackgroundService.cs`
  - `AutoPickAssignmentBackgroundService.cs`
  - `PickReminderBackgroundService.cs`

### Phase 7: Testing
- [ ] Unit test cron expression generation
- [ ] Integration test schedule generation
- [ ] Manual test GitHub workflow creation
- [ ] Verify workflow runs at scheduled times

## Setup Requirements (After Implementation)

### 1. Create GitHub Personal Access Token
1. Go to: https://github.com/settings/tokens/new
2. Grant permissions: `repo` and `workflow`
3. Generate token and copy it

### 2. Add to Render Environment Variables
- Key: `GitHub__PersonalAccessToken`
- Value: `<your-token-from-step-1>`

### 3. Add to GitHub Secrets
- Name: `EXTERNAL_SYNC_API_KEY` (already exists from previous work)
- This will be used by generated workflows to call your API

### 4. Update appsettings.json
- Set `GitHub:Owner` to your GitHub username
- Set `GitHub:Repository` to `PremierLeaguePredictions`

## How It Works (Once Complete)

### Weekly Flow:
1. **Monday 9 AM UTC** - Master scheduler workflow runs
2. Calls `/api/v1/admin/schedule/generate`
3. Backend generates SchedulePlan for next 7 days
4. Converts to GitHub Actions YAML
5. Commits workflow file to repository via GitHub API
6. GitHub Actions automatically runs jobs at scheduled times

### Generated Jobs:
- **Reminders**: 24h, 12h, 3h before each gameweek deadline
- **Auto-Pick**: At each gameweek deadline
- **Live Scores**: Every 2 minutes during match windows

### Example Generated Workflow:
```yaml
name: Weekly Jobs - 2025 W49
on:
  schedule:
    - cron: '0 15 5 12 *'  # Reminder 24h before GW14
    - cron: '0 3 6 12 *'   # Reminder 12h before GW14
    - cron: '*/2 15-17 6 12 *'  # Live scores during 3 PM matches

jobs:
  send-reminders:
    if: github.event.schedule == '0 15 5 12 *' || github.event.schedule == '0 3 6 12 *'
    runs-on: ubuntu-latest
    steps:
      - run: curl -X POST https://api.eplpredict.com/api/v1/admin/reminders \
          -H "X-API-Key: ${{ secrets.EXTERNAL_SYNC_API_KEY }}"

  # ... more jobs
```

## Benefits Over Background Services

✅ **No wasted resources** - Jobs only run when needed
✅ **Survives app downtime** - GitHub Actions independent of Render
✅ **Precise scheduling** - Exact cron timing
✅ **Visibility** - All jobs visible in GitHub Actions tab
✅ **Free tier friendly** - ~45 min/month << 2,000 min limit
✅ **Scalable** - Easy to add new job types

## Next Steps

1. Implement `GitHubWorkflowService.cs` (YAML generation logic)
2. Implement `AdminScheduleController.cs` (API endpoints)
3. Create `.github/workflows/master-scheduler.yml`
4. Update `Program.cs` and `appsettings.json`
5. Remove background services
6. Test end-to-end
7. Deploy and monitor first week

## Files Created So Far

- `backend/PremierLeaguePredictions.Application/DTOs/SchedulePlan.cs`
- `backend/PremierLeaguePredictions.Application/Interfaces/ICronSchedulerService.cs`
- `backend/PremierLeaguePredictions.Application/Services/CronSchedulerService.cs`
- `backend/PremierLeaguePredictions.Infrastructure/Services/IGitHubWorkflowService.cs`
- `backend/PremierLeaguePredictions.Infrastructure/Services/GitHubApiClient.cs`

## Estimated Completion Time

- Remaining implementation: ~2-3 hours
- Testing: ~1 hour
- Documentation: ~30 minutes

**Total**: ~4 hours of focused work

## Notes

- This implementation is partially complete
- Core scheduling logic is done
- Main remaining work is YAML generation and integration
- Can be completed in next session or incrementally
