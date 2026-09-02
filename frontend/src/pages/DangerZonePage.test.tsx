import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen } from '@testing-library/react';
import { DangerZonePage } from './DangerZonePage';
import { render } from '@/test/test-utils';
import { eliminationsService } from '@/services/eliminations';
import type { AtRiskPlayer, DangerZone, EliminationsOverview } from '@/types';

vi.mock('@/services/eliminations', () => ({
  eliminationsService: { getOverview: vi.fn() },
}));

const player = (
  userId: string,
  userName: string,
  position: number,
  totalPoints: number,
  gap: { behind?: number; clear?: number } = {}
): AtRiskPlayer => ({
  userId,
  userName,
  position,
  totalPoints,
  picksMade: 4,
  averagePointsPerGame: totalPoints / 4,
  wins: 1,
  draws: 0,
  losses: 3,
  goalsFor: 2,
  goalsAgainst: 8,
  goalDifference: -6,
  pointsBehindSafety: gap.behind ?? 0,
  pointsClearOfZone: gap.clear ?? 0,
  averageBehindSafety: 0,
});

const zone = (overrides?: Partial<DangerZone>): DangerZone => ({
  gameweekNumber: 5,
  deadline: '2026-09-12T13:00:00Z',
  eliminationCount: 2,
  deadlinePassed: false,
  // Both lists arrive worst first.
  players: [
    player('drop2', 'Worst Player', 22, 1, { behind: 3 }),
    player('drop1', 'Nearly Safe', 21, 2, { behind: 2 }),
  ],
  justSafe: [
    player('safe1', 'Just Above', 20, 3, { clear: 1 }),
    player('safe2', 'Two Clear', 19, 4, { clear: 2 }),
  ],
  pointsFromDangerThreshold: 3,
  ...overrides,
});

const overview = (dangerZone?: DangerZone): EliminationsOverview => ({
  seasonId: '2026-2027',
  eliminated: [],
  activePlayers: 22,
  totalPlayers: 22,
  dangerZone,
});

describe('DangerZonePage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(eliminationsService.getOverview).mockResolvedValue(overview(zone()));
  });

  it('names the gameweek and how many go out', async () => {
    render(<DangerZonePage />);

    const card = await screen.findByTestId('danger-zone');
    expect(card).toHaveTextContent('Gameweek 5');
    expect(card).toHaveTextContent('2 players go out');
  });

  it('puts the drop zone at the bottom, below the cut-off line', async () => {
    render(<DangerZonePage />);

    await screen.findByTestId('danger-zone');

    // Read down the card: the chasing pack, the line, then the players who would go out. The
    // worst player is last, the way a league table bottoms out.
    const card = screen.getByTestId('danger-zone');
    const order = [...card.querySelectorAll('[data-testid]')]
      .map((el) => el.getAttribute('data-testid')!)
      .filter(
        (id) =>
          id === 'elimination-cutoff' || id.startsWith('just-safe-') || id.startsWith('at-risk-')
      );

    expect(order).toEqual([
      'just-safe-safe2',
      'just-safe-safe1',
      'elimination-cutoff',
      'at-risk-drop1',
      'at-risk-drop2',
    ]);
  });

  it('shows how far each player is from the line, in points', async () => {
    render(<DangerZonePage />);

    await screen.findByTestId('danger-zone');

    // Points, not points per game: a gap of "0.33" beside a column of whole points reads as the
    // wrong quantity.
    expect(screen.getByTestId('from-line-drop2')).toHaveTextContent('3 pts');
    expect(screen.getByTestId('from-line-safe1')).toHaveTextContent('+1 pts');
  });

  it('says so when nobody outside the zone is close', async () => {
    vi.mocked(eliminationsService.getOverview).mockResolvedValue(
      overview(zone({ justSafe: [] }))
    );

    render(<DangerZonePage />);

    expect(await screen.findByTestId('danger-zone-nobody-close')).toHaveTextContent(
      'within 3 points'
    );
    // The line and the drop zone are still there — somebody is always going out.
    expect(screen.getByTestId('elimination-cutoff')).toBeInTheDocument();
  });

  it('says nobody is in danger when no elimination is configured', async () => {
    vi.mocked(eliminationsService.getOverview).mockResolvedValue(overview(undefined));

    render(<DangerZonePage />);

    expect(await screen.findByText('Nobody is in danger')).toBeInTheDocument();
    expect(screen.queryByTestId('elimination-cutoff')).not.toBeInTheDocument();
  });

  it('says results alone decide it once the deadline has passed', async () => {
    vi.mocked(eliminationsService.getOverview).mockResolvedValue(
      overview(zone({ deadlinePassed: true }))
    );

    render(<DangerZonePage />);

    expect(await screen.findByText(/only results can change this now/i)).toBeInTheDocument();
  });

  it('reports a page it cannot load', async () => {
    vi.mocked(eliminationsService.getOverview).mockRejectedValue(new Error('nope'));

    render(<DangerZonePage />);

    expect(await screen.findByTestId('danger-zone-error')).toBeInTheDocument();
  });
});
