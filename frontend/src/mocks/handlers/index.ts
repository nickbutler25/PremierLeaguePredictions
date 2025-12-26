import { authHandlers } from './auth.handlers';
import { dashboardHandlers } from './dashboard.handlers';
import { picksHandlers } from './picks.handlers';
import { teamsHandlers } from './teams.handlers';
import { leagueHandlers } from './league.handlers';
import { fixturesHandlers } from './fixtures.handlers';
import { gameweeksHandlers } from './gameweeks.handlers';
import { adminHandlers } from './admin.handlers';

/**
 * MSW Request Handlers
 *
 * Combine all API mock handlers here.
 * These handlers intercept network requests and return mock responses.
 */

export const handlers = [
  ...authHandlers,
  ...dashboardHandlers,
  ...picksHandlers,
  ...teamsHandlers,
  ...leagueHandlers,
  ...fixturesHandlers,
  ...gameweeksHandlers,
  ...adminHandlers,
];

// Export individual handler arrays for selective mocking
export {
  authHandlers,
  dashboardHandlers,
  picksHandlers,
  teamsHandlers,
  leagueHandlers,
  fixturesHandlers,
  gameweeksHandlers,
  adminHandlers,
};
