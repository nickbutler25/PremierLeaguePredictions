# Session Handoff — 2026-08-23

Status note for resuming fresh. Delete or `.gitignore` this file when no longer needed.

---

## TL;DR

Two long-standing problems are fixed: **live scores never synced** (an auth routing bug) and
**email never sent** (Render blocks SMTP). Both are verified working on dev. The league table
gained a current-pick crest and a form guide.

Gameweeks now finalise themselves, and the league table updates live off SignalR rather than a
poll.

`develop` is **11 commits ahead of `main`** and unmerged, with uncommitted frontend and backend
work on top. Until it merges, cron-triggered jobs keep running `main`'s older workflow file —
`repository_dispatch` always uses the default branch's copy.

**Next action:** open a PR `develop → main`, then verify a full gameweek on prod.

---

## Git state

- Branch: `develop`, 11 commits ahead of `origin/main`.
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

### Live scores — root cause found and fixed

`ApiKeyAuthenticationHandler` accepts the key from an `X-API-Key` header *or* an `apiKey` query
parameter, but `SmartScheme`'s `ForwardDefaultSelector` only routed to the ApiKey scheme on the
header. Query-string calls fell through to JWT Bearer, found no token, and 401'd before the
handler ran — the query-string branch was unreachable code, so the cron-job.org → API sync had
**never** authenticated.

The selector now matches either form, and the generated sync job sends the key as a header
instead (request logs record URLs in full, which is how the key leaked into screenshots).

Verified end to end on dev:

```
API Key authentication successful for ExternalSync
Polling 1 of 10 fixtures from external API for GW 1 (9 settled or not due)
Updated fixture: Brentford 3 - 0 Tottenham Hotspur (IN_PLAY -> FINISHED)
Recalculated 1 picks for GW 1
Sent SignalR notification for 1 fixture updates
```

### Score sync efficiency

Only fixtures that can still change are polled — settled ones (`FINISHED`/`AWARDED`/`CANCELLED`)
and those more than 15 minutes from kickoff are skipped. A live gameweek costs ~1
football-data.org call per cycle instead of 10, dropping to 0 once the last match ends. The sync
also stopped issuing an `UPDATE` per fixture per cycle (`UpdatedAt` was assigned unconditionally),
and `FixturesUpdated` — read in three places, assigned in none — now reports the real count.

### Email — SMTP is impossible on Render free

Probed from inside the container: ports 587, 465 and 25 all timed out at exactly 5s while
`www.google.com:443` connected in 14ms. Email now goes through **Brevo's REST API** over HTTPS.

- `IEmailService.SendEmailAsync` returns `EmailSendResult` (`Sent`/`Skipped`/`Failed`) instead of
  `bool`. Previously `SmtpEmailService` swallowed every exception without rethrowing, so a send
  that timed out for 100 seconds still reported `1 sent, 0 failed`.
- `SendBulkAsync` sends reminders with bounded concurrency (`Email:MaxConcurrentSends`, default
  5). The old per-user loop did a `GetByIdAsync` *and* an HTTP request each — at ~275 players that
  is minutes of work, longer than the calling workflow's 120s timeout.
- The **12h reminder was dropped**; 24h and 3h remain. It landed overnight for a Saturday deadline
  and put two full sends on one calendar day.
- `NoSendAddresses` skips RFC 2606 reserved domains (`example.com`, `.test`, `.invalid`, …), so
  seeded test users are never mailed and are counted as skipped, not sent.

### Schedule generation

`POST /admin/schedule/generate` returns **202** and runs in the background, with
`GET /admin/schedule/generate/status` for progress; a concurrent start is refused with 409. Calls
to cron-job.org are paced, so a generate takes minutes and cannot be held open on a request. The
plan is scoped to gameweeks with deadlines within 7 days either side of now — it previously
included every unlocked gameweek in the season and produced 158 jobs. Job titles now name the
season and gameweek (`EPL-DEV-2026-2027-GW1-sync-scores-1`).

### League table

Two columns. **Pick** shows the crest of the current gameweek's pick with a W/D/L letter badge,
revealed only once the deadline passes and coloured live during a match. **Form** (full page only)
shows crests from the last 10 *completed* gameweeks. Form is computed before the standings cache;
the live pick is layered on after it.

### Gameweeks complete themselves

Nothing ever locked a gameweek — `IsLocked` was set only in tests, though four places read it.
A job at last kickoff + 2h30 pulls results once more, checks every fixture is settled, processes
eliminations and locks the gameweek. A postponed fixture leaves it open rather than eliminating
players on a scoreline still missing a match; the weekly generate re-plans a completion job for
any gameweek past its deadline and still unlocked, so it retries until the match is replayed.

`ResultsService` used to count POSTPONED as "all fixtures finished" and would have eliminated on
exactly those gameweeks. Both now settle on FINISHED, CANCELLED or AWARDED.

### Live scores reach the league table

Two separate faults, both fixed:

- **In-progress picks counted as losses.** The standings query includes a pick whose fixture is
  IN_PLAY or PAUSED and classifies it by its points, but `RecalculatePointsForGameweekAsync` only
  scored FINISHED fixtures — so a live pick sat at zero points, and zero reads as a loss. It now
  uses the `CalculatePickPoints` helper that already existed for the backfill path, which scores
  matches under way.
- **The push was blocked by the cache.** `useResultsUpdates` already invalidated
  `['league-standings']` on the SignalR event, but the refetch was served a five-minute-old
  cached response. The sync now evicts `standings_{seasonId}` *before* publishing, and
  `refetchInterval` has been removed from the table — it is push-driven now.

`FootballDataService` logs what the provider returned for every fixture, with raw bodies behind
`FootballData__LogRawResponses`. An empty score there means the provider sent null rather than the
match being goalless — a distinction that cost an afternoon of guessing.

### Config defaults removed

`ApiBaseUrl`, `GitHub:Environment` and `Email:FromEmail` no longer have defaults in
`appsettings.json`. All three defaulted to production values inherited by every environment —
dev generated cron jobs whose score syncs pointed at the live API and failed with 503, silently.
They are now required per environment and throw when absent.

---

## Outstanding

1. **Open PR `develop → main`.** The workflow half of these changes has no effect until merged.
2. **Verify a full gameweek on prod.** Everything was proven on dev with one real participant.
   Both `ApiBaseUrl` and the cron-job.org quota silently broke this path before.
3. **Rotate two credentials:**
   - `ExternalSync__ApiKey` (`sk_live_9fA7Qx…`) — exposed in cron-job.org screenshots. After
     rotating, re-run generate so sync jobs pick up the new key in their header.
   - `GitHub__Token` — the PAT was logged in plaintext to Render on 2026-08-21 (the client logged
     the full request body on failure; that logging is now redacted, but the leaked value stands).
4. **`appsettings.Development.json`** (untracked) still has the old `plpredictions.com` sender.
5. **Raise cron-job.org `RequestTimeout`** (30s) above Render's cold start (~24s).
6. **Watch Brevo's 300/day cap.** ~165 on a peak day at 275 players, worst case 275.
7. **`DeleteExistingEplJobsAsync` wipes before creating**, so a mid-run failure leaves the week
   unscheduled. Diffing desired-vs-existing would make a no-op rerun cost one `GET /jobs`.
8. **Split the standings cache into settled and live halves** — see below. Worth doing before
   go-live at ~275 players, but measure first.

---

## Gotchas to remember

- **Standings are cached server-side for 5 minutes** (`LeagueService.StandingsCacheDuration`),
  but the cache is now evicted whenever the score sync changes a fixture, so live scores appear
  immediately. Changes made *outside* the sync — seeding users, editing picks directly in the
  database — still take up to five minutes to show, on top of React Query's 5-minute
  `staleTime`. That has caused confusion twice; it is not a bug.
- **The league table is push-driven, not polled.** `useResultsUpdates` (mounted in `Layout`)
  invalidates `['league-standings']` on the SignalR `ResultsUpdated` event. Do not reintroduce
  `refetchInterval` — at a few hundred players that is roughly a request a second, all day,
  which is the load the cache exists to absorb.
- **Reminders only fire within 30 minutes of 24h or 3h before a deadline.** A manual run outside
  those bands correctly reports `0 sent, 0 failed`, so you cannot test reminders on demand — use
  `POST /api/v1/admin/email/test` instead (accepts a `"to"` override in Development only).
- **A 2xx from Brevo means accepted, not delivered.** An unverified sender is rejected afterwards.
  Match the `messageId` in our logs against Brevo's transactional log.
- **cron-job.org's API quota is undocumented and unforgiving** — exhausting it locked out generate
  for over eight hours. The opening `GET /jobs` deliberately does not retry a 429.
- **cron-job.org schedules are a cross-product of hours × minutes**, so a 16:30–18:30 window
  actually fires 16:00–18:58. Harmless now that idle cycles cost one query.
- **`repository_dispatch` runs the workflow file from the default branch**, never the branch you
  edited. Workflow changes need merging to `main` to take effect.
- Season names must NOT contain `/` (breaks `seasonId`-in-URL routing); use `2026-2027`.
- Secrets: `appsettings.Development.json` is gitignored with real dev secrets — never commit it.
- Backend build MSB file-locks happen when the dev backend / Visual Studio is running.

---

## Follow-up: split the standings cache into settled and live halves

`GetStandingsDataAsync` recomputes the **whole season** on every cache miss — one SQL statement
with eight correlated subqueries per user, plus `AttachFormAsync` reading every pick in the
season. Now that a goal evicts the cache, a single goal in GW20 recomputes nineteen gameweeks
that cannot possibly have changed. The cost grows every week; the part that actually moves does
not.

Completed gameweeks are immutable. Their points, wins, goals and form entries are settled the
moment the gameweek is locked. Only the gameweek in play changes, and that is ~275 picks against
~10 fixtures.

Suggested shape:

- **Historical totals per user** — cached under a key that includes the last completed gameweek,
  e.g. `standings_2026-2027_gw19`. When a gameweek completes the key changes by itself and the
  old entry ages out, so there is no invalidation logic to get wrong.
- **Current gameweek delta per user** — computed fresh on every request. Bounded and cheap.
- Sum the two for the table.

The form guide falls out of this neatly: it is *entirely* historical, so it can be cached for the
week rather than rebuilt on every goal, which is what happens today.

**Measure before building.** Dev has 21 users and one gameweek, which will not show the problem.
Get the uncached endpoint timing from prod once real players are loaded, or run `EXPLAIN ANALYZE`
against prod-shaped data. A previous scaling assumption in this area turned out to be wrong
because it was reasoned from the schema rather than measured.

---

## Running the UI locally against dev

`.env.development.local` outranks `.env.local` in Vite and is gitignored, so it overrides without
touching your existing config. Port 3000 is required — dev's CORS allowlist has no other localhost
origin.

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
