# Deployment Guide - Premier League Predictions

This guide covers deploying the Premier League Predictions application to production.

## Table of Contents
- [cron-job.org Scheduler Setup](#cron-joborg-scheduler-setup)
- [Database Migrations](#database-migrations)
- [Render.com Deployment](#rendercom-deployment)
- [Manual Deployment](#manual-deployment)
- [Environment Variables](#environment-variables)
- [Health Checks](#health-checks)
- [Troubleshooting the cron-job.org Scheduler](#troubleshooting-the-cron-joborg-scheduler)

---

## cron-job.org Scheduler Setup

The application uses **cron-job.org** (an external cron service) to schedule automated tasks (reminders, auto-picks, score syncing). A single weekly **master job** runs every **Monday at 09:00 Europe/London** and calls our API, which then generates a precise per-week schedule as individual cron-job.org jobs.

> GitHub Actions is used **only** for CI/CD (test/build/deploy) — it no longer schedules app jobs. The old `master-scheduler.yml` / `weekly-jobs-*.yml` workflows and `GitHub__*` env vars are retired (see [Environment Variables](#environment-variables)).

The canonical reference for this flow is [LIVE_SCORES_SETUP.md](LIVE_SCORES_SETUP.md).

### How It Works

1. **Monday 09:00 Europe/London** — the master job `POST`s to `/api/v1/admin/schedule/generate` (authenticated with the `X-API-Key` header = ExternalSync key).
2. `CronSchedulerService.GenerateWeeklyScheduleAsync` builds a `SchedulePlan` from unlocked gameweeks with deadlines/fixtures in the next 7 days:
   - Reminder emails at **24h, 12h, 3h** before each deadline.
   - **Auto-pick** at each deadline.
   - **Live-score sync** every 2 minutes during each match window (kickoff → +2h, fixtures grouped into 15-minute kickoff windows).
3. `CronJobsOrgService.SyncWeeklyJobsAsync` calls the cron-job.org REST API to create one cron-job.org job per (jobType, schedule).
   - **Before** creating, it **deletes all cron-job.org jobs whose title starts with `EPL-`**. Generated jobs are titled `EPL-{weekNumber}-{jobType}-{n}`.
   - The master job and warm-up job must **NOT** be `EPL-` prefixed, or they would delete themselves.
4. If there are no gameweeks/fixtures in the next 7 days (pre-season/off-season), `generate` correctly creates **zero** `EPL-` jobs. This is expected, not a failure.

**Generated jobs hit these endpoints** (auth passed as a query parameter `?apiKey=<ExternalSync key>`, because cron-job.org REST-API jobs don't reliably support custom headers):

| Job type | Method + endpoint |
|---|---|
| `send-reminders` | `POST /api/v1/admin/schedule/reminders` |
| `auto-pick` | `POST /api/v1/admin/schedule/auto-pick` |
| `sync-scores` | `POST /api/v1/admin/sync/results` |

The API's `ApiKeyAuthenticationHandler` accepts the ExternalSync key from **either** the `X-API-Key` header **or** the `apiKey` query-string parameter, validates it against config `ExternalSync:ApiKey`, and grants the Admin role for the request.

### One-Time Setup (per environment)

You configure two jobs per environment (production and development) in the cron-job.org dashboard.

#### 1. Set environment variables (per Render service)

- `CronJobsOrg__ApiKey` — the cron-job.org **account** API key (Bearer token so our API can list/create/delete jobs via `api.cron-job.org`). Set **manually in each Render service's Environment tab** (in the Render dashboard). Use **separate keys for prod and dev** — a shared key makes the two environments wipe each other's `EPL-` jobs, because the delete step is account-wide.
- `ExternalSync__ApiKey` — the key the cron-job.org jobs use to authenticate **to** our API.
- `ApiBaseUrl` — base URL the generated jobs' target URLs are built from. Set manually in each Render service's Environment tab (prod → `https://api.eplpredict.com`, dev → `https://premierleague-api-dev.onrender.com`).

See [Environment Variables](#environment-variables) for values.

#### 2. Create the master job

In the cron-job.org dashboard:

**Production**
- Title: `Prem Predictions Schedule Generate` (must **not** start with `EPL-`)
- URL: `https://api.eplpredict.com/api/v1/admin/schedule/generate`
- Method: `POST`, header `X-API-Key: <ExternalSync key>`
- Schedule: Mondays 09:00 Europe/London

**Development**
- Title: `Prem Predictions Schedule Generate Dev`
- URL: `https://premierleague-api-dev.onrender.com/api/v1/admin/schedule/generate`
- Method: `POST`, header `X-API-Key: <ExternalSync key>`
- Schedule: Mondays 09:00 Europe/London

#### 3. Create the warm-up (health-ping) job

Render's free tier cold-starts (~24s) can cause the Monday `generate` to hit the 30s cron-job.org timeout. Add a warm-up job per environment that `GET`s `/health` (no auth, no body — warms the app process and DB connection) a few minutes before the master job, e.g. at **08:55 and 08:58 Europe/London**:

- Prod → `https://api.eplpredict.com/health`
- Dev → `https://premierleague-api-dev.onrender.com/health`

Even if the first ping itself times out on a cold instance, it still triggers the Render spin-up so `generate` lands warm. The warm-up job's title must **not** start with `EPL-`. **Do not** try to raise the cron-job.org timeout — it is capped at 30s.

### Manual Trigger

To regenerate the schedule on demand, open the master job in the cron-job.org dashboard and use its "run now" / test-run option (or wait for the next Monday run).

---

## Database Migrations

**IMPORTANT:** Running migrations on application startup is generally avoided in multi-instance deployments to prevent race conditions. On the current single-instance Render **free tier**, however, migrations intentionally run on startup via `RunMigrationsOnStartup=true` (see below), since the free tier has no `preDeployCommand`.

### Automatic Migration (Render.com free tier)

On Render's free tier there is no `render.yaml`/Blueprint and no `preDeployCommand` (that is a paid-tier feature). Instead, the app applies EF Core migrations at boot when the `RunMigrationsOnStartup=true` environment variable is set (configured manually in each service's Environment tab):

```
RunMigrationsOnStartup=true
```

This means migrations run on every application startup, which is acceptable for the single-instance free tier.

> **Paid tier (future):** a paid Render service could instead run migrations pre-deploy via `preDeployCommand: "cd /app && dotnet ef database update --no-build"` and set `RunMigrationsOnStartup=false`. This is not used today.

### Manual Migration Scripts

For other deployment environments, use the provided migration scripts:

#### Linux/macOS:
```bash
# Set connection string
export ConnectionStrings__DefaultConnection="Host=your-host;Database=your-db;Username=user;Password=pass"

# Run migrations
./scripts/run-migrations.sh
```

#### Windows (PowerShell):
```powershell
# Set connection string
$env:ConnectionStrings__DefaultConnection = "Host=your-host;Database=your-db;Username=user;Password=pass"

# Run migrations
.\scripts\run-migrations.ps1
```

### Using EF Core CLI Directly

If you have the .NET SDK installed:

```bash
cd backend/PremierLeaguePredictions.API

# Update database to latest migration
dotnet ef database update --connection "your-connection-string"

# List pending migrations
dotnet ef migrations list

# Generate SQL script for review
dotnet ef migrations script > migration.sql
```

### Development Environment

In development, migrations run automatically on startup. This is controlled by the `RunMigrationsOnStartup` setting:

```json
// appsettings.Development.json
{
  "RunMigrationsOnStartup": true  // Only true in development
}
```

**Production:** This setting defaults to `false` but has exceptions (see below).

---

## Render.com Deployment

### Important: Free Tier Limitations

**Render.com Free Tier** does not support Blueprints (`render.yaml`) or `preDeployCommand`, which is the usual way to run migrations before deployment. Both services (`premierleague-api` = prod, `premierleague-api-dev` = dev) are therefore created and configured **manually in the Render dashboard**, and migrations are configured to run on application startup.

Set this manually in each service's Environment tab:
```
RunMigrationsOnStartup=true  # Required for Render free tier
```

⚠️ **Tradeoff:** This means migrations run on every application restart, which is acceptable for single-instance free tier deployments but should be avoided in multi-instance production deployments (paid tiers).

**For Paid Tiers (future):** a paid service could use `preDeployCommand: "cd /app && dotnet ef database update --no-build"` and set `RunMigrationsOnStartup=false`. Not used today.

### Prerequisites
1. Render.com account (free or paid tier)
2. GitHub repository connected to Render
3. Supabase PostgreSQL database (or other hosted PostgreSQL)

### Deployment Steps

1. **Connect Repository**
   - Go to Render Dashboard
   - Select "New Web Service"
   - Connect your GitHub repository

2. **Configure Service** (manually — there is no Blueprint/`render.yaml`)
   - Service name: `premierleague-api` (prod) or `premierleague-api-dev` (dev)
   - Region: Oregon (or your preferred region)
   - Root Directory: `backend`
   - Runtime: Docker (Dockerfile Path `./Dockerfile`)
   - Health Check Path: set under Settings → Health Check Path = `/health`

3. **Set Environment Variables**

   Required variables (set in Render Dashboard):
   ```
   ConnectionStrings__DefaultConnection  # PostgreSQL connection string
   Google__ClientId                      # Google OAuth client ID
   FootballData__ApiKey                  # Football Data API key
   AllowedOrigins__0                     # Frontend URL (e.g., https://your-app.com)
   ```

   Auto-generated by Render:
   ```
   JWT__Secret                          # Auto-generated secure secret
   ```

4. **Deploy**
   - Click "Create Web Service"
   - Render will:
     1. Build the Docker image
     2. Start the application
     3. Run migrations on first startup (via `RunMigrationsOnStartup`)
     4. Run health checks

5. **Verify Deployment**
   - Check health endpoint: `https://your-app.onrender.com/health`
   - Review logs in Render Dashboard for migration success
   - Test authentication and API endpoints

### Service Configuration (Render dashboard)

There is **no `render.yaml`/Blueprint**. Each service is configured by hand in the Render dashboard with the following settings:

| Setting | Value |
|---|---|
| Runtime | Docker |
| Root Directory | `backend` |
| Dockerfile Path | `./Dockerfile` |
| Health Check Path | `/health` (Settings → Health Check Path) |

**Key environment variables** (set manually in the service's Environment tab — see [Environment Variables](#environment-variables) for the full list):

```
ASPNETCORE_ENVIRONMENT=Production
RunMigrationsOnStartup=true   # Migrations run on app startup (free tier)
```

> **Paid tier (future):** a paid service could set `RunMigrationsOnStartup=false` and add a `preDeployCommand` to run migrations before deploy. Not used today.

---

## Manual Deployment

For deploying to custom infrastructure (Azure, AWS, on-premises):

### 1. Build the Application

```bash
cd backend/PremierLeaguePredictions.API
dotnet publish -c Release -o ./publish
```

### 2. Run Migrations

```bash
# Set connection string
export ConnectionStrings__DefaultConnection="your-connection-string"

# Run migration script
./scripts/run-migrations.sh
```

### 3. Deploy Application

```bash
# Copy published files to server
scp -r ./publish/* user@server:/var/www/premierleague-api/

# Set up systemd service (Linux)
sudo systemctl start premierleague-api
sudo systemctl enable premierleague-api
```

### 4. Configure Reverse Proxy

**Nginx Example:**
```nginx
server {
    listen 443 ssl;
    server_name api.yourapp.com;

    ssl_certificate /path/to/cert.pem;
    ssl_certificate_key /path/to/key.pem;

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

---

## Environment Variables

### Required Production Variables

```bash
# Database
ConnectionStrings__DefaultConnection="Host=host;Database=db;Username=user;Password=pass"

# JWT Authentication
JWT__Secret="your-secure-secret-minimum-32-characters-long"
JWT__Issuer="PremierLeaguePredictionsAPI"
JWT__Audience="PremierLeaguePredictionsClient"
JWT__ExpirationInMinutes="1440"

# Google OAuth
Google__ClientId="your-google-client-id.apps.googleusercontent.com"

# Football Data API
FootballData__ApiKey="your-football-data-api-key"

# CORS
AllowedOrigins__0="https://your-frontend-url.com"

# cron-job.org Scheduler
CronJobsOrg__ApiKey="your-cron-job-org-account-api-key"  # Set manually in each Render service's Environment tab. Use separate keys for prod and dev.
ExternalSync__ApiKey="your-external-sync-api-key"        # Key the cron-job.org jobs use to authenticate to our API
ApiBaseUrl="https://api.eplpredict.com"                  # REQUIRED per service. No default — the app throws if unset. dev: https://premierleague-api-dev.onrender.com
GitHub__Environment="prod"                               # REQUIRED per service ("prod" or "dev"). Selects the EPL-{ENV}- job prefix and the target the dispatch workflow calls.

# DEPRECATED / removable — only used by the retired GitHub Actions scheduler
# GitHub__Owner="your-github-username"
# GitHub__Repository="PremierLeaguePredictions"
# GitHub__PersonalAccessToken="ghp_your_token_here"

# Email — sent through Brevo's REST API over HTTPS.
# Render's free tier blocks outbound SMTP (587/465/25 are silently dropped), so SMTP is not an
# option here. Email__Provider=Smtp selects SmtpEmailService for local development only.
Email__Brevo__ApiKey="xkeysib-your-brevo-api-key"
Email__FromEmail="noreply@eplpredict.com"                # REQUIRED. No default. Must be a sender Brevo has verified,
                                                         # or a domain authenticated in Brevo — otherwise Brevo accepts
                                                         # the message with a 2xx and rejects it later at send time.
Email__FromName="Premier League Predictions"             # dev: "Premier League Predictions Dev"
# Email__MaxConcurrentSends="5"                          # Optional. Parallel sends for bulk reminder runs.
# Email__NoSendDomains="example.com,.test,.invalid"       # Optional. Addresses on these are skipped, not mailed.
```

### Development-Only Variables

```bash
# Development settings (DO NOT USE IN PRODUCTION)
DisableAuthorizationInDevelopment="false"  # Must be false in production
RunMigrationsOnStartup="false"            # Must be false in production
```

---

## Health Checks

The application exposes health check endpoints for monitoring:

### Primary Health Check
```
GET /health
```

Returns overall application health including:
- Application status
- Database connectivity
- SignalR hub status

**Response Example:**
```json
{
  "status": "Healthy",
  "checks": [
    {
      "name": "database",
      "status": "Healthy",
      "description": "PostgreSQL connection",
      "duration": 15.2
    },
    {
      "name": "signalr",
      "status": "Healthy",
      "description": "SignalR hub",
      "duration": 2.1
    }
  ],
  "totalDuration": 17.3
}
```

### Monitoring Setup

Configure your monitoring tool to check `/health` endpoint:
- **Interval:** Every 30-60 seconds
- **Timeout:** 5 seconds
- **Success:** HTTP 200 status
- **Alert:** If unhealthy for 2+ consecutive checks

**Render.com:** Configured manually per service under Settings → Health Check Path = `/health`

---

## Rollback Procedure

If deployment fails or issues are detected:

### 1. Rollback Application

**Render.com:**
- Go to service dashboard
- Click "Rollbacks" tab
- Select previous working deployment
- Click "Rollback"

**Manual:**
```bash
# Deploy previous version
git checkout <previous-tag>
./scripts/deploy.sh
```

### 2. Rollback Database (if needed)

**⚠️ WARNING:** Database rollbacks are risky. Test thoroughly first.

```bash
# Revert to specific migration
dotnet ef database update <migration-name> --connection "connection-string"

# Example: Revert to migration before breaking change
dotnet ef database update AddPickRulesTable --connection "connection-string"
```

---

## Troubleshooting

### Migrations Fail

**Error:** "A connection was successfully established with the server..."
- **Solution:** Check connection string format and database accessibility

**Error:** "The migration has already been applied"
- **Solution:** Safe to ignore, migration is idempotent

### Application Won't Start

**Check logs:**
```bash
# Render.com: View in dashboard logs tab
# Manual: Check application logs
tail -f /var/log/premierleague-api/app.log
```

**Common issues:**
1. Missing environment variables
2. Database connection failure
3. Port already in use (manual deployments)

### Rate Limiting Issues

If users hit rate limits:
- Adjust limits in `appsettings.json` under `IpRateLimiting`
- Consider using Redis for distributed rate limiting in multi-instance deployments

---

## Production Checklist

Before deploying to production:

- [ ] All secrets removed from appsettings.json
- [ ] Environment variables configured correctly
- [ ] `RunMigrationsOnStartup` is `false`
- [ ] HTTPS enforced (`RequireHttpsMetadata` = true)
- [ ] Rate limiting configured
- [ ] Health checks working
- [ ] Database backups configured
- [ ] Monitoring/alerting set up
- [ ] CORS allowed origins configured
- [ ] Test migration rollback procedure
- [ ] Document any manual migration steps

---

## Troubleshooting the cron-job.org Scheduler

### 401 from cron-job.org's REST API

**Symptom:** `generate` runs but logs a 401 when creating/deleting jobs on `api.cron-job.org`.

**Cause:** Bad or missing `CronJobsOrg__ApiKey` (the cron-job.org **account** API key).

**Fix:** Set a valid `CronJobsOrg__ApiKey` in the Render service. Use **separate** keys for prod and dev.

### 401 from our API

**Symptom:** The master job or a generated job gets 401 from `/api/v1/admin/...`.

**Cause:** Bad ExternalSync key. The master job authenticates via the `X-API-Key` header; generated jobs authenticate via the `?apiKey=` query parameter. Both are validated against `ExternalSync:ApiKey`.

**Fix:** Ensure the key configured in the cron-job.org job matches `ExternalSync__ApiKey` in the corresponding Render service.

```bash
# Test the master endpoint manually
curl -X POST https://api.eplpredict.com/api/v1/admin/schedule/generate \
  -H "X-API-Key: $EXTERNAL_SYNC_API_KEY" -v   # expect 200

# Test a generated-style call (query-param auth)
curl -X POST "https://api.eplpredict.com/api/v1/admin/sync/results?apiKey=$EXTERNAL_SYNC_API_KEY" -v
```

### 500 from `generate`

**Check:**
1. Render logs (Dashboard > Logs > filter "schedule") for the exception.
2. Database connectivity.
3. `FootballData__ApiKey` is set.

### Zero jobs created

**Symptom:** `generate` succeeds but no `EPL-` jobs appear.

**Cause:** No gameweeks/fixtures in the next 7 days (pre-season/off-season). **This is normal**, not a failure.

### Cold-start timeouts / master job auto-disabled

**Symptom:** `generate` times out; cron-job.org eventually auto-disables the master job.

**Cause:** cron-job.org's request timeout is a hard **30s** cap. Render free tier cold-starts (~24s) can exceed it. The timeout is client-side, so Kestrel aborts the request (cancellation token threaded through `generate`) — this can abort mid-flight, possibly **after** deleting old `EPL-` jobs but **before** recreating them, leaving that week unscheduled.

**Fix:**
1. Add/verify the **warm-up ping job** — a cron-job.org `GET` to `/health` at 08:55 and 08:58 Europe/London, per environment. Even if the first ping times out, it triggers the Render spin-up so `generate` lands warm.
2. Re-enable the master job in the cron-job.org dashboard if it was auto-disabled.
3. If a week ended up unscheduled, re-run the master job manually once the instance is warm.
4. **Do not** try to raise the cron-job.org timeout — it cannot be raised beyond 30s.

### A week's jobs disappeared / prod and dev interfering

**Symptom:** Jobs vanish unexpectedly, or one environment wipes the other's schedule.

**Cause:** The delete step (`SyncWeeklyJobsAsync`) removes **all** account-wide cron-job.org jobs whose title starts with `EPL-` before recreating. A shared `CronJobsOrg__ApiKey` across prod and dev lets one environment delete the other's `EPL-` jobs. It also means any job you create manually with an `EPL-` prefix will be deleted.

**Fix:** Use separate `CronJobsOrg__ApiKey` accounts for prod and dev, and never prefix the master/warm-up jobs with `EPL-`.

### Reminders Not Sending

**Check:**
1. **Email Configuration** — `Email__Brevo__ApiKey` and `Email__FromEmail`. A missing key logs
   `Email configuration is incomplete. Missing: ...`.
1a. **Sender verified in Brevo?** A 2xx from Brevo means *accepted*, not *delivered*. An
   unverified sender is rejected afterwards, so the endpoint reports success and no mail
   arrives. Check Brevo's transactional log, matching the `messageId` in our logs.
1b. **Was it a reminder window?** Reminders only send within 30 minutes of 24h or 3h before a
   deadline. Outside those bands the run correctly reports `0 sent, 0 failed`.
2. **User Email Addresses**
   ```sql
   SELECT "Email", "FirstName", "LastName" FROM "Users" WHERE "Email" IS NULL OR "Email" = '';
   ```
3. **API Logs** — look for "Sending pick reminders" entries.

### Score Sync Not Updating

**Check:**
1. The `EPL-{week}-sync-scores-*` jobs exist for the week and are enabled.
2. **football-data.org** API key is valid and hasn't hit rate limits (10 requests/minute free tier).
3. Fixtures have `ExternalId` values set in the database.
4. **Test Manual Sync:**
   ```bash
   curl -X POST "https://api.eplpredict.com/api/v1/admin/sync/results?apiKey=$EXTERNAL_SYNC_API_KEY"
   ```

---

## Support

For deployment issues:
1. Check application logs
2. Review Render.com build logs (if applicable)
3. Verify all environment variables are set
4. Test database connectivity
5. Check health endpoint response
6. Review the cron-job.org dashboard for job status/history

For questions or issues, create a GitHub issue or contact the development team.
