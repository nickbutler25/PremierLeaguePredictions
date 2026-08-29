import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import { UserProfilePage } from './UserProfilePage';
import { render, createMockUser } from '@/test/test-utils';
import { usersService } from '@/services/users';
import type { PickSummary, UserProfile } from '@/types';

vi.mock('@/services/users', () => ({
  usersService: { getProfile: vi.fn() },
}));

const PLAYER_ID = 'player-1';

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual<typeof import('react-router-dom')>('react-router-dom');
  return { ...actual, useParams: () => ({ userId: PLAYER_ID }) };
});

const pick = (gameweekNumber: number, teamName: string): PickSummary => ({
  gameweekNumber,
  teamId: gameweekNumber,
  teamName,
  teamShortName: teamName.slice(0, 3).toUpperCase(),
  logoUrl: `https://example.test/${gameweekNumber}.png`,
  opponentName: 'Everton',
  teamScore: 2,
  opponentScore: 1,
  outcome: 'Win',
  isLive: false,
});

const profile = (overrides?: Partial<UserProfile>): UserProfile => ({
  userId: PLAYER_ID,
  firstName: 'Alice',
  lastName: 'Johnson',
  photoUrl: 'https://example.test/alice.png',
  seasonId: '2026-2027',
  standing: {
    position: 3,
    totalPoints: 20,
    picksMade: 8,
    wins: 6,
    draws: 2,
    losses: 0,
    goalsFor: 14,
    goalsAgainst: 6,
    goalDifference: 8,
    averagePointsPerGame: 2.5,
    isEliminated: false,
  },
  previousPicks: [pick(1, 'Arsenal')],
  teamUsage: {
    half: 1,
    firstGameweek: 1,
    lastGameweek: 19,
    maxTimesTeamCanBePicked: 1,
    used: [{ teamId: 1, teamName: 'Arsenal', teamShortName: 'ARS', timesPicked: 1 }],
    available: [{ teamId: 2, teamName: 'Liverpool', teamShortName: 'LIV', timesPicked: 0 }],
  },
  ...overrides,
});

describe('UserProfilePage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(usersService.getProfile).mockResolvedValue(profile());
  });

  it('shows who the player is, with their picture', async () => {
    render(<UserProfilePage />);

    expect(await screen.findByText('Alice Johnson')).toBeInTheDocument();
    expect(screen.getByTestId('user-avatar')).toHaveAttribute(
      'src',
      'https://example.test/alice.png'
    );
    expect(screen.getByText('3rd')).toBeInTheDocument();
  });

  it('leads with the average points per game, since eliminations run on it', async () => {
    render(<UserProfilePage />);

    await waitFor(() => expect(screen.getByText('2.50')).toBeInTheDocument());
    expect(screen.getByText('Avg')).toBeInTheDocument();
  });

  it('says picks are hidden rather than showing an empty current pick', async () => {
    render(<UserProfilePage />);

    // No gameweek is under way, so there is deliberately nothing to reveal.
    expect(await screen.findByTestId('no-current-pick')).toBeInTheDocument();
    expect(screen.getByText(/hidden until the gameweek deadline passes/i)).toBeInTheDocument();
  });

  it('shows the current pick once the gameweek is under way', async () => {
    vi.mocked(usersService.getProfile).mockResolvedValue(
      profile({ currentPick: pick(9, 'Chelsea'), currentGameweek: 9 })
    );

    render(<UserProfilePage />);

    const card = await screen.findByTestId('profile-current-pick');
    expect(card).toHaveTextContent('Chelsea');
    expect(card).toHaveTextContent('GW9');
    expect(screen.queryByTestId('no-current-pick')).not.toBeInTheDocument();
  });

  it('lists the teams spent and the teams left', async () => {
    render(<UserProfilePage />);

    expect(await screen.findByTestId('teams-used')).toHaveTextContent('ARS');
    expect(screen.getByTestId('teams-available')).toHaveTextContent('LIV');
  });

  it('compares you with them, but not with yourself', async () => {
    const headToHead = {
      gameweeksCompared: 4,
      samePickCount: 1,
      viewerPoints: 7,
      playerPoints: 10,
      differences: [
        {
          gameweekNumber: 2,
          viewerPick: pick(2, 'Liverpool'),
          playerPick: pick(2, 'Arsenal'),
          viewerPoints: 0,
          playerPoints: 3,
        },
      ],
    };
    vi.mocked(usersService.getProfile).mockResolvedValue(profile({ headToHead }));

    render(<UserProfilePage />);

    expect(await screen.findByTestId('head-to-head-score')).toHaveTextContent('7');
    expect(screen.getByTestId('head-to-head-score')).toHaveTextContent('10');
    expect(screen.getByText(/behind by 3/i)).toBeInTheDocument();
  });

  it('drops the comparison on your own profile', async () => {
    const me = createMockUser({ id: PLAYER_ID });
    vi.mocked(usersService.getProfile).mockResolvedValue(profile({ userId: me.id }));

    render(<UserProfilePage />, { user: me, token: 'test-token' });

    await waitFor(() => expect(screen.getByText('(You)')).toBeInTheDocument());
    expect(screen.queryByTestId('profile-head-to-head')).not.toBeInTheDocument();
  });

  it('reports a profile it cannot load', async () => {
    vi.mocked(usersService.getProfile).mockRejectedValue(new Error('nope'));

    render(<UserProfilePage />);

    expect(await screen.findByTestId('profile-error')).toBeInTheDocument();
  });
});
