# Session Handoff — 2026-08-27

Status note for resuming fresh. Delete or `.gitignore` this file when no longer needed.

---

## TL;DR

The iPhone login failure is fixed — dev now lives on `dev.eplpredict.com` + `api-dev.eplpredict.com`,
so its auth cookie is first-party and Safari stops dropping it. Since then: a player profile page, an
eliminations page, both themes restyled (Nebula Night dark, Light Ocean light), and a run of defects
found along the way — a leaked picks endpoint, dead links in every email, standings that went stale
after admin edits, an override that never rescored, and an elimination rule that disagreed with the
documented one.

`develop` is **18 commits ahead of `main`** and unmerged, with **substantial uncommitted work on top**
(eliminations feature, light theme, surface textures, contrast fixes). Working tree is *not* clean.

**Next actions:** commit the outstanding work, set `AppBaseUrl` on both Render services, open a PR
`develop → main`, then watch a full gameweek on prod.

---

## Git state

- Branch `develop`, **18 commits ahead** of `origin/main`. Latest three are from this session:
  - `0a04b71` Add player profiles, restyle dark theme, fix email and cache bugs
  - `2734d60` Draw the form guide as W/D/L tiles and vary the standings columns
  - `5995dd5` Choose auth cookie flags by hostname, not environment name
- **Uncommitted** (see "Not yet committed" below) — roughly 19 modified, 6 new files.
- Branch strategy: `feature/* → develop → main` (PRs, tests must pass; `main` auto-deploys).
- **Do not commit/push without explicit instruction** (standing user rule).

---

## Environment change: dev moved to eplpredict.com

**This is the fix for "the dev site won't get past the login screen on an iPhone."**

Auth is an httpOnly cookie set by the API. Dev's UI was on `vercel.app` and its API on
`onrender.com` — different registrable domains, so that cookie was **third-party**, and every iOS
browser (all WebKit) blocks third-party cookie writes by default. Login succeeded, the cookie was
discarded, `/users/me` 401'd, and the axios interceptor bounced back to `/login`.

| | UI | API |
|---|---|---|
| prod | `eplpredict.com` | `api.eplpredict.com` |
| dev | **`dev.eplpredict.com`** | **`api-dev.eplpredict.com`** |

- DNS for `eplpredict.com` is hosted **at Vercel** (`ns1/ns2.vercel-dns.com`) — records are edited
  under Vercel's account-level **Domains** tab, not a project's.
- `premierleague-api-dev.onrender.com` still resolves and is still what `ApiBaseUrl`, the GitHub
  Actions waker and the cron-job.org jobs call. Those are server-to-server, so the domain is
  irrelevant to them; leaving them alone avoids regenerating every cron job.
- The domain move alone fixed iOS. A first-party cookie is allowed regardless of its `SameSite`
  value.

`5995dd5` then made the cookie flags follow the **request host** rather than `ASPNETCORE_ENVIRONMENT`,
so an API served from `Auth:CookieDomain` issues `SameSite=Lax` and anything else keeps
`SameSite=None`. The cookie is **host-only everywhere** — deliberately no `Domain` attribute, because
scoping it to `.eplpredict.com` would put dev and prod sessions in one browser cookie and signing
into dev would end the prod session.

---

## What changed this session

### Player profiles (committed)

A name in the league standings links to `/users/:id`: avatar, season record, revealed picks, teams
used and still available, and a head-to-head against the viewer.

**One gate governs all of it — a gameweek's deadline must have passed** — and it covers *inference*
as much as disclosure. Team usage and head-to-head are built from revealed picks only, because a
team missing from the "still available" list gives away an unrevealed pick as plainly as naming it.
`UserProfileServiceTests` pins that.

**Closed a live leak while doing it:** `GET /api/v1/picks/gameweek/{seasonId}/{n}` returned the whole
field's picks for *any* gameweek to any signed-in user. Nothing in the UI called it, but it was
reachable. The gate now sits in `PickService`, not the controller, so no caller can route around it.

### Emails link at the site again (committed)

Approval and pick-reminder emails shipped a hardcoded `https://your-app-url.com`, so the button did
nothing for everyone who ever clicked it. There was no site URL configured anywhere — only
`ApiBaseUrl`, which is the API host.

New **`AppBaseUrl`** setting, deliberately undefaulted for the same reason `ApiBaseUrl` is. Unset
means the button is **omitted with a logged warning** rather than sending a dead link or failing the
mail a player needs. **It is not yet set on either Render service** — see Outstanding.

### Standings stopped going stale (committed)

Standings are cached for five minutes and only the score sync dropped that cache. An admin
backfilled a pick, looked at the table, and saw nothing — refreshing could not help, because the
staleness was server-side. `BackfillPicksAsync`, `OverridePickAsync`,
`RecalculatePointsForGameweekAsync` and `ProcessGameweekEliminationsAsync` all invalidate now.

Also found: **`OverridePickAsync` changed a pick's team but never rescored it**, so an admin
correction showed the new crest with the old team's points until a recalculation happened to run.

### Eliminations (not committed)

New player-facing `/eliminations` page and `GET /api/v1/eliminations` (all existing elimination
endpoints are admin-only and carry an admin trail players should not see).

- **Danger zone** — the configured count for the next unprocessed gameweek, naming exactly those
  players, with how far behind safety each is, and whether picks can still change it.
- **Out of the competition** — grouped by the gameweek each went out in.

**The elimination metric changed.** `CLAUDE.md` said "lowest average points per game"; the code
sorted on **total points**. Per your decision the code was fixed, not the docs. "Per game" means
points ÷ gameweeks whose fixture has a score — the same count the standings call picks made — so
joining late is not scored as a run of nil results.

Two holes fell out of testing it:

- **A player with no picks at all could never be eliminated** — the ranking was grouped from picks,
  so someone who never picked was not in the list. It now ranks over approved participants.
- **Ties were broken by user ID**, i.e. a GUID coin-flip deciding who leaves.

**Server-side guard added:** `PickService` now refuses create/update/delete from an eliminated
player. The UI already hid picking; the endpoint did not.

The dashboard banner no longer claims an eliminated player can view the standings (they are filtered
out of it). It points at their profile and the eliminations page instead.

### Themes (dark committed, light not)

- **Nebula Night (dark).** Surfaces step `#080414` → `#11112B` → `#1E1B4B`, violet accent, with two
  wide radial pools bled in from the top corners for the glow.
- **Light Ocean (light).** `#E8F2FF` page → white cards → `#3276C8` accent, with a gradient running
  blue at the top to white at the bottom. Surfaces run *opposite* to dark: the page is tinted and
  cards are pure white, so a card lifts by being lighter than its surroundings.
- **Surface texture.** `.card-surface` (grain + wash + edge highlight) and `.btn-surface` (grain +
  top-lit gradient + lip). Both drop out under `prefers-contrast: more`.
- **Contrast fixes.** Several fills failed WCAG AA for their label text and are corrected — see the
  table under Gotchas.

---

## Not yet committed

| Area | Files |
|---|---|
| Eliminations page + endpoint | `EliminationsController.cs`, `EliminationDTOs.cs`, `IEliminationService.cs`, `EliminationService.cs`, `EliminationsPage.tsx(+test)`, `services/eliminations.ts`, `App.tsx`, `Layout.tsx` |
| Elimination metric + tiebreaks | `EliminationService.cs`, `LeagueService.cs`, `EliminationRankingTests.cs` |
| Eliminated-player pick guard | `PickService.cs`, `EliminatedPickGuardTests.cs` |
| Light Ocean + textures + contrast | `index.css`, `card.tsx`, `button.tsx`, `FormBadge.tsx`, `PickCrest.tsx`, `LoginPage.tsx`, `Layout.tsx`, three `pages/admin/*.tsx` |
| Theme-flip fix | `Layout.tsx` |
| Elimination rule documented | `CLAUDE.md` |

All green at handoff: **119 backend tests, 109 frontend tests**, lint, `tsc`, Prettier.

---

## Outstanding

1. **Commit the work above**, then **open PR `develop → main`.** The workflow half of the older
   changes still has no effect until merged — `repository_dispatch` always runs the default branch's
   workflow file.
2. **Set `AppBaseUrl` on both Render services.** Until then approval and reminder emails go out with
   no link at all, and it fails silently apart from a logged warning.
   - `premierleague-api` → `https://eplpredict.com`
   - `premierleague-api-dev` → `https://dev.eplpredict.com`
3. **Verify a full gameweek on prod**, including completion and eliminations.
4. **Rotate two credentials** (both still outstanding):
   - `ExternalSync__ApiKey` (`sk_live_9fA7Qx…`) — exposed in cron-job.org screenshots. Re-run
     generate afterwards so sync jobs pick up the new key.
   - `GitHub__Token` — the PAT was logged in plaintext to Render on 2026-08-21.
5. **If prod has any eliminations already recorded, they were decided on total points**, not average.
   Nothing to recompute on dev (none exist there).
6. **Delete `AdminDiagnosticsController`** — still present; it existed to prove the SMTP block.
7. **Raise cron-job.org `RequestTimeout`** (30s) above Render's cold start (~24s).
8. **`bg-green-600` still fails contrast anywhere it is not a button** — the three admin confirm
   buttons and the W/L tiles are fixed, but a sweep of remaining green/red fills would be worth it.
9. **Watch Brevo's 300/day cap.** ~165 on a peak day at 275 players, worst case 275.
10. **`DeleteExistingEplJobsAsync` wipes before creating**, so a mid-run failure leaves the week
    unscheduled.
11. **Split the standings cache into settled and live halves** — see the section at the end. Measure
    on prod-shaped data first.

---

## Gotchas to remember

### New this session

- **Theme preference is stored server-side per user and overrides `localStorage`.** `Layout` treats
  `user.themePreference` as the truth and re-applies it *on every mount*. The toggle must feed the
  saved user back through `updateUser` — leaving it stale made the next page flip the theme back.
  It only showed when clicking **Admin**, because sibling routes reuse the same `Layout` instance
  while the admin route nests `AdminLayout` inside it, forcing a remount.
  Consequence for screenshots/testing: setting `localStorage.theme` does **not** work. Rewrite
  `themePreference` on the `/users/me` *and* `/dev/login-as-admin` responses instead — both carry it.
- **Elimination ranking chain**, now shared by the run, the danger zone and the standings sort:
  average → total points → games played → goal difference → goals for → user id (stability only).
  The standings previously had *no* final tiebreak, so positions could shuffle between requests and
  whoever landed last looked bottom of the table without being bottom of anything.
- **Internal `Team.Id` is not the football-data crest id.** Arsenal is `221` internally; `57` is what
  appears in `crests.football-data.org/57.png`. Passing a crest id to an admin endpoint silently
  fails or 400s.
- **Blend modes have to match the tonal range**, which is why the three surfaces differ:
  `multiply` for grain on near-white cards, `screen` on near-black cards, `soft-light` on saturated
  button fills. Using one everywhere leaves the grain invisible on two of the three.
- **`dateStyle`/`timeStyle` cannot be combined with `timeZoneName`** in `toLocaleString` — it throws
  rather than degrading, which crashed the whole eliminations page. Use component options.
- **Contrast corrections applied** (white label text unless stated):

  | Fill | Was | Now |
  |---|---|---|
  | `--primary` dark | `258 90% 66%` — 4.31:1 | `257 58% 50%` — 7.35:1 |
  | Admin green buttons | `green-600` — 3.30:1 | `green-700` — 5.02:1 |
  | Win tile dark | white on `green-500` — 2.28:1 | near-black on `green-500` — 8.69:1 |
  | Win tile light | white on `green-600` — 3.30:1 | white on `green-700` — 5.02:1 |
  | Loss tile dark | white on `red-500` — 3.76:1 | near-black on `red-500` — 5.26:1 |

  The W/D/L letters are 11px bold — *normal* text for WCAG, so the 3:1 large-text threshold does not
  apply, which is why white-on-green looked passable but was not.
- **Dev has 15 seeded GW2 picks** (Test Users 1–15, each different from their GW1 pick). GW2's
  deadline is 2026-08-28 18:00 UTC, so until then they are correctly invisible everywhere — that is
  the reveal gate working, not a bug.

### Carried over

- **Standings are cached server-side for 5 minutes**, now evicted by the score sync *and* by admin
  backfill/override/recalculate/elimination runs. Direct database edits still take up to five
  minutes, on top of React Query's 5-minute `staleTime`.
- **The league table is push-driven, not polled.** `useResultsUpdates` (mounted in `Layout`)
  invalidates `['league-standings']` on the SignalR `ResultsUpdated` event. Do not reintroduce
  `refetchInterval`.
- **Two sync runs per cycle during a busy Saturday is expected** — cron-job.org schedules are a
  cross-product of hours × minutes, so overlapping match windows fire on the same minutes.
- **Reminders only fire within 30 minutes of 24h or 3h before a deadline.** Use
  `POST /api/v1/admin/email/test` to test on demand (accepts a `"to"` override in Development only).
- **A 2xx from Brevo means accepted, not delivered.** Match the `messageId` against Brevo's log.
- **cron-job.org's API quota is undocumented and unforgiving** — exhausting it locked out generate
  for over eight hours. The opening `GET /jobs` deliberately does not retry a 429.
- **`repository_dispatch` runs the workflow file from the default branch**, never the branch you
  edited.
- **Points recalculation only runs when a fixture changes.** A scoring-logic change is not
  retroactive; `POST /api/v1/admin/gameweeks/{seasonId}/{gw}/recalculate` forces it.
- Season names must NOT contain `/`; use `2026-2027`.
- `appsettings.Development.json` is gitignored with real dev secrets — never commit it.

---

## Verified vs not

**Verified this session:** iOS login on the new domain; the profile endpoint's reveal gate (payload
contains no reference to an unrevealed gameweek); the picks-leak fix live (open gameweek → 0 picks,
settled → 21); backfill appearing in the standings immediately; the eliminations endpoint and page;
the theme flip reproduced *and* fixed; both themes and textures inspected at 3× crop.

**Still not verified:**

- **No full gameweek has run on prod.**
- **Gameweek completion has never fired.** Watch for `GW1 completed and locked`, or the
  `not complete — N of M fixtures unsettled` warning.
- **No elimination has ever been processed**, so the new metric, the danger zone and the eliminated
  list have never been seen with real data.
- **Reminders have not been sent for real**, and no email has yet gone out *with* a working link
  (needs `AppBaseUrl`).

---

## Required environment variables

Both Render services need these, or startup throws:

| Key | dev | prod |
|---|---|---|
| `ApiBaseUrl` | `https://premierleague-api-dev.onrender.com` | `https://api.eplpredict.com` |
| `AppBaseUrl` | `https://dev.eplpredict.com` | `https://eplpredict.com` |
| `Auth__CookieDomain` | `eplpredict.com` | `eplpredict.com` |
| `GitHub__Environment` | `dev` | `prod` |
| `Email__FromEmail` | a sender verified in Brevo | a sender verified in Brevo |
| `Email__Brevo__ApiKey` | `xkeysib-…` | `xkeysib-…` |
| `Email__FromName` | `Premier League Predictions Dev` | `Premier League Predictions` |
| `AllowedOrigins` | must include `https://dev.eplpredict.com` | `https://eplpredict.com` |

No trailing slash on `ApiBaseUrl` — it is concatenated directly with `/api/v1/admin/sync/results`.
`AppBaseUrl` is the **site**, not the API host. `Auth__CookieDomain` accepts a leading dot or not.

---

## Running locally

The local `appsettings.Development.json` currently points at the **dev Supabase database** through
the pooler (`aws-0-us-west-2.pooler.supabase.com:5432`, user `postgres.dyxzfuxtslbzvzaihfjv`), with
`RunMigrationsOnStartup: false`. Keep migrations off while pointed there — the deployed dev API
shares that database.

```bash
# backend
cd backend
ASPNETCORE_ENVIRONMENT=Development dotnet run --project PremierLeaguePredictions.API \
  --no-launch-profile --urls http://localhost:5154

# frontend (.env.local already points at http://localhost:5154)
cd frontend && npm run dev -- --strictPort
```

Then `http://localhost:5173` → **Login as Admin (Dev)**.

Two connection-string traps, both hit this session:

- **Npgsql cannot parse a `postgresql://` URI.** Supabase's dashboard shows one; Npgsql needs
  `Host=…;Port=…;Database=…;Username=…;Password=…`. Copy the value from Render's
  `ConnectionStrings__DefaultConnection` instead, which is already in the right form.
- The **pooler** host uses username `postgres.<projectref>`; the direct `db.<ref>.supabase.co` host
  uses plain `postgres`. Mixing them fails authentication.

Pointing the local **frontend** at the deployed dev API no longer works well: `localhost` is
cross-site with `api-dev.eplpredict.com`, so once dev issues `SameSite=Lax` cookies the browser stops
sending them. Run the API locally instead.

---

## Follow-up: split the standings cache into settled and live halves

`GetStandingsDataAsync` recomputes the **whole season** on every cache miss — one SQL statement with
eight correlated subqueries per user, plus `AttachFormAsync` reading every pick in the season. Now
that a goal evicts the cache, a single goal in GW20 recomputes nineteen gameweeks that cannot have
changed.

Suggested shape:

- **Historical totals per user** cached under a key that includes the last completed gameweek, e.g.
  `standings_2026-2027_gw19`. The key changes by itself when a gameweek completes, so there is no
  invalidation logic to get wrong.
- **Current gameweek delta per user** computed fresh per request. Bounded and cheap.
- Sum the two. The form guide is *entirely* historical, so it can be cached for the week.

**Measure before building.** Dev has 22 users and one completed gameweek, which will not show the
problem. A scaling assumption here was wrong once already because it was reasoned from the schema
rather than measured.

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
dotnet test --filter "FullyQualifiedName!~Integration"   # integration tests need Postgres on :5433
```
