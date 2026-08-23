# Premier League Predictions - CLAUDE.md

## Project Overview

A points-based Premier League prediction game. Each week users pick a Premier League team; they earn **3 points for a correct pick (win), 1 for a draw, 0 for a loss**. The goal is to accumulate the most points over the season. Pick rules and elimination rules are configured per-season in the admin screen.

**Target timeline:** Testing through end of 2025/26 season, go live for 2026/27 season (starts August 2026).

---

## Pick Rules (Current Season Config)

Pick rules are split into two halves of the season (configured in admin):

- **Weeks 1–20 (First Half):** Each team must be picked exactly once. The same opponent (team your pick is playing against) can be targeted unlimited times.
- **Weeks 21–38 (Second Half):** 18 teams must be picked once. The same opponent can only be targeted a maximum of 4 times.

The opponent is determined from the fixture — users pick a team, and the system knows who that team is facing from the fixture data.

## Eliminations

Configurable per season in admin. Each week, X players with the lowest **average points per game** at the end of that gameweek are eliminated.

---

## Tech Stack

**Backend:** .NET 10, ASP.NET Core Web API, Entity Framework Core 10, PostgreSQL (Supabase), Serilog, FluentValidation, AutoMapper, SignalR, JWT + Google OAuth

**Frontend:** React 19, TypeScript, Vite, TanStack React Query, React Router v7, Tailwind CSS, shadcn/ui, Axios, SignalR client

**Deployment:** Render.com (free tier — app spins down on inactivity), Supabase (PostgreSQL), cron-job.org (external cron scheduling), GitHub Actions (CI/CD)

**External APIs:** football-data.org (free tier — be mindful of rate limits), Brevo (transactional email over HTTPS)

---

## Architecture

Clean Architecture with four layers:

```
Core/           → Domain entities (User, Team, Season, Gameweek, Fixture, Pick, etc.)
Application/    → Business logic, services, DTOs, interfaces, validators
Infrastructure/ → EF Core, repositories, external API clients (FootballData, cron-job.org, Google)
API/            → Controllers, middleware, auth, filters
```

**Key patterns:**
- Unit of Work + generic `IRepository<T>` for all data access
- All API responses wrapped in `ApiResponse<T>`
- `ValidationFilter<T>` with FluentValidation on controllers
- AutoMapper for entity → DTO mapping
- JWT Bearer + API Key auth ("SmartScheme" tries API key first, falls back to JWT)
- Granular admin authorization policies: `AdminOnly`, `DataModification`, `CriticalOperations`, `ExternalSync`

---

## Live Score Flow (Current Architecture)

```
cron-job.org sync-scores job (every 2 min during match windows)
  → POST /api/v1/admin/sync/results  (API Key auth, key sent as an X-API-Key header)
  → ResultsService fetches from football-data.org
  → Results saved to DB
  → SignalR pushes updates to connected frontend clients
```

**Resolved 2026-08-22 — live scores never updated because the sync call never authenticated.** `ApiKeyAuthenticationHandler` accepts the key from an `X-API-Key` header *or* an `apiKey` query parameter, but `SmartScheme`'s `ForwardDefaultSelector` in `Program.cs` only routed to the ApiKey scheme on the header. Query-string calls fell through to JWT Bearer, found no token, and 401'd before the handler ran — the query-string branch was unreachable. The selector now matches either form, and the generated sync job sends a header. Verified end to end in dev: auth → poll → detect change → persist → recalculate points → SignalR push. **Not yet verified across a full gameweek on prod.**

**Note on scheduling:** Scheduling runs on **cron-job.org** (an external cron service), not GitHub Actions. A weekly master job (Mondays 09:00 Europe/London) POSTs to `/api/v1/admin/schedule/generate`, which returns **202 immediately** and runs in the background — calls to cron-job.org are paced to stay under its rate limit, so a generate takes minutes and cannot be held open on an HTTP request. Poll `GET /api/v1/admin/schedule/generate/status` for progress and the outcome; a second concurrent start is refused with 409. `CronSchedulerService` builds a plan from gameweeks whose deadline falls within 7 days either side of now, then `CronJobsOrgService` creates one cron-job.org job per (reminders / auto-pick / sync-scores) schedule. Generated jobs are titled `EPL-{ENV}-{season}-GW{n}-{jobType}-{i}` (e.g. `EPL-DEV-2026-2027-GW1-sync-scores-1`); each run deletes existing jobs matching **its own environment prefix** only, so dev and prod can share an account (the master and warm-up jobs must NOT use an `EPL-` prefix). Off-season with no upcoming fixtures correctly produces zero jobs. cron-job.org has a hard 30s request timeout and Render free tier cold-starts (~24s), so a warm-up ping to `/health` runs a few minutes before the Monday generate. GitHub Actions is now CI/CD only. See `LIVE_SCORES_SETUP.md` for the full flow.

---

## Commands

**Backend tests:**
```bash
cd backend
dotnet test
```

**Frontend tests:**
```bash
cd frontend
npm run test   # Vitest
```

**E2E tests:**
```bash
# Playwright (tests may not be written yet — confirm before running)
npx playwright test
```

**Run backend locally:**
```bash
cd backend
dotnet run --project PremierLeaguePredictions.API
```

**Run frontend locally:**
```bash
cd frontend
npm run dev
```

**EF Core migrations:**
```bash
cd backend
dotnet ef database update --project PremierLeaguePredictions.Infrastructure --startup-project PremierLeaguePredictions.API
```

---

## Branch & CI/CD Strategy

```
feature/* → develop  (PR, tests must pass)
develop   → main     (PR, tests must pass → auto-deploy to Render)
```

- `develop` branch exists
- CI/CD pipeline (GitHub Actions for test runs on PRs) may not be fully set up yet — check before assuming it exists
- Playwright E2E tests may not be written yet — confirm before referencing them

---

## Deployment

- **API:** Docker container on Render.com free tier (`/health` health check endpoint). Spins down after inactivity.
- **Database:** Supabase PostgreSQL. Migrations run automatically on startup (`RunMigrationsOnStartup: true`).
- **Frontend:** Static site on Render.
- **Config:** `appsettings.Development.json` for local dev (not committed). Environment variables synced in Render.

---

## Key Files

| Purpose | Path |
|---|---|
| API entry point | `backend/PremierLeaguePredictions.API/Program.cs` |
| Cron schedule generation | `backend/PremierLeaguePredictions.Application/Services/CronSchedulerService.cs` |
| cron-job.org integration (create/delete jobs) | `backend/PremierLeaguePredictions.Infrastructure/Services/CronJobsOrgService.cs` |
| cron-job.org REST API client | `backend/PremierLeaguePredictions.Infrastructure/Services/CronJobsOrgClient.cs` |
| Schedule controller (generate/reminders/auto-pick) | `backend/PremierLeaguePredictions.API/Controllers/Admin/AdminScheduleController.cs` |
| EF Core context | `backend/PremierLeaguePredictions.Infrastructure/Data/ApplicationDbContext.cs` |
| Auth policies | `backend/PremierLeaguePredictions.API/Authorization/AdminPolicies.cs` |
| API container build (prod / dev) | `backend/Dockerfile` / `backend/Dockerfile.dev` |
| Frontend routes | `frontend/App.tsx` |
| Auth context | `frontend/src/contexts/` |

---

## Frontend Architecture Rules (React UI)

### Folder Structure

```
src/
├── components/
│   ├── ui/          # shadcn/ui primitives only — no business logic
│   ├── layout/      # Layout wrappers (Layout.tsx, AdminLayout.tsx)
│   └── {feature}/   # Feature components (dashboard/, fixtures/, league/, etc.)
├── pages/
│   ├── admin/       # Admin-only pages
│   └── *Page.tsx    # Top-level route components
├── contexts/        # Global state providers (Auth, Theme, SignalR)
├── hooks/           # Custom React hooks
├── services/        # API service layer — one file per feature domain
├── types/           # All TypeScript types in index.ts
├── mocks/handlers/  # MSW mock handlers per feature
├── lib/             # queryClient, utils (cn), sentry
└── config/          # constants.ts (API_URL, query keys, etc.)
```

### Naming Conventions

| Thing | Convention | Example |
|---|---|---|
| Components | PascalCase | `LeagueStandings.tsx` |
| Pages | PascalCase + `Page` suffix | `DashboardPage.tsx` |
| Contexts | PascalCase + `Context` suffix | `AuthContext.tsx` |
| Hooks | camelCase + `use` prefix | `useResultsUpdates.ts` |
| Services | lowercase | `picks.ts`, `admin.ts` |
| Mock services | lowercase + `.mock` | `picks.mock.ts` |
| Test files | `*.test.tsx` / `*.spec.ts` | `Picks.test.tsx` |

### Import Rules

- Always use the `@/` path alias — no relative `../` imports
- Types imported as `import type { X } from '@/types'`
- UI primitives: `import { Button } from '@/components/ui/button'`
- Barrel files exist for types (`@/types`) and mock handlers — not for components

### State Management

**Two distinct layers — do not mix them:**

1. **Context API** — global app state that outlives queries:
   - `AuthContext`: user identity, token, `isAdmin` flag
   - `ThemeContext`: light/dark mode (persists to localStorage)
   - `SignalRContext`: hub connection, event subscribe/unsubscribe

2. **React Query** — all async server state:
   - `useQuery` for reads, `useMutation` for writes
   - Query keys defined as constants in `config/constants.ts` — use kebab-case strings: `['active-season']`, `['pick-rules', seasonName]`
   - Default config: 5-min staleTime, no refetch on window focus, 1 retry
   - On mutation error: global toast (except 409 Conflict — handle that locally)

**Do not use `useState` for server data** — always React Query.

### Service Layer Pattern

Each feature domain has a service file that exports a single service object:

```typescript
// Toggle via VITE_USE_MOCK_API=true env var
const USE_MOCK_API = import.meta.env.VITE_USE_MOCK_API === 'true';
export const picksService = USE_MOCK_API ? mockPicksService : realPicksService;
```

- Services return raw promises — React Query wraps them in hooks
- The real service uses the shared `apiClient` (Axios instance at `@/services/api`)
- The mock service lives in `services/picks.mock.ts` and mirrors the same interface
- All API calls go through `apiClient` — do not use `fetch` or create new Axios instances

### Component Rules

- Functional components only — no class components
- Props interfaces defined in the same file as the component
- Business logic lives in hooks or services — keep components presentational where possible
- Feature components go in `components/{feature}/` — not in `pages/`
- Pages are thin: they compose feature components and wire up React Query hooks

### Routing & Auth Guards

Three route guard components — use the right one:

| Guard | Use when |
|---|---|
| `ProtectedRoute` | Any authenticated-only route |
| `ApprovalCheckRoute` | Routes that also require season approval |
| `AdminRoute` | Admin-only routes |

Do not add auth checks inside components — use the route guards in `App.tsx`.

### SignalR

- All SignalR state and subscriptions managed through `SignalRContext`
- Use the dedicated hooks: `useResultsUpdates()`, `useAutoPickNotifications()`, etc.
- When a SignalR event arrives, invalidate the relevant React Query keys — do not manually update query cache
- Do not subscribe directly to the hub connection outside of `SignalRContext`

### UI Components (shadcn/ui)

- `components/ui/` is for primitive shadcn components only — no business logic in there
- Use the `cn()` utility (`@/lib/utils`) for conditional Tailwind classes
- Component variants use CVA (`class-variance-authority`)
- Dark mode via Tailwind `dark:` prefix — theme toggled by adding `dark` class to `document.documentElement`

### Forms

- Use React Hook Form + Zod for any non-trivial form
- For simple single-field admin controls, `useState` is acceptable
- Validation errors from the server are shown via toast notifications

### Testing

- Unit/component tests: Vitest + React Testing Library
- Mocked API: MSW handlers in `src/mocks/handlers/{feature}.handlers.ts`
- Custom render utility at `src/test/test-utils.tsx` — use this, not RTL's `render` directly
- E2E: Playwright (may not be written yet — confirm before referencing)
- Enable mock API locally: `VITE_USE_MOCK_API=true`

---

## Important Constraints

- **football-data.org free tier** — has rate limits. Don't add unnecessary calls. The score sync only polls fixtures that can still change: settled ones (FINISHED / AWARDED / CANCELLED) and those more than 15 minutes from kickoff are skipped, so a live gameweek costs ~1 call per cycle rather than 10.
- **Render free tier** — app spins down. No persistent background workers possible.
- **Render free tier blocks outbound SMTP.** Ports 587, 465 and 25 are silently dropped (probed from inside the container: all three timed out at 5s while 443 connected in 14ms). Email must leave over HTTPS — hence Brevo's REST API. `SmtpEmailService` still exists and is selectable with `Email:Provider=Smtp`, but only works locally.
- **Brevo free tier — 300 emails/day** is the binding limit, not the 9,000/month. At ~275 players a peak reminder day is ~165, worst case 275. Growth past ~300 players, or a deadline in the small hours (which would put both reminder windows on one calendar day), would breach it.
- **cron-job.org API has an undocumented quota.** Exhausting it locks out generate for hours — the opening `GET /jobs` deliberately does not retry a 429, because each retry spends more of the allowance.
- **GitHub Actions free tier** — ~2,000 min/month. Current usage is ~45 min/month, well within limits.
- **Do not manually edit migration files** — always generate via `dotnet ef migrations add`.
- **Supabase has Row-Level Security (RLS)** set up — see `/database/enable_rls.sql`. Be careful with direct DB operations.
