export const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000';
export const GOOGLE_CLIENT_ID = import.meta.env.VITE_GOOGLE_CLIENT_ID || '';

export const API_ENDPOINTS = {
  // Auth
  LOGIN: '/api/auth/login',
  REGISTER: '/api/auth/register',
  REFRESH: '/api/auth/refresh',

  // Users
  USERS: '/api/users',
  USER_BY_ID: (id: string) => `/api/users/${id}`,
  DEACTIVATE_USER: (id: string) => `/api/users/${id}/deactivate`,
  MARK_AS_PAID: (id: string) => `/api/users/${id}/mark-paid`,

  // Seasons
  SEASONS: '/api/seasons',
  ACTIVE_SEASON: '/api/seasons/active',
  SEASON_BY_ID: (id: string) => `/api/seasons/${id}`,
  ARCHIVE_SEASON: (id: string) => `/api/seasons/${id}/archive`,

  // Gameweeks
  GAMEWEEKS: '/api/gameweeks',
  GAMEWEEK_BY_ID: (id: string) => `/api/gameweeks/${id}`,
  CURRENT_GAMEWEEK: '/api/gameweeks/current',

  // Fixtures
  FIXTURES: '/api/fixtures',
  FIXTURES_BY_GAMEWEEK: (gameweekId: string) => `/api/fixtures/gameweek/${gameweekId}`,

  // Picks
  PICKS: '/api/picks',
  PICKS_BY_USER: (userId: string) => `/api/picks/user/${userId}`,
  PICKS_BY_GAMEWEEK: (gameweekId: string) => `/api/picks/gameweek/${gameweekId}`,
  CREATE_PICK: '/api/picks',
  UPDATE_PICK: (id: string) => `/api/picks/${id}`,

  // Teams
  TEAMS: '/api/teams',
  AVAILABLE_TEAMS: (userId: string, gameweekNumber: number) =>
    `/api/teams/available?userId=${userId}&gameweekNumber=${gameweekNumber}`,

  // League
  LEAGUE_STANDINGS: '/api/league/standings',

  // Dashboard
  DASHBOARD: '/api/dashboard',

  // Admin
  OVERRIDE_PICK: '/api/admin/override-pick',
  OVERRIDE_DEADLINE: '/api/admin/override-deadline',
  ADMIN_ACTIONS: '/api/admin/actions',
} as const;

export const STORAGE_KEYS = {
  AUTH_TOKEN: 'auth_token',
  USER: 'user',
} as const;

export const QUERY_KEYS = {
  USER: 'user',
  USERS: 'users',
  SEASONS: 'seasons',
  ACTIVE_SEASON: 'active_season',
  GAMEWEEKS: 'gameweeks',
  CURRENT_GAMEWEEK: 'current_gameweek',
  FIXTURES: 'fixtures',
  PICKS: 'picks',
  TEAMS: 'teams',
  AVAILABLE_TEAMS: 'available_teams',
  LEAGUE_STANDINGS: 'league_standings',
  DASHBOARD: 'dashboard',
  ADMIN_ACTIONS: 'admin_actions',
} as const;
