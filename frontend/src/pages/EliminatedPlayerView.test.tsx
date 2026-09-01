import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { DashboardPage } from './DashboardPage';
import { UserProfilePage } from './UserProfilePage';
import { render, createMockUser } from '@/test/test-utils';
import { dashboardService } from '@/services/dashboard';
import { leagueService } from '@/services/league';
import { usersService } from '@/services/users';
import type { LeagueStandingsData, StandingEntry, UserProfile } from '@/types';

vi.mock('@/services/dashboard', () => ({
  dashboardService: { getDashboard: vi.fn() },
}));

vi.mock('@/services/league', () => ({
  leagueService: { getStandings: vi.fn() },
}));

vi.mock('@/services/users', () => ({
  usersService: { getProfile: vi.fn() },
}));

// Picks reaches SignalRContext through useSeasonApproval, which the shared test wrapper does not
// provide. These tests are about whether the column is rendered at all, not what is inside it.
vi.mock('@/components/dashboard/Picks', () => ({
  Picks: () => <div data-testid="picks-stub" />,
}));

const navigate = vi.fn();
let params: { userId?: string } = {};

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual<typeof import('react-router-dom')>('react-router-dom');
  return { ...actual, useNavigate: () => navigate, useParams: () => params };
});

const ME = 'test-user-id';

const standing = (overrides: Partial<StandingEntry> = {}): StandingEntry =>
  ({
    position: 1,
    rank: 1,
    userId: ME,
    userName: 'Test User',
    totalPoints: 12,
    picksMade: 6,
    wins: 4,
    draws: 0,
    losses: 2,
    goalsFor: 8,
    goalsAgainst: 4,
    goalDifference: 4,
    averagePointsPerGame: 2,
    isEliminated: false,
    ...overrides,
  }) as StandingEntry;

const standings = (entries: StandingEntry[]) =>
  ({ seasonId: '2026-2027', standings: entries }) as LeagueStandingsData;

const dashboard = () => ({
  user: {
    id: ME,
    firstName: 'Test',
    lastName: 'User',
    email: 'test@example.com',
    totalPoints: 12,
    totalPicks: 6,
    totalWins: 4,
    totalDraws: 0,
    totalLosses: 2,
  },
  currentGameweek: {
    id: 'gw-7',
    seasonId: '2026-2027',
    weekNumber: 7,
    deadline: new Date(Date.now() + 86_400_000).toISOString(),
    status: 'Upcoming',
  },
  upcomingGameweeks: [
    {
      id: 'gw-7',
      seasonId: '2026-2027',
      weekNumber: 7,
      deadline: new Date(Date.now() + 86_400_000).toISOString(),
      status: 'Upcoming',
    },
  ],
  recentPicks: [],
});

describe('Dashboard once a player is out', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    params = {};
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.mocked(dashboardService.getDashboard).mockResolvedValue(dashboard() as any);
  });

  it('carries the picks control while the player is still in', async () => {
    vi.mocked(leagueService.getStandings).mockResolvedValue(standings([standing()]));

    render(<DashboardPage />, { user: createMockUser(), token: 'jwt' });

    await waitFor(() => expect(screen.getByTestId('dashboard-picks-column')).toBeInTheDocument());
    expect(screen.getByTestId('dashboard-grid')).toHaveClass('lg:grid-cols-3');
  });

  it('drops the picks control entirely once eliminated', async () => {
    vi.mocked(leagueService.getStandings).mockResolvedValue(
      standings([standing({ isEliminated: true, eliminatedInGameweek: 5 })])
    );

    render(<DashboardPage />, { user: createMockUser(), token: 'jwt' });

    await waitFor(() =>
      expect(screen.getByTestId('dashboard-fixtures-column')).toBeInTheDocument()
    );

    // Not merely disabled: they cannot pick again this season, so the card would be permanently
    // dead weight taking a third of the width.
    expect(screen.queryByTestId('dashboard-picks-column')).not.toBeInTheDocument();
  });

  it('reflows to two columns so nothing is left empty', async () => {
    vi.mocked(leagueService.getStandings).mockResolvedValue(
      standings([standing({ isEliminated: true, eliminatedInGameweek: 5 })])
    );

    render(<DashboardPage />, { user: createMockUser(), token: 'jwt' });

    await waitFor(() => {
      const grid = screen.getByTestId('dashboard-grid');
      expect(grid).toHaveClass('lg:grid-cols-2');
      expect(grid).not.toHaveClass('lg:grid-cols-3');
    });
  });
});

describe('Dashboard standings column height', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    params = {};
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.mocked(dashboardService.getDashboard).mockResolvedValue(dashboard() as any);
    vi.mocked(leagueService.getStandings).mockResolvedValue(standings([standing()]));
  });

  it('lifts the standings card out of the grid flow so it cannot set the row height', async () => {
    render(<DashboardPage />, { user: createMockUser(), token: 'jwt' });

    const column = await screen.findByTestId('dashboard-standings-column');

    // A grid row is as tall as its tallest item's content. While the card was in the flow, a
    // 266-row table made the row 266 rows tall and every "fill the cell" rule resolved to
    // exactly that. Positioned, it contributes no height and Picks and Fixtures decide the row.
    expect(column).toHaveClass('md:relative');
    expect(column.firstElementChild).toHaveClass('md:absolute', 'md:inset-0');
  });

  it('lifts the picks card out too, leaving Fixtures to set the height', async () => {
    render(<DashboardPage />, { user: createMockUser(), token: 'jwt' });

    const column = await screen.findByTestId('dashboard-picks-column');

    // Picks runs to 38 gameweeks, so in the flow it would set the row height and the other two
    // would stretch to it. With Picks and Standings both positioned, Fixtures is the only card
    // left deciding the row and all three come out its height.
    expect(column).toHaveClass('md:relative');
    expect(column.firstElementChild).toHaveClass('md:absolute', 'md:inset-0');

    const fixtures = screen.getByTestId('dashboard-fixtures-column');
    expect(fixtures).not.toHaveClass('md:relative');
  });
});

describe('Player selector', () => {
  const OTHER = 'other-player';

  const profile = (userId: string, firstName: string): UserProfile =>
    ({
      userId,
      firstName,
      lastName: 'Player',
      photoUrl: undefined,
      standing: standing({ userId, userName: `${firstName} Player` }),
      currentPick: undefined,
      previousPicks: [],
      teamsUsed: [],
      teamsRemaining: [],
      headToHead: undefined,
    }) as unknown as UserProfile;

  beforeEach(() => {
    vi.clearAllMocks();
    params = {};
    vi.mocked(usersService.getProfile).mockImplementation(async (id: string) =>
      profile(id, id === ME ? 'Test' : 'Other')
    );
    vi.mocked(leagueService.getStandings).mockResolvedValue(
      standings([
        standing({ userId: OTHER, userName: 'Other Player', position: 2 }),
        standing({ userId: ME, userName: 'Test User', position: 1 }),
      ])
    );
  });

  it('defaults to the signed-in player when no id is in the URL', async () => {
    render(<UserProfilePage />, { user: createMockUser(), token: 'jwt' });

    // The Players nav link is a bare /users, which has to mean "me".
    await waitFor(() => expect(usersService.getProfile).toHaveBeenCalledWith(ME));
  });

  it('lists everyone, marking the eliminated', async () => {
    vi.mocked(leagueService.getStandings).mockResolvedValue(
      standings([
        standing({ userId: ME, userName: 'Test User' }),
        standing({ userId: OTHER, userName: 'Other Player', isEliminated: true }),
      ])
    );

    render(<UserProfilePage />, { user: createMockUser(), token: 'jwt' });

    const select = await screen.findByTestId('player-selector');
    expect(select).toHaveTextContent('Test User (you)');

    // The league table filters eliminated players out; this is a lookup, so their season is
    // still reachable — including their own, which is where the eliminated banner sends them.
    expect(select).toHaveTextContent('Other Player — eliminated');
  });

  it('navigates to whoever is chosen', async () => {
    const user = userEvent.setup();
    render(<UserProfilePage />, { user: createMockUser(), token: 'jwt' });

    const select = await screen.findByTestId('player-selector');
    await user.selectOptions(select, OTHER);

    expect(navigate).toHaveBeenCalledWith(`/users/${OTHER}`);
  });
});
