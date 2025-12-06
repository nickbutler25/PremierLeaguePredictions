# Live Scores External Sync Setup

This document explains how to set up external triggering for live score updates using GitHub Actions.

## Overview

**Problem**: Render's free tier spins down your app after 15 minutes of inactivity, stopping the internal background service that syncs live scores.

**Solution**: Use GitHub Actions to ping your API every 5 minutes during match times, ensuring scores update even when the app is sleeping.

## Architecture

### Dual-Trigger System:
1. **Internal Background Service** (`ResultsSyncBackgroundService`)
   - Runs automatically when the app is active
   - Syncs every 2 minutes during live matches
   - Perfect for active usage periods

2. **External GitHub Actions** (`.github/workflows/sync-live-scores.yml`)
   - Runs every 5 minutes during typical match times
   - Wakes up the Render app if it's sleeping
   - Ensures continuous updates even when idle

## Setup Instructions

### Step 1: Generate a Secure API Key

Generate a random, secure API key:

```bash
# On Linux/Mac:
openssl rand -hex 32

# On Windows (PowerShell):
-join ((65..90) + (97..122) + (48..57) | Get-Random -Count 32 | ForEach-Object {[char]$_})
```

### Step 2: Add API Key to Render Environment Variables

1. Go to your Render dashboard
2. Select your backend service
3. Go to "Environment" tab
4. Add a new environment variable:
   - **Key**: `ExternalSync__ApiKey`
   - **Value**: The API key you generated above
5. Save and redeploy

**Note**: Use double underscore `__` for nested configuration in Render (this maps to `ExternalSync:ApiKey` in appsettings.json)

### Step 3: Add API Key to GitHub Secrets

1. Go to your GitHub repository
2. Navigate to: **Settings** → **Secrets and variables** → **Actions**
3. Click **New repository secret**
4. Add:
   - **Name**: `EXTERNAL_SYNC_API_KEY`
   - **Secret**: The same API key from Step 1
5. Click **Add secret**

### Step 4: Verify the Workflow File Exists

The workflow file should already be in your repository at:
```
.github/workflows/sync-live-scores.yml
```

This file defines when and how often to sync scores.

### Step 5: Test the Setup

#### Option A: Manual Test via GitHub Actions UI
1. Go to **Actions** tab in GitHub
2. Select **Sync Live Scores** workflow
3. Click **Run workflow**
4. Check the workflow run logs to confirm success

#### Option B: Test via curl (locally)
```bash
curl -X POST https://api.eplpredict.com/api/v1/admin/sync/results \
  -H "X-API-Key: YOUR_API_KEY_HERE" \
  -H "Content-Type: application/json"
```

### Step 6: Monitor the Workflow

- The workflow runs automatically during match times
- Check **Actions** tab in GitHub to see execution history
- Each run shows HTTP status and response from your API

## Schedule Details

The GitHub Action runs:
- **Saturdays**: Every 5 minutes from 12:00 PM - 11:00 PM UTC
- **Sundays**: Every 5 minutes from 12:00 PM - 9:00 PM UTC
- **Midweek (Tue/Wed/Thu)**: Every 5 minutes from 6:00 PM - 11:00 PM UTC

### Why these times?
- Most Premier League matches kick off at 3:00 PM GMT (Saturday)
- Evening matches typically 5:30 PM or 8:00 PM GMT
- Midweek matches usually 7:45 PM or 8:00 PM GMT
- UTC schedule accounts for GMT/BST timezone differences

### Customizing the Schedule

Edit `.github/workflows/sync-live-scores.yml` and modify the `cron` schedules:

```yaml
schedule:
  # Format: '*/5 START-END * * DAY'
  # */5 = every 5 minutes
  # START-END = hour range (UTC, 24-hour format)
  # DAY = 0=Sunday, 6=Saturday
  - cron: '*/5 12-23 * * 6'  # Saturday example
```

**Cron syntax**: `minute hour day month weekday`

## How It Works

### Request Flow:
1. GitHub Actions sends POST request to `/api/v1/admin/sync/results`
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

✅ **Internal service** handles updates when app is active
✅ **GitHub Actions** ensures updates continue when app is sleeping
✅ **Admin button** allows manual triggering anytime
✅ **Free tier friendly** - no additional costs
✅ **Secure** - API key authentication

Your live scores will now update reliably, even on Render's free tier!
