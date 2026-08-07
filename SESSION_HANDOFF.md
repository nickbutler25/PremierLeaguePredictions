# Session Handoff — 2026-08-06

Status note for resuming fresh. Delete or `.gitignore` this file when no longer needed.

---

## TL;DR

All CI/CD pipeline checks are **green** (verified locally against a fresh DB replicating CI).
The work is committed on `develop` (2 commits ahead of `main`), working tree is **clean**.
**Next action:** open a PR `develop → main` (title/description below). Nothing left to commit.

---

## Git state

- Branch: `develop` (2 commits ahead of `origin/main`, working tree clean)
  - `cb0e194` Scope views to the active season, deactivate relegated teams, and polish season UX
  - `24b8304` CI / CD testing fixes (includes the E2E seeder fix)
- Branch strategy: `feature/* → develop → main` (PRs, tests must pass; `main` auto-deploys to Render).
- **Do not commit/push without explicit instruction** (standing user rule).

## Changeset (10 files, `main..develop`)

**Active-season scoping** (gameweek numbers 1–38 repeat each season; frontend keys deadlines by
week number, so returning all seasons let old past deadlines lock every week):
- `GameweekService.GetAllGameweeksAsync` → active season only (empty if none active).
- `TeamService.GetAllTeamsAsync` → active teams only.

**Relegation/promotion on sync:**
- `FixtureSyncService.SyncTeamsAsync` → deactivates relegated teams, reactivates promoted, busts
  the 24h teams cache (`all_teams`).
- `FixtureSyncServiceTests` (EF InMemory + Moq) covers this. **Note: CI has NO `dotnet test` job —
  backend tests only run locally.**

**E2E / CI fix (the thing we just finished):**
- `DbSeeder.SeedE2eSeasonAsync` now makes the `E2E-TEST` season **active whenever no real active
  season exists** (always true in the isolated E2E/CI DB), idempotently (activates even an
  already-seeded inactive row). In dev, where a real active season exists, it stays inactive.
- Root cause of the CI failure `picks.spec.ts:45 › should allow selecting a team for a gameweek`:
  fresh CI DB had no active season → `GetAllGameweeksAsync` returned empty → frontend `gameweek`
  undefined → `seasonId` (`activeSeason?.name || gameweek?.seasonId` in `Picks.tsx`) undefined →
  guard blocked the pick → `pick-team-gw1` never rendered.

**Season admin UX:**
- `SeasonManagementPage.tsx` — Create Season dropdown offers only the current season (mid-June
  fixtures-release rollover, `FIXTURES_RELEASE_DAY = 20`); disabled + message when none available.
- `SeasonApprovalsPage.tsx` — removed by-season filter + Season column; always scoped to the
  active season.
- `SeasonManagementPage.test.tsx` — date-robust current-season computation + new assertions.

**Mobile / UI polish:**
- `index.html` — iOS `apple-mobile-web-app-status-bar-style` = `default` (content below notch on
  home-screen PWA). **Requires deploy + delete/re-add the home-screen app to verify** (the meta is
  cached at install time).
- `index.css` — theme-aware scrollbars (uses `muted-foreground` token; fixes dark-mode scrollbar).

## Verification done this session (all ✅)

Replicated the CI E2E job locally: backend with `ASPNETCORE_ENVIRONMENT=Testing`,
`RunMigrationsOnStartup=true`, against a **fresh empty Postgres DB**, then:
- E2E (Playwright): **36 passed** (incl. the previously-failing pick test; no regressions)
- Lint (ESLint) ✅ · Format (Prettier) ✅ · Typecheck (`tsc -b`) ✅
- Unit tests (Vitest): **87 passed** ✅
- Production build (`vite build`) ✅
- Confirmed seeder produces `E2E-TEST active=true` on a fresh DB.

## Next steps / open items

1. **Open PR `develop → main`** using the title/description below (waiting on your go-ahead to
   push/create with `gh`).
2. Standing TODOs (from project memory, not addressed this session):
   - Rotate the exposed `ExternalSync` API key (`sk_live_9fA7Qx...`).
   - Deploy the iPhone status-bar fix, then delete/re-add the PWA to confirm the notch rendering.
   - Raise cron-job.org `RequestTimeout` (30s) above Render cold-start (~24s).
   - Live scores not always updating during games — root cause still unknown (SignalR / sync job /
     football-data polling / frontend render).

## How to re-run the full E2E suite locally like CI (recipe used this session)

```bash
# 1. Fresh DB (local Postgres :5432, user postgres, pw dDSqDJ8gtb)
psql -h localhost -U postgres -d postgres -c "DROP DATABASE IF EXISTS plp_e2e_verify;"
psql -h localhost -U postgres -d postgres -c "CREATE DATABASE plp_e2e_verify;"

# 2. Backend with CI's Testing env (from backend/PremierLeaguePredictions.API)
ASPNETCORE_ENVIRONMENT=Testing ASPNETCORE_URLS=http://localhost:5154 \
ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=plp_e2e_verify;Username=postgres;Password=dDSqDJ8gtb" \
JwtSettings__Secret=test-jwt-secret-key-minimum-32-characters-long-for-hs256-testing \
JwtSettings__Issuer=PremierLeaguePredictions JwtSettings__Audience=PremierLeaguePredictions \
JwtSettings__ExpirationInMinutes=1440 DisableAuthorizationInDevelopment=false RunMigrationsOnStartup=true \
dotnet run --configuration Debug

# 3. Frontend E2E (from frontend/) — reuses a running vite on :5173, which proxies /api → :5154
VITE_ENABLE_DEV_LOGIN=true VITE_USE_MOCK_API=false npx playwright test --project=chromium

# 4. Cleanup: kill backend on :5154, then DROP DATABASE plp_e2e_verify
```

## Gotchas to remember

- **Local dev DB** (`premier_league_predictions`) currently has `2026-2027` **active** and
  `2025-2026` inactive. Because a real active season exists locally, the E2E seeder keeps
  `E2E-TEST` **inactive** there — so E2E against the dev DB will NOT mirror CI. Always use a
  fresh empty DB (recipe above) to reproduce CI.
- Backend build MSB file-locks happen when the dev backend / Visual Studio is running (locks DLLs).
  Stop them before a full `dotnet build`/`dotnet test`.
- Season names must NOT contain `/` (breaks `seasonId`-in-URL routing); use hyphen e.g. `2026-2027`.
- Secrets: `appsettings.Development.json` is gitignored with real dev secrets — never commit it.

## PR title & description (ready to use)

**Title:** `Scope views to the active season, fix E2E pick flow, and polish season UX`

**Description:** see the full markdown block generated in the prior turn (Summary / Changes /
Testing). Key sections: active-season scoping, relegation-promotion sync, E2E seeder fix,
season admin UX, mobile/UI polish, and the local CI-replication test results (36 E2E, 87 unit,
lint/format/typecheck/build all green).
