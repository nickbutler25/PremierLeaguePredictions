# Live Scores Setup - GitHub Actions Scheduler

This document explains how live score updates are handled using the dynamic GitHub Actions scheduler.

## Overview

**Problem**: Render's free tier spins down your app after 15 minutes of inactivity, making continuous score updates challenging.

**Solution**: Use a **dynamic GitHub Actions scheduler** that generates weekly job schedules based on actual fixture times, ensuring scores update during matches without wasting resources.

## Architecture

### Dynamic Scheduling System:
1. **Master Scheduler** (`.github/workflows/master-scheduler.yml`)
   - Runs every Monday at 9 AM UTC
   - Queries gameweeks for the next 7 days
   - Generates workflow files with precise match-time schedules
   - Groups fixtures by 15-minute kickoff windows

2. **Generated Weekly Jobs** (`.github/workflows/weekly-jobs-YYYY-WW.yml`)
   - Created automatically by the master scheduler
   - Syncs scores every 2 minutes during actual match windows
   - Wakes up the Render app if it's sleeping
   - Automatically cleaned up after the week ends

## Setup Instructions

For complete setup instructions, see [DEPLOYMENT.md](DEPLOYMENT.md#github-actions-scheduler-setup).

### Quick Setup Summary

1. **Create GitHub Personal Access Token**
   - Go to [GitHub Settings > Tokens (classic)](https://github.com/settings/tokens)
   - Click "Generate new token (classic)"
   - Grant `workflow` scope (includes necessary repository access)
   - Copy the token immediately

2. **Add Token to Render Environment**
   ```
   GitHub__PersonalAccessToken=ghp_your_token_here
   ```

3. **Add API Key to GitHub Secrets**
   ```
   Name:  EXTERNAL_SYNC_API_KEY
   Value: [Your API key]
   ```

4. **Verify Master Scheduler**
   - Check `.github/workflows/master-scheduler.yml` exists
   - It runs every Monday at 9 AM UTC
   - Manually trigger: Actions > Master Scheduler > Run workflow

## Schedule Details

### How Schedules Are Generated

The system **automatically generates precise schedules** based on actual fixture times:

1. **Monday 9 AM UTC**: Master scheduler analyzes upcoming gameweeks
2. **Fixture Grouping**: Groups matches by 15-minute kickoff windows
3. **YAML Generation**: Creates workflow with exact cron expressions
4. **Auto-Cleanup**: Deletes previous week's workflow file

### Example Generated Schedule

For a weekend with fixtures at:
- Saturday 15:00 GMT (5 matches)
- Saturday 17:30 GMT (1 match)
- Saturday 20:00 GMT (1 match)
- Sunday 14:00 GMT (3 matches)

The scheduler creates:
```yaml
schedule:
  - cron: '*/2 15-17 7 12 *'   # Saturday 3-5 PM (5 matches)
  - cron: '*/2 17-19 7 12 *'   # Saturday 5:30-7 PM (1 match)
  - cron: '*/2 20-22 7 12 *'   # Saturday 8-10 PM (1 match)
  - cron: '*/2 14-16 8 12 *'   # Sunday 2-4 PM (3 matches)
```

**Benefits:**
- **No wasted resources** - Only syncs during actual matches
- **Precise timing** - Matches exact kickoff times
- **Adaptive** - Adjusts for postponements, rescheduling
- **Efficient** - Grouping reduces job count

## How It Works

### Schedule Generation Flow:
1. **Monday 9 AM UTC**: Master scheduler calls `/api/v1/admin/schedule/generate`
2. `CronSchedulerService.GenerateWeeklyScheduleAsync()` executes:
   - Queries database for gameweeks in next 7 days
   - Groups fixtures by 15-minute kickoff windows
   - Creates `SchedulePlan` with job times
3. `GitHubWorkflowService.GenerateAndCommitWorkflowAsync()` executes:
   - Converts schedule plan to GitHub Actions YAML
   - Commits `weekly-jobs-YYYY-WW.yml` via GitHub API
   - Deletes previous week's workflow file
4. GitHub Actions automatically runs jobs at scheduled times

### Score Sync Flow:
1. GitHub Actions sends POST request to `/api/v1/dev/fixtures/sync-results`
2. Request includes `X-API-Key` header
3. `ApiKeyAuthenticationHandler` validates the API key
4. If valid, grants Admin role and allows access
5. `ResultsService.SyncRecentResultsAsync()` is called
6. Football Data API is queried for latest fixture data
7. Database is updated with new scores/statuses
8. User points are recalculated if needed

### Security:
- API key authentication prevents unauthorized access
- Key is stored securely in GitHub Secrets (encrypted)
- Key is stored in Render environment variables (encrypted)
- Only the sync endpoints accept API key authentication
- Regular user endpoints still require JWT authentication

## Manual "Refresh Scores" Button

Admins also have a **"⟳ Refresh Scores"** button in the Fixtures card on the dashboard:
- Click to immediately trigger a sync
- Uses your admin JWT token (no API key needed)
- Useful for testing and immediate updates during matches

## Troubleshooting

### Workflow fails with 401 Unauthorized
- Check that `EXTERNAL_SYNC_API_KEY` GitHub secret matches `ExternalSync__ApiKey` in Render
- Verify the API key is set correctly in both places

### Workflow fails with 500 Internal Server Error
- Check Render logs for errors
- Verify database connection is working
- Check that `FootballData:ApiKey` environment variable is set

### Scores still not updating
- Check if the internal background service is running (look for "Smart Results Sync Background Service is starting" in logs)
- Verify Football Data API rate limits haven't been exceeded (10 calls/minute on free tier)
- Check that fixtures have `ExternalId` values set in the database

### App is sleeping and not waking up
- Check Render logs to see if the request is reaching the server
- Verify the URL in the workflow file matches your actual API domain
- Try manually triggering the workflow from GitHub Actions UI

## Cost Analysis

### GitHub Actions (Free tier):
- 2,000 minutes/month free
- Each workflow run takes ~10 seconds
- Running every 5 minutes for 11 hours/day = 132 runs/day
- 132 runs × 10 seconds = 22 minutes/day
- 22 minutes/day × 30 days = **660 minutes/month**
- ✅ **Well within free tier limits**

### Render (Free tier):
- Apps can spin down after 15 minutes of inactivity
- Each GitHub Action request wakes up the app
- No additional cost - still within free tier

## Alternative Solutions (Future)

If you outgrow the free tier or need more control:

1. **Upgrade to Render's paid tier** - keeps app always running
2. **Use a dedicated cron service** - cron-job.org, EasyCron, etc.
3. **Self-host a cron job** - Run on your own server/VPS
4. **Use Render Cron Jobs** - Paid feature for scheduled tasks

## Files Modified

- **Backend**:
  - `backend/PremierLeaguePredictions.API/Authorization/ApiKeyAuthenticationHandler.cs` (new)
  - `backend/PremierLeaguePredictions.API/Program.cs` (modified - added API key authentication scheme)

- **Frontend**:
  - `frontend/src/components/dashboard/Fixtures.tsx` (modified - added "Refresh Scores" button)

- **GitHub Actions**:
  - `.github/workflows/sync-live-scores.yml` (new)

## Summary

✅ **Dynamic scheduling** - Jobs generated based on actual fixture times
✅ **Resource efficient** - Only runs during matches, reminders, deadlines
✅ **Self-managing** - Auto-generates and cleans up workflows
✅ **Free tier friendly** - Minimal GitHub Actions usage (~45-240 min/month)
✅ **Reliable** - Works even when Render app is sleeping
✅ **Secure** - API key authentication for all endpoints

Your live scores, reminders, and auto-picks will now run reliably with zero ongoing maintenance!

For detailed troubleshooting and setup, see [DEPLOYMENT.md](DEPLOYMENT.md#github-actions-scheduler-setup).
