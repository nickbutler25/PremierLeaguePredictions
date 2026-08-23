# Live Scores Setup - cron-job.org Scheduler

This document explains how live score updates, pick reminders, and auto-picks are scheduled using the external **cron-job.org** service. This is the canonical reference for the scheduling flow.

> **Note:** Scheduling used to run on GitHub Actions (a master-scheduler workflow that committed weekly job YAML to the repo). That approach has been retired. GitHub Actions is now used **only** for CI/CD (test/build/deploy) — not for scheduling. If you see references to `master-scheduler.yml`, `weekly-jobs-*.yml`, or `GitHub__*` env vars, they are outdated.

## Overview

**Problem**: Render's free tier spins the app down after ~15 minutes of inactivity, making continuous score updates challenging.

**Solution**: Use **cron-job.org** (a free external cron service) with a single weekly "master" job that calls our API to generate a precise per-week schedule based on actual fixture times. The API then creates one cron-job.org job per task via the cron-job.org REST API, so scores update during matches without wasting resources.

## Architecture

### Dynamic Scheduling System

1. **Master Job** (created once, manually, per environment, in the cron-job.org dashboard)
   - Runs weekly: **Mondays 09:00 Europe/London**.
   - `POST`s to `/api/v1/admin/schedule/generate`.
   - Authenticated via the `X-API-Key` header, whose value is the ExternalSync API key.
   - Its title must **NOT** start with `EPL-` (see the cleanup rule below).

2. **Generated Jobs** (`EPL-{weekNumber}-{jobType}-{n}`)
   - Created automatically by the `generate` call, one cron-job.org job per (jobType, schedule).
   - Cover reminders, auto-picks, and live-score syncs for the coming week.
   - Regenerated every Monday; the previous week's `EPL-` jobs are deleted first.

### What `generate` does

`POST /api/v1/admin/schedule/generate` runs `CronSchedulerService.GenerateWeeklyScheduleAsync`, which builds a `SchedulePlan` from unlocked gameweeks that have deadlines/fixtures in the next 7 days:

- **Reminder emails** at 24h and 3h before each deadline, to players who have not yet picked.
- **Auto-pick** at each deadline.
- **Live-score sync** every 2 minutes during each match window (kickoff → +2h, with fixtures grouped into 15-minute kickoff windows).

Then `CronJobsOrgService.SyncWeeklyJobsAsync` calls the cron-job.org REST API to create one cron-job.org job per (jobType, schedule). Before creating the new jobs, it **deletes all existing cron-job.org jobs whose title starts with `EPL-`**. This is why the master job (and the warm-up job) must **not** be `EPL-` prefixed — otherwise they would delete themselves. Generated jobs are titled `EPL-{weekNumber}-{jobType}-{n}`.

> **Off-season is not a failure.** If there are no gameweeks/fixtures in the next 7 days (e.g. pre-season/off-season), `generate` correctly creates **zero** `EPL-` jobs. This is expected behaviour, not an error.

### Generated job endpoints

Generated jobs hit these API endpoints:

| Job type | Method + endpoint |
|---|---|
| `send-reminders` | `POST /api/v1/admin/schedule/reminders` |
| `auto-pick` | `POST /api/v1/admin/schedule/auto-pick` |
| `sync-scores` | `POST /api/v1/admin/sync/results` |

Generated jobs authenticate with an **`X-API-Key` header**, set through cron-job.org's `extendedData.headers`. (An earlier note here claimed the REST API could not set custom headers — it can, and the GitHub dispatch jobs have always relied on it. The key was previously in the query string, which put it into every request log and into the job URL visible in cron-job.org's UI.)

Note that the API accepts the key either way: `SmartScheme` routes to the API-key handler on an `X-API-Key` header *or* an `apiKey` query parameter. Until 2026-08-22 it matched only the header, so query-string calls fell through to JWT Bearer and 401'd — which is why score syncing never worked.

The API's `ApiKeyAuthenticationHandler` accepts the ExternalSync key from **either** the `X-API-Key` header **or** the `apiKey` query-string parameter, validates it against config `ExternalSync:ApiKey`, and grants the Admin role for that request.

## Live Score Flow

```
cron-job.org sync-scores job (every 2 min during match windows)
  → POST /api/v1/admin/sync/results  (API key auth via X-API-Key header)
  → ResultsService fetches from football-data.org
  → results saved to DB
  → SignalR pushes updates to connected frontend clients
```

## Setup Instructions

You configure two jobs **per environment** (production and development) in the cron-job.org dashboard. This is a one-time manual setup.

### 1. Set environment variables (per Render service)

| Variable | Purpose |
|---|---|
| `CronJobsOrg__ApiKey` | The cron-job.org **account** API key. Used as a Bearer token so our API can call `api.cron-job.org` to list/create/delete jobs. Set **manually in each Render service's Environment tab** (in the Render dashboard). |
| `ExternalSync__ApiKey` | The key external callers (the cron-job.org jobs) use to authenticate **to** our API. Set per Render service. |
| `ApiBaseUrl` | Base URL the generated jobs' target URLs are built from. |

> **Use separate `CronJobsOrg__ApiKey` values for prod and dev.** A shared account key makes the two environments wipe each other's `EPL-` jobs, because the delete step is account-wide.

### 2. Create the master job

In the cron-job.org dashboard, create a job:

**Production**
- Title: `Prem Predictions Schedule Generate` (must **not** start with `EPL-`)
- URL: `https://api.eplpredict.com/api/v1/admin/schedule/generate`
- Method: `POST`
- Header: `X-API-Key: <ExternalSync key>`
- Schedule: Mondays 09:00 Europe/London

**Development**
- Title: `Prem Predictions Schedule Generate Dev`
- URL: `https://premierleague-api-dev.onrender.com/api/v1/admin/schedule/generate`
- Method: `POST`
- Header: `X-API-Key: <ExternalSync key>`
- Schedule: Mondays 09:00 Europe/London

### 3. Create the warm-up (health-ping) job

Render's free tier cold-starts (~24s) can cause the Monday `generate` to time out (see [Operational Constraints](#operational-constraints)). To mitigate, add a warm-up job **per environment** that `GET`s `/health` a few minutes before the master job, e.g. at **08:55 and 08:58 Europe/London**:

- Prod → `https://api.eplpredict.com/health`
- Dev → `https://premierleague-api-dev.onrender.com/health`

The `/health` endpoint needs no auth and no body — hitting it warms both the app process and the DB connection. Even if the first ping itself times out on a cold instance, it still triggers the Render spin-up, so `generate` lands on a warm instance. The warm-up job's title must **not** start with `EPL-`.

## Manual "Refresh Scores" Button

Admins also have a **"⟳ Refresh Scores"** button in the Fixtures card on the dashboard:
- Click to immediately trigger a sync.
- Uses your admin JWT token (no API key needed).
- Useful for testing and immediate updates during matches.

## Operational Constraints

- **cron-job.org request timeout is 30 seconds** — a hard cap that **cannot** be raised. Do not attempt to work around scheduling issues by raising it.
- **Render free tier** spins the app down after ~15 min idle; cold start is ~24s. So the Monday `generate` (and match-window syncs) can hit a cold instance.
- The 30s timeout is **client-side**: if cron-job.org gives up, Kestrel aborts the request (the cancellation token is threaded through `generate`), which can abort `generate` mid-flight — possibly **after** deleting the old `EPL-` jobs but **before** recreating them, leaving that week unscheduled. Repeated timeouts also cause cron-job.org to **auto-disable** the job.
- **Mitigation:** the warm-up ping job described above. This is the supported fix — again, do not try to raise the 30s cron-job.org timeout.

### Health endpoints

Health endpoints are `GET`, and are **not** shown in Swagger (they are `MapHealthChecks` endpoints, not controllers):

| Endpoint | Checks |
|---|---|
| `/health` | Full — includes DB check |
| `/health/live` | Liveness only, no DB |
| `/health/ready` | Readiness / DB |

## Troubleshooting

### `generate` returns 401 (from cron-job.org)
- The master job could not authenticate our API. Check that the master job's `X-API-Key` header matches `ExternalSync__ApiKey` in the corresponding Render service.

### `generate` returns 401 when calling cron-job.org's REST API
- Our API could not authenticate to `api.cron-job.org`. Check `CronJobsOrg__ApiKey` in the Render service — it must be a valid cron-job.org account API key.

### `generate` returns 500
- Check Render logs for the error.
- Verify the database connection is working.
- Verify `FootballData__ApiKey` is set.

### Zero `EPL-` jobs created
- Normal during pre-season/off-season when there are no gameweeks/fixtures in the next 7 days. Not a failure.

### `generate` times out / cron-job.org auto-disabled the master job
- Almost always a Render cold-start hitting the 30s cap. Confirm the warm-up ping job exists and runs a few minutes before 09:00. Re-enable the master job in the cron-job.org dashboard if it was auto-disabled.
- If a timeout aborted `generate` after deletion but before recreation, that week may be unscheduled — re-run the master job manually from the cron-job.org dashboard once the instance is warm.

### A week's jobs vanished unexpectedly
- Check you are not sharing one `CronJobsOrg__ApiKey` across prod and dev. The delete step is account-wide and only spares titles that don't start with `EPL-`, so a shared key lets one environment wipe the other's `EPL-` jobs.

### Scores still not updating
- Verify the sync jobs exist for the week (`EPL-{week}-sync-scores-*`) and are enabled.
- Verify football-data.org rate limits haven't been exceeded (10 calls/minute on the free tier).
- Check that fixtures have `ExternalId` values set in the database.

## Cost / Limits

- **cron-job.org (free tier):** covers the master job, the warm-up pings, and the generated per-week jobs. The 30s request timeout is the main constraint to design around.
- **Render (free tier):** the app spins down after ~15 min idle; each incoming request (including the warm-up ping) wakes it. No additional cost.

## Summary

- **External scheduling** — cron-job.org runs the weekly master job; no GitHub Actions scheduling.
- **Dynamic** — jobs generated each Monday from actual fixture times, grouped into 15-min kickoff windows.
- **Self-managing** — old `EPL-` jobs are deleted and recreated on each run.
- **Resource efficient** — only syncs during matches, reminders, and deadlines.
- **Off-season safe** — zero jobs when there are no upcoming fixtures.
- **Cold-start aware** — a warm-up ping keeps `generate` off cold instances.
