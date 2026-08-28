import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import { LeagueStandings } from './LeagueStandings';
import { render } from '@/test/test-utils';
import { leagueService } from '@/services/league';
import type { LeagueStandings as LeagueStandingsData, PickSummary } from '@/types';

vi.mock('@/services/league', () => ({
  leagueService: {
    getStandings: vi.fn(),
  },
}));

const formPick = (gameweekNumber: number, outcome: PickSummary['outcome']): PickSummary => ({
  gameweekNumber,
  teamId: gameweekNumber,
  teamName: `Team ${gameweekNumber}`,
  teamShortName: `T${gameweekNumber}`,
  logoUrl: `https://example.test/${gameweekNumber}.png`,
  opponentName: 'Opponent',
  teamScore: outcome === 'Win' ? 2 : 1,
  opponentScore: outcome === 'Loss' ? 2 : 1,
  outcome,
  isLive: false,
});

// A revealed pick is what tells the table a gameweek is under way — the server attaches it
// only for the in-progress gameweek, so the tests set the state the way the app sees it.
const standings = (overrides?: Partial<LeagueStandingsData['standings'][number]>) =>
  ({
    standings: [
      {
        userId: 'user-1',
        userName: 'Alice Johnson',
        position: 1,
        picksMade: 15,
        wins: 10,
        draws: 3,
        losses: 2,
        goalsFor: 28,
        goalsAgainst: 12,
        goalDifference: 16,
        totalPoints: 33,
        averagePointsPerGame: 2.2,
        isEliminated: false,
        ...overrides,
      },
    ],
  }) as LeagueStandingsData;

const header = (name: string) => screen.queryByRole('columnheader', { name });

describe('LeagueStandings columns', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(leagueService.getStandings).mockResolvedValue(standings());
  });

  it('shows the season record between gameweeks on the compact table', async () => {
    render(<LeagueStandings compact />);

    await waitFor(() => expect(header('W')).toBeInTheDocument());
    expect(header('D')).toBeInTheDocument();
    expect(header('L')).toBeInTheDocument();
    expect(header('Pts')).toBeInTheDocument();

    // Nothing is being played, so there is no pick to reveal and no goal difference moving.
    expect(header('Pick')).not.toBeInTheDocument();
    expect(header('GD')).not.toBeInTheDocument();
  });

  it('swaps to the pick and goal difference while a gameweek is in progress', async () => {
    vi.mocked(leagueService.getStandings).mockResolvedValue(
      standings({ currentPick: formPick(11, 'Win') })
    );
    render(<LeagueStandings compact />);

    await waitFor(() => expect(header('Pick')).toBeInTheDocument());
    expect(header('GD')).toBeInTheDocument();
    expect(header('Pts')).toBeInTheDocument();

    expect(header('W')).not.toBeInTheDocument();
    expect(header('D')).not.toBeInTheDocument();
    expect(header('L')).not.toBeInTheDocument();
  });

  it('keeps points as the last column on the compact table', async () => {
    vi.mocked(leagueService.getStandings).mockResolvedValue(
      standings({ currentPick: formPick(11, 'Win') })
    );
    render(<LeagueStandings compact />);

    await waitFor(() => expect(header('Pts')).toBeInTheDocument());
    const headers = screen.getAllByRole('columnheader').map((h) => h.textContent);
    expect(headers).toEqual(['#', 'Name', 'Pick', 'GD', 'Pts']);
  });

  it('carries every column on the full table while a gameweek is in progress', async () => {
    vi.mocked(leagueService.getStandings).mockResolvedValue(
      standings({ currentPick: formPick(11, 'Win') })
    );
    render(<LeagueStandings />);

    await waitFor(() => expect(header('Pick')).toBeInTheDocument());
    const headers = screen.getAllByRole('columnheader').map((h) => h.textContent);
    expect(headers).toEqual([
      '#',
      'Name',
      'Pick',
      'P',
      'W',
      'D',
      'L',
      'Pts',
      'Avg',
      'GF',
      'GA',
      'GD',
      'Form',
    ]);
  });

  it('drops the pick column from the full table between gameweeks', async () => {
    render(<LeagueStandings />);

    await waitFor(() => expect(header('Pts')).toBeInTheDocument());
    // Nothing to reveal, so the column would be empty in every row.
    expect(header('Pick')).not.toBeInTheDocument();
    const headers = screen.getAllByRole('columnheader').map((h) => h.textContent);
    expect(headers).toEqual([
      '#',
      'Name',
      'P',
      'W',
      'D',
      'L',
      'Pts',
      'Avg',
      'GF',
      'GA',
      'GD',
      'Form',
    ]);
  });
});

describe('LeagueStandings average', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(leagueService.getStandings).mockResolvedValue(standings());
  });

  it('shows the figure the table is ordered on', async () => {
    render(<LeagueStandings />);

    // Shown so a reader can see why two players level on points are ordered as they are.
    expect(await screen.findByTestId('standing-average-1')).toHaveTextContent('2.20');
  });

  it('is left off the compact table', async () => {
    render(<LeagueStandings compact />);

    // It only breaks a tie on points, and a sixth column pushes the points off the card.
    await screen.findByTestId('standing-points-1');
    expect(screen.queryByTestId('standing-average-1')).not.toBeInTheDocument();
  });

  it('explains the column in the key', async () => {
    render(<LeagueStandings />);

    await screen.findByTestId('standing-average-1');
    expect(screen.getByTestId('league-standings-card')).toHaveTextContent('Points per game');
  });
});

describe('LeagueStandings form guide', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(leagueService.getStandings).mockResolvedValue(
      standings({ form: [formPick(1, 'Win'), formPick(2, 'Draw'), formPick(3, 'Loss')] })
    );
  });

  it('shows the result as a W/D/L badge rather than the club crest', async () => {
    render(<LeagueStandings />);

    const cell = await screen.findByTestId('standing-form-1');

    expect(cell.textContent).toContain('W');
    expect(cell.textContent).toContain('D');
    expect(cell.textContent).toContain('L');
    expect(cell.querySelector('img')).toBeNull();
  });

  it('still names the club and score for each result', async () => {
    render(<LeagueStandings />);

    const cell = await screen.findByTestId('standing-form-1');

    expect(cell.textContent).toContain('GW1 — Team 1 2-1 Opponent (won)');
  });
});
