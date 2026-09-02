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
  });

  it('leaves the danger zone to its own page', async () => {
    vi.mocked(eliminationsService.getOverview).mockResolvedValue(
      overview({
        dangerZone: {
          gameweekNumber: 5,
          deadline: '2026-09-12T13:00:00Z',
          eliminationCount: 2,
          deadlinePassed: false,
          players: [],
          justSafe: [],
          pointsFromDangerThreshold: 3,
        },
      })
    );

    render(<EliminationsPage />);

    // This page is a record of who has gone out. Who is about to go out is a different question
    // and gets the room to answer it properly elsewhere.
    await screen.findByTestId('eliminations-page');
    expect(screen.queryByTestId('danger-zone')).not.toBeInTheDocument();
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
            finalPosition: 19,
            totalPoints: 2,
            picksMade: 4,
            averagePointsPerGame: 0.5,
            wins: 0,
            draws: 2,
            losses: 2,
            goalsFor: 3,
            goalsAgainst: 7,
            goalDifference: -4,
            eliminatedAt: '2026-09-08T10:00:00Z',
          },
          {
            userId: 'u4',
            userName: 'Diana Wilson',
            gameweekNumber: 3,
            finalPosition: 20,
            totalPoints: 1,
            picksMade: 3,
            averagePointsPerGame: 0.33,
            wins: 0,
            draws: 1,
            losses: 2,
            goalsFor: 1,
            goalsAgainst: 6,
            goalDifference: -5,
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

  it('shows where an eliminated player finished, not where they sit now', async () => {
    vi.mocked(eliminationsService.getOverview).mockResolvedValue(
      overview({
        activePlayers: 21,
        eliminated: [
          {
            userId: 'u3',
            userName: 'Charlie Davis',
            gameweekNumber: 4,
            finalPosition: 22,
            totalPoints: 2,
            picksMade: 4,
            averagePointsPerGame: 0.5,
            wins: 0,
            draws: 2,
            losses: 2,
            goalsFor: 3,
            goalsAgainst: 7,
            goalDifference: -4,
            eliminatedAt: '2026-09-08T10:00:00Z',
          },
        ],
      })
    );

    render(<EliminationsPage />);

    // The number comes from the elimination record, so it stays put as the players still in
    // score. It used to be read from the live standings and drifted.
    expect(await screen.findByTestId('player-position-u3')).toHaveTextContent('22');
  });

  it('reports a page it cannot load', async () => {
    vi.mocked(eliminationsService.getOverview).mockRejectedValue(new Error('nope'));

    render(<EliminationsPage />);

    expect(await screen.findByTestId('eliminations-error')).toBeInTheDocument();
  });
});
