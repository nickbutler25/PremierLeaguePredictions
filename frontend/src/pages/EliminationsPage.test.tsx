import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import { EliminationsPage } from './EliminationsPage';
import { render } from '@/test/test-utils';
import { eliminationsService } from '@/services/eliminations';
import type { EliminationsOverview } from '@/types';

vi.mock('@/services/eliminations', () => ({
  eliminationsService: { getOverview: vi.fn() },
}));

const overview = (overrides?: Partial<EliminationsOverview>): EliminationsOverview => ({
  seasonId: '2026-2027',
  eliminated: [],
  activePlayers: 22,
  totalPlayers: 22,
  ...overrides,
});

const atRisk = (userId: string, userName: string, average: number, behind: number) => ({
  userId,
  userName,
  position: 21,
  totalPoints: 3,
  picksMade: 4,
  averagePointsPerGame: average,
  averageBehindSafety: behind,
});

describe('EliminationsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(eliminationsService.getOverview).mockResolvedValue(overview());
  });

  it('says who is still in', async () => {
    render(<EliminationsPage />);

    expect(await screen.findByText('22 players still in')).toBeInTheDocument();
  });

  it('shows nothing about eliminations before any have happened', async () => {
    render(<EliminationsPage />);

    expect(await screen.findByTestId('no-eliminations')).toBeInTheDocument();
    expect(screen.queryByTestId('danger-zone')).not.toBeInTheDocument();
  });

  it('names who the next gameweek threatens', async () => {
    vi.mocked(eliminationsService.getOverview).mockResolvedValue(
      overview({
        dangerZone: {
          gameweekNumber: 5,
          deadline: '2026-09-12T13:00:00Z',
          eliminationCount: 2,
          deadlinePassed: false,
          players: [atRisk('u1', 'Bob Smith', 0.5, 0.75), atRisk('u2', 'Eve Martinez', 0.6, 0.65)],
        },
      })
    );

    render(<EliminationsPage />);

    const zone = await screen.findByTestId('danger-zone');
    expect(zone).toHaveTextContent('Danger zone');
    expect(zone).toHaveTextContent('GW5');
    expect(zone).toHaveTextContent('2 players go out');
    expect(zone).toHaveTextContent('Bob Smith');
    expect(zone).toHaveTextContent('0.75 behind safety');
  });

  it('says the zone can still change while picks are open', async () => {
    vi.mocked(eliminationsService.getOverview).mockResolvedValue(
      overview({
        dangerZone: {
          gameweekNumber: 5,
          deadline: '2026-09-12T13:00:00Z',
          eliminationCount: 1,
          deadlinePassed: false,
          players: [atRisk('u1', 'Bob Smith', 0.5, 0.75)],
        },
      })
    );

    render(<EliminationsPage />);

    expect(await screen.findByText(/so this can still change/i)).toBeInTheDocument();
  });

  it('says results alone decide it once the deadline has passed', async () => {
    vi.mocked(eliminationsService.getOverview).mockResolvedValue(
      overview({
        dangerZone: {
          gameweekNumber: 5,
          deadline: '2026-09-12T13:00:00Z',
          eliminationCount: 1,
          deadlinePassed: true,
          players: [atRisk('u1', 'Bob Smith', 0.5, 0.75)],
        },
      })
    );

    render(<EliminationsPage />);

    expect(await screen.findByText(/only results can change this now/i)).toBeInTheDocument();
  });

  it('groups those already out by the gameweek they went out in', async () => {
    vi.mocked(eliminationsService.getOverview).mockResolvedValue(
      overview({
        activePlayers: 19,
        eliminated: [
          {
            userId: 'u3',
            userName: 'Charlie Davis',
            gameweekNumber: 4,
            totalPoints: 2,
            picksMade: 4,
            averagePointsPerGame: 0.5,
            eliminatedAt: '2026-09-08T10:00:00Z',
          },
          {
            userId: 'u4',
            userName: 'Diana Wilson',
            gameweekNumber: 3,
            totalPoints: 1,
            picksMade: 3,
            averagePointsPerGame: 0.33,
            eliminatedAt: '2026-09-01T10:00:00Z',
          },
        ],
      })
    );

    render(<EliminationsPage />);

    await waitFor(() => expect(screen.getByText('After gameweek 4')).toBeInTheDocument());
    expect(screen.getByText('After gameweek 3')).toBeInTheDocument();
    expect(screen.getByText('19 of 22 still in · 2 out')).toBeInTheDocument();
    expect(screen.queryByTestId('no-eliminations')).not.toBeInTheDocument();
  });

  it('reports a page it cannot load', async () => {
    vi.mocked(eliminationsService.getOverview).mockRejectedValue(new Error('nope'));

    render(<EliminationsPage />);

    expect(await screen.findByTestId('eliminations-error')).toBeInTheDocument();
  });
});
