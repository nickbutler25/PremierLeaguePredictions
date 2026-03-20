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

**Deployment:** Render.com (free tier — app spins down on inactivity), Supabase (PostgreSQL), GitHub Actions (cron scheduling)

**External APIs:** football-data.org (free tier — be mindful of rate limits)

---

## Architecture

Clean Architecture with four layers:

```
Core/           → Domain entities (User, Team, Season, Gameweek, Fixture, Pick, etc.)
Application/    → Business logic, services, DTOs, interfaces, validators
Infrastructure/ → EF Core, repositories, external API clients (FootballData, GitHub, Google)
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
GitHub Actions cron (every 2 min during match windows)
  → POST /api/v1/dev/fixtures/sync-results  (API Key auth)
  → FootballDataService fetches from football-data.org
  → Results saved to DB
  → SignalR pushes updates to connected frontend clients
```

**Known issue:** Live scores do not always update correctly on the frontend during games. Root cause is unknown — could be SignalR connection, the sync job, the football-data.org polling, or the frontend receiving but not rendering updates. This needs investigation.

**Note on scheduling:** The GitHub Actions approach (master-scheduler generates a weekly YAML, committed to the repo) is functional but not ideal. The user is open to alternatives. Hangfire is not viable because Render free tier spins the app down — background jobs would die. Any alternative needs to work with a service that may be cold-started.

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
| GitHub workflow generation | `backend/PremierLeaguePredictions.Infrastructure/Services/GitHubWorkflowService.cs` |
| EF Core context | `backend/PremierLeaguePredictions.Infrastructure/Data/ApplicationDbContext.cs` |
| Auth policies | `backend/PremierLeaguePredictions.API/Authorization/AdminPolicies.cs` |
| Master scheduler workflow | `.github/workflows/master-scheduler.yml` |
| Render deploy config | `render.yaml` |
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

- **football-data.org free tier** — has rate limits. Don't add unnecessary calls.
- **Render free tier** — app spins down. No persistent background workers possible.
- **GitHub Actions free tier** — ~2,000 min/month. Current usage is ~45 min/month, well within limits.
- **Do not manually edit migration files** — always generate via `dotnet ef migrations add`.
- **Supabase has Row-Level Security (RLS)** set up — see `/database/enable_rls.sql`. Be careful with direct DB operations.
