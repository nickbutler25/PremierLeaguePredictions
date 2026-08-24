# Session Handoff — 2026-08-24

Status note for resuming fresh. Delete or `.gitignore` this file when no longer needed.

---

## TL;DR

Two long-standing problems are fixed and **verified against real live matches**: live scores never
synced (an auth routing bug), and email never sent (Render blocks outbound SMTP). The league table
now shows each player's current pick and recent form, and updates on a SignalR push rather than a
poll. Gameweeks finalise themselves via a new scheduled job.

`develop` is **14 commits ahead of `main`** and unmerged, working tree clean. Until it merges,
cron-triggered jobs keep running `main`'s older workflow file — `repository_dispatch` always uses
the default branch's copy.

**Next action:** open a PR `develop → main`, then watch a full gameweek on prod.

---

## Git state

- Branch: `develop`, 14 commits ahead of `origin/main`, clean.
  - `61a4212` Stop watching appsettings files for changes (inotify crash fix)
  - `61b6553` Turn down framework logging so the app's own lines are readable
  - `03a15a1` Push league standings on score changes instead of polling for them
  - `008709b` Score matches in progress, and log what football-data.org returns
  - `654a069` Add a scheduled job that completes a gameweek once its football is over
  - `695b143` Update docs for the score-sync, email and league-table changes
  - `62a3d26` Apply Prettier formatting to LeagueStandings
  - `78492b7` Show W/D/L beside the pick crest, and stop defaulting the sender address
  - `e7ca6fc` Show each player's current pick and recent form in the league table
  - `a8bccbb` Send email through Brevo's HTTP API and batch reminder sends
  - `13252c6` Report the fixture update count and skip work when nothing is pollable
  - `0e782a0` Poll only fixtures that can still change during score sync
  - `6ee6c27` Fix API-key auth for query-string callers and report email outcomes honestly
  - `0fa6b59` Add temporary outbound connectivity probe
- Branch strategy: `feature/* → develop → main` (PRs, tests must pass; `main` auto-deploys).
- **Do not commit/push without explicit instruction** (standing user rule).

---

## What changed

### Live scores — root cause found, fixed, verified

`ApiKeyAuthenticationHandler` accepts the key from an `X-API-Key` header *or* an `apiKey` query
parameter, but `SmartScheme`'s `ForwardDefaultSelector` only routed to the ApiKey scheme on the
header. Query-string calls fell through to JWT Bearer, found no token, and 401'd before the handler
ran — the query-string branch was unreachable, so the cron-job.org → API sync had **never**
authenticated.

The selector now matches either form, and the generated sync job sends the key as a header
(request logs record URLs in full, which is how this key leaked into screenshots).

**Verified on dev across a real gameweek**, 2026-08-23:

```
football-data 560548: status=IN_PLAY fullTime=1-1 halfTime=0-1
Updated fixture: Manchester City 1 - 1 AFC Bournemouth (IN_PLAY -> IN_PLAY)
Points recalculated for gameweek 2026-2027-1: 5 pick(s) changed
Sent SignalR notification for 1 fixture updates
```

…and again at full time for both `Brighton 4 - 0 Aston Villa` and `Manchester City 2 - 1
AFC Bournemouth`.

### Score sync efficiency

Only fixtures that can still change are polled — settled ones (`FINISHED`/`AWARDED`/`CANCELLED`)
and those more than 15 minutes from kickoff are skipped. A live gameweek costs ~1 football-data.org
call per cycle instead of 10, dropping to 0 once the last match ends.

Two phantom-write patterns removed: fixtures and picks were both marked dirty every cycle by an
unconditional `UpdatedAt`, so a sync reporting "0 updated" still issued a row per fixture and per
player. `FixturesUpdated` — read in three places, assigned in none — now reports the real count.

### Live scores reach the league table

Two separate faults:

- **In-progress picks counted as losses.** The standings query includes a pick whose fixture is
  IN_PLAY or PAUSED and classifies it by points, but `RecalculatePointsForGameweekAsync` only
  scored FINISHED fixtures — so a live pick sat at zero points, and zero reads as a loss. It now
  uses the `CalculatePickPoints` helper that already existed for the backfill path.
- **The push was blocked by the cache.** `useResultsUpdates` already invalidated
  `['league-standings']` on the SignalR event, but the refetch was served a five-minute-old cached
  response. The sync now evicts `standings_{seasonId}` *before* publishing, and `refetchInterval`
  is gone from the table — it is push-driven.

### Email — SMTP is impossible on Render free

Probed from inside the container: ports 587, 465 and 25 all timed out at exactly 5s while
`www.google.com:443` connected in 14ms. Email goes through **Brevo's REST API** over HTTPS, and a
test send has been delivered successfully.

- `IEmailService.SendEmailAsync` returns `EmailSendResult` (`Sent`/`Skipped`/`Failed`) instead of
  `bool`. `SmtpEmailService` swallowed every exception without rethrowing, so a send that timed out
  for 100 seconds still reported `1 sent, 0 failed`.
- `SendBulkAsync` sends reminders with bounded concurrency (`Email:MaxConcurrentSends`, default 5).
  The old per-user loop did a `GetByIdAsync` *and* an HTTP request each — at ~275 players that is
  minutes of work, longer than the calling workflow's 120s timeout.
- The **12h reminder was dropped**; 24h and 3h remain.
- `NoSendAddresses` skips RFC 2606 reserved domains, so seeded test users are never mailed and are
  counted as skipped, not sent.

### Schedule generation

`POST /admin/schedule/generate` returns **202** and runs in the background, with
`GET /admin/schedule/generate/status` for progress; a concurrent start is refused with 409. Calls to
cron-job.org are paced. The plan is scoped to gameweeks with deadlines within 7 days either side of
now — it previously included every unlocked gameweek and produced 158 jobs. Job titles name the
season and gameweek (`EPL-DEV-2026-2027-GW1-sync-scores-1`).

### Gameweeks complete themselves

Nothing ever locked a gameweek — `IsLocked` was set only in tests, though four places read it. A job
at last kickoff + 2h30 pulls results once more, checks every fixture is settled, processes
eliminations and locks the gameweek. A postponed fixture leaves it open rather than eliminating
players on an incomplete scoreline; the weekly generate re-plans a completion job for any gameweek
past its deadline and still unlocked, so it retries until the match is replayed.

`ResultsService` used to count POSTPONED as "all fixtures finished". Both now settle on FINISHED,
CANCELLED or AWARDED.

### League table

**Pick** shows the crest of the current gameweek's pick with a green/amber/red W-D-L badge,
revealed only once the deadline passes and coloured live during a match. **Form** (full page only)
shows crests from the last 10 *completed* gameweeks. Form is computed before the standings cache;
the live pick is layered on after it.

The dashboard's compact widget hides P/W/D/L **via the `compact` prop, not a breakpoint** — Tailwind
breakpoints key off viewport width, so they cannot help a ~500px card on a 1900px screen.

### Logging and config

- Framework logging turned down to Warning: `Microsoft.EntityFrameworkCore.Database.Command` (full
  SQL of every query), `Microsoft.AspNetCore` (four lines per request on top of Serilog's one-line
  summary), EF infrastructure, and `HttpClient`. A sync cycle went from ~30 lines to ~9.
- `FootballDataService` logs what the provider returned for every fixture; raw bodies behind
  `FootballData__LogRawResponses`. An empty score there means the provider sent null rather than the
  match being goalless.
- `ApiBaseUrl`, `GitHub:Environment` and `Email:FromEmail` no longer have defaults in
  `appsettings.json`. All three defaulted to production values inherited by every environment — dev
  generated cron jobs whose score syncs pointed at the live API and failed with 503, silently. They
  are now required per environment and throw when absent.
- `DOTNET_hostBuilder__reloadConfigOnChange=false` in both Dockerfiles. The API crashed on startup
  (exit 139) because `WebApplication.CreateBuilder` opens an inotify instance per watched appsettings
  file and the host limit (128, shared across containers) was exhausted. **Takes effect only on
  image rebuild** — the same variable in Render's Environment tab applies without one.

---

## Verified vs not

**Verified on dev:** score sync end to end during live matches; points recalculating live; SignalR
push; schedule generation (20 jobs for GW1+GW2); a delivered Brevo test email; the league table Pick
column.

**Not verified:**

- **No full gameweek has run on prod.** Everything was proven on dev with one real participant plus
  twenty seeded test users.
- **Gameweek completion has never fired**, since it needs a gameweek to actually finish. Watch for
  `GW1 completed and locked`, or the `not complete — N of M fixtures unsettled` warning.
- **The Form column has never displayed data** — it needs a completed gameweek.
- **Reminders have not been sent for real.** They only fire within 30 minutes of 24h or 3h before a
  deadline.

---

## Outstanding

1. **Open PR `develop → main`.** The workflow half of these changes has no effect until merged.
2. **Verify a full gameweek on prod**, including completion and eliminations.
3. **Rotate two credentials:**
   - `ExternalSync__ApiKey` (`sk_live_9fA7Qx…`) — exposed in cron-job.org screenshots. After
     rotating, re-run generate so sync jobs pick up the new key in their header.
   - `GitHub__Token` — the PAT was logged in plaintext to Render on 2026-08-21. That logging is now
     redacted, but the leaked value stands.
4. **Delete `AdminDiagnosticsController`** — added to prove the SMTP block, has served its purpose.
5. **`appsettings.Development.json`** (untracked) still has the old `plpredictions.com` sender.
6. **Raise cron-job.org `RequestTimeout`** (30s) above Render's cold start (~24s).
7. **Watch Brevo's 300/day cap.** ~165 on a peak day at 275 players, worst case 275.
8. **`DeleteExistingEplJobsAsync` wipes before creating**, so a mid-run failure leaves the week
   unscheduled. Diffing desired-vs-existing would make a no-op rerun cost one `GET /jobs`.
9. **Split the standings cache into settled and live halves** — see below. Before go-live at ~275
   players, but measure first.

---

## Gotchas to remember

- **Standings are cached server-side for 5 minutes** (`LeagueService.StandingsCacheDuration`), but
  the cache is evicted whenever the score sync changes a fixture, so live scores appear immediately.
  Changes made *outside* the sync — seeding users, editing picks directly in the database — still
  take up to five minutes, on top of React Query's 5-minute `staleTime`. That has caused confusion
  twice; it is not a bug.
- **The league table is push-driven, not polled.** `useResultsUpdates` (mounted in `Layout`)
  invalidates `['league-standings']` on the SignalR `ResultsUpdated` event. Do not reintroduce
  `refetchInterval` — at a few hundred players that is roughly a request a second, all day, which is
  the load the cache exists to absorb.
- **Two sync runs per cycle during a busy Saturday is expected**, not a duplicate job. cron-job.org
  schedules are a cross-product of hours × minutes, so two genuine match windows overlapping (e.g.
  12:30 and 15:00 kickoffs) fire on the same minutes. A 16:30–18:30 window also runs 16:00–18:58 for
  the same reason.
- **Reminders only fire within 30 minutes of 24h or 3h before a deadline.** A manual run outside
  those bands correctly reports `0 sent, 0 failed`, so you cannot test reminders on demand — use
  `POST /api/v1/admin/email/test` instead (accepts a `"to"` override in Development only).
- **A 2xx from Brevo means accepted, not delivered.** An unverified sender is rejected afterwards.
  Match the `messageId` in our logs against Brevo's transactional log.
- **cron-job.org's API quota is undocumented and unforgiving** — exhausting it locked out generate
  for over eight hours. The opening `GET /jobs` deliberately does not retry a 429.
- **`repository_dispatch` runs the workflow file from the default branch**, never the branch you
  edited.
- **Points recalculation only runs when a fixture changes.** A change to scoring logic does not apply
  retroactively; `POST /api/v1/admin/gameweeks/{seasonId}/{gw}/recalculate` forces it.
- Season names must NOT contain `/` (breaks `seasonId`-in-URL routing); use `2026-2027`.
- Secrets: `appsettings.Development.json` is gitignored with real dev secrets — never commit it.

---

## Follow-up: split the standings cache into settled and live halves

`GetStandingsDataAsync` recomputes the **whole season** on every cache miss — one SQL statement with
eight correlated subqueries per user, plus `AttachFormAsync` reading every pick in the season. Now
that a goal evicts the cache, a single goal in GW20 recomputes nineteen gameweeks that cannot have
changed. The cost grows every week; the part that actually moves does not.

Completed gameweeks are immutable. Only the gameweek in play changes, and that is ~275 picks against
~10 fixtures.

Suggested shape:

- **Historical totals per user** — cached under a key that includes the last completed gameweek, e.g.
  `standings_2026-2027_gw19`. When a gameweek completes the key changes by itself and the old entry
  ages out, so there is no invalidation logic to get wrong.
- **Current gameweek delta per user** — computed fresh on every request. Bounded and cheap.
- Sum the two for the table.

The form guide falls out neatly: it is *entirely* historical, so it can be cached for the week rather
than rebuilt on every goal.

**Measure before building.** Dev has 21 users and one gameweek, which will not show the problem. Get
the uncached endpoint timing from prod once real players are loaded, or run `EXPLAIN ANALYZE` against
prod-shaped data. A scaling assumption in this area turned out to be wrong earlier because it was
reasoned from the schema rather than measured.

---

## Required environment variables

Both Render services need these, or startup throws:

| Key | dev | prod |
|---|---|---|
| `ApiBaseUrl` | `https://premierleague-api-dev.onrender.com` | `https://api.eplpredict.com` |
| `GitHub__Environment` | `dev` | `prod` |
| `Email__FromEmail` | a sender verified in Brevo | a sender verified in Brevo |
| `Email__Brevo__ApiKey` | `xkeysib-…` | `xkeysib-…` |
| `Email__FromName` | `Premier League Predictions Dev` | `Premier League Predictions` |

No trailing slash on `ApiBaseUrl` — it is concatenated directly with `/api/v1/admin/sync/results`.
The `Email__Smtp*` variables are no longer read.

---

## Running the UI locally against dev

`.env.development.local` outranks `.env.local` in Vite and is gitignored. Port 3000 is required —
dev's CORS allowlist has no other localhost origin.

```bash
# frontend/.env.development.local
VITE_API_URL=https://premierleague-api-dev.onrender.com
VITE_GOOGLE_CLIENT_ID=<same as .env.local>
VITE_USE_MOCK_API=false
```

```bash
cd frontend && npm run dev -- --port 3000 --strictPort
```

Delete the file afterwards to point back at the local backend on `:5154`.

---

## CI gates (all must pass)

```bash
cd frontend
npm run lint
npm run format:check     # Prettier fails CI readily — run before pushing
npx tsc -b --noEmit
npm run test -- --run

cd ../backend
dotnet build
dotnet test --filter "FullyQualifiedName!~Integration"   # integration tests need a local Postgres
```
