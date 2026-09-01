import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { LeagueStandings } from './LeagueStandings';
import { render, createMockUser } from '@/test/test-utils';
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

  // Points per game remains the first tiebreak in the ordering chain and is still carried on
  // the DTO — it is simply not displayed. These pin that it stays out of the table, so it is
  // not reinstated by reflex on the grounds that the standings are sorted on it.
  it.each([
    ['full', undefined],
    ['compact', true],
  ])('is not shown on the %s table', async (_name, compact) => {
    render(<LeagueStandings compact={compact} />);

    await screen.findByTestId('standing-points-1');
    expect(screen.queryByTestId('standing-average-1')).not.toBeInTheDocument();
  });

  it('leaves it out of the key', async () => {
    render(<LeagueStandings />);

    await screen.findByTestId('standing-points-1');
    expect(screen.getByTestId('league-standings-card')).not.toHaveTextContent('Points per game');
  });
});

/**
 * At a few hundred players the table is many screens long, so the two questions it has to answer
 * quickly are "where am I" and "where is my mate".
 */
describe('LeagueStandings at scale', () => {
  const league = (count: number, mePosition: number, meEliminated = false) =>
    ({
      standings: Array.from({ length: count }, (_, i) => ({
        userId: i + 1 === mePosition ? 'test-user-id' : `user-${i + 1}`,
        userName: i + 1 === mePosition ? 'Test User' : `Player ${i + 1}`,
        position: i + 1,
        rank: i + 1,
        picksMade: 2,
        wins: 1,
        draws: 0,
        losses: 1,
        goalsFor: 2,
        goalsAgainst: 2,
        goalDifference: 0,
        totalPoints: count - i,
        averagePointsPerGame: 1.5,
        isEliminated: i + 1 === mePosition ? meEliminated : false,
      })),
    }) as LeagueStandingsData;

  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(leagueService.getStandings).mockResolvedValue(league(40, 30));
    // jsdom has no layout, so scrollIntoView is not implemented on elements.
    Element.prototype.scrollIntoView = vi.fn();
  });

  it('narrows the table to matching names', async () => {
    const user = userEvent.setup();
    render(<LeagueStandings />, { user: createMockUser(), token: 'jwt' });

    await screen.findByTestId('standings-filter');
    expect(screen.getByTestId('standing-row-1')).toBeInTheDocument();

    await user.type(screen.getByTestId('standings-filter'), 'Player 12');

    expect(screen.getByTestId('standing-row-12')).toBeInTheDocument();
    expect(screen.queryByTestId('standing-row-1')).not.toBeInTheDocument();
    expect(screen.getByTestId('standings-filter-count')).toHaveTextContent('1 of 40');
  });

  it('says so when nothing matches', async () => {
    const user = userEvent.setup();
    render(<LeagueStandings />, { user: createMockUser(), token: 'jwt' });

    await screen.findByTestId('standings-filter');
    await user.type(screen.getByTestId('standings-filter'), 'Nobody');

    expect(screen.getByTestId('standings-no-matches')).toBeInTheDocument();
  });

  it('jumps to the signed-in player, clearing a filter that would hide them', async () => {
    const user = userEvent.setup();
    render(<LeagueStandings />, { user: createMockUser(), token: 'jwt' });

    await screen.findByTestId('standings-filter');

    // Filter to someone else first, so the row being jumped to is not currently rendered.
    await user.type(screen.getByTestId('standings-filter'), 'Player 12');
    expect(screen.queryByTestId('standing-row-30')).not.toBeInTheDocument();

    await user.click(screen.getByTestId('standings-jump-to-me'));

    await waitFor(() => expect(screen.getByTestId('standing-row-30')).toBeInTheDocument());
    expect(screen.getByTestId('standings-filter')).toHaveValue('');
    expect(Element.prototype.scrollIntoView).toHaveBeenCalled();
  });

  it('offers no jump button to a player who is out', async () => {
    vi.mocked(leagueService.getStandings).mockResolvedValue(league(40, 30, true));
    render(<LeagueStandings />, { user: createMockUser(), token: 'jwt' });

    // Eliminated players are filtered out of the table, so there is no row to jump to.
    await screen.findByTestId('standings-filter');
    expect(screen.queryByTestId('standings-jump-to-me')).not.toBeInTheDocument();
  });

  it('keeps the name filter off the dashboard card, which is too narrow for it', async () => {
    render(<LeagueStandings compact />, { user: createMockUser(), token: 'jwt' });

    await screen.findByTestId('standing-row-1');
    expect(screen.queryByTestId('standings-filter')).not.toBeInTheDocument();
  });

  it('still offers a jump to me on the dashboard card', async () => {
    const user = userEvent.setup();
    render(<LeagueStandings compact />, { user: createMockUser(), token: 'jwt' });

    await screen.findByTestId('standing-row-1');
    await user.click(screen.getByTestId('standings-jump-to-me-compact'));

    // The card scrolls its own rows, so finding yourself in it needs the same help the full
    // table gives.
    expect(Element.prototype.scrollIntoView).toHaveBeenCalled();
  });

  it('can send the dashboard card back to the top', async () => {
    const scrollTo = vi.fn();
    Element.prototype.scrollTo = scrollTo;

    const user = userEvent.setup();
    render(<LeagueStandings compact />, { user: createMockUser(), token: 'jwt' });

    await screen.findByTestId('standing-row-1');
    await user.click(screen.getByTestId('standings-scroll-top'));

    // Having jumped to yourself there is no page scroll to get back with — the card scrolls
    // itself, so it needs its own way back to first place.
    expect(scrollTo).toHaveBeenCalledWith({ top: 0, behavior: 'smooth' });
  });

  it('shortens the title on the dashboard to make room for the controls', async () => {
    render(<LeagueStandings compact />, { user: createMockUser(), token: 'jwt' });

    await screen.findByTestId('standing-row-1');
    expect(screen.getByText('Standings')).toBeInTheDocument();
    expect(screen.queryByText('League Standings')).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Full Table' })).toBeInTheDocument();
  });

  it('keeps the full title on the standings page, which has the room', async () => {
    render(<LeagueStandings />, { user: createMockUser(), token: 'jwt' });

    await screen.findByTestId('standing-row-1');
    expect(screen.getByText('League Standings')).toBeInTheDocument();
  });

  it('offers no jump on the dashboard card to a player who is out', async () => {
    vi.mocked(leagueService.getStandings).mockResolvedValue(league(40, 30, true));
    render(<LeagueStandings compact />, { user: createMockUser(), token: 'jwt' });

    await screen.findByTestId('standing-row-1');
    expect(screen.queryByTestId('standings-jump-to-me-compact')).not.toBeInTheDocument();
  });

  it('makes the dashboard card fill its cell and scroll, rather than set its own height', async () => {
    const { container } = render(<LeagueStandings compact />, {
      user: createMockUser(),
      token: 'jwt',
    });

    await screen.findByTestId('standing-row-1');

    // DashboardPage gives the card its height; the card only has to stretch to it. min-h-0 is
    // what lets the table scroll at all.
    expect(screen.getByTestId('league-standings-card')).toHaveClass('h-full');

    const scroller = container.querySelector('.overflow-y-auto');
    expect(scroller).not.toBeNull();
    expect(scroller).toHaveClass('md:flex-1', 'md:min-h-0');

    // Below md there is a single column and nothing to match, so a cap still applies.
    expect(scroller).toHaveClass('max-h-[26rem]', 'md:max-h-none');
  });

  it('lets the full table grow, since it is the whole page', async () => {
    const { container } = render(<LeagueStandings />, {
      user: createMockUser(),
      token: 'jwt',
    });

    await screen.findByTestId('standing-row-1');
    expect(container.querySelector('.overflow-y-auto')).toBeNull();
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
