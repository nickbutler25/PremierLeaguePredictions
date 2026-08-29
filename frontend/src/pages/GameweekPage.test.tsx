import { describe, it, expect, vi, beforeEach } from 'vitest';
import userEvent from '@testing-library/user-event';
import { screen, within } from '@testing-library/react';
import { GameweekPage } from './GameweekPage';
import { render } from '@/test/test-utils';
import { gameweekService } from '@/services/gameweek';
import type { LiveGameweek, PickSummary } from '@/types';

vi.mock('@/services/gameweek', () => ({
  gameweekService: { getGameweek: vi.fn() },
}));

const pick = (overrides?: Partial<PickSummary>): PickSummary => ({
  gameweekNumber: 2,
  teamId: 3,
  teamName: 'Chelsea',
  teamShortName: 'CHE',
  outcome: 'Win',
  isLive: true,
  opponentName: 'Everton',
  teamScore: 1,
  opponentScore: 0,
  ...overrides,
});

const live = (overrides?: Partial<LiveGameweek>): LiveGameweek => ({
  seasonId: '2026-2027',
  isLive: true,
  isRevealed: true,
  isComplete: false,
  availableGameweeks: [
    { gameweekNumber: 2, deadline: '2026-08-28T18:00:00Z', isLive: true, isComplete: false },
    { gameweekNumber: 1, deadline: '2026-08-21T18:00:00Z', isLive: false, isComplete: true },
  ],
  gameweekNumber: 2,
  deadline: '2026-08-28T18:00:00Z',
  fixturesTotal: 2,
  fixturesSettled: 0,
  fixturesInPlay: 1,
  fixturesToKickOff: 1,
  playersWithPick: 4,
  totalPlayers: 4,
  myPick: {
    pick: pick(),
    isAutoAssigned: false,
    kickoffTime: '2026-08-29T14:00:00Z',
    ownedByCount: 1,
    ownershipPercent: 25,
    pointsSoFar: 3,
    playersGainedOn: 3,
    playersLevelWith: 0,
    playersLostTo: 0,
    fieldAveragePoints: 0.75,
  },
  ownership: [
    {
      teamId: 4,
      teamName: 'Everton',
      teamShortName: 'EVE',
      count: 2,
      percent: 50,
      isMyPick: false,
      opponentName: 'Chelsea',
      teamScore: 0,
      opponentScore: 1,
      outcome: 'Loss',
      isLive: true,
      points: 0,
      owners: [
        { userId: 'u2', userName: 'Bob Smith', isMe: false, isEliminated: false },
        { userId: 'u3', userName: 'Cara Davies', isMe: false, isEliminated: false },
      ],
    },
    {
      teamId: 3,
      teamName: 'Chelsea',
      teamShortName: 'CHE',
      count: 1,
      percent: 25,
      isMyPick: true,
      opponentName: 'Everton',
      teamScore: 1,
      opponentScore: 0,
      outcome: 'Win',
      isLive: true,
      points: 3,
      owners: [{ userId: 'u1', userName: 'Alice Johnson', isMe: true, isEliminated: false }],
    },
  ],
  fixtures: [
    {
      id: 'f1',
      kickoffTime: '2026-08-29T14:00:00Z',
      status: 'IN_PLAY',
      homeTeamId: 3,
      homeTeamName: 'Chelsea',
      homeTeamShortName: 'CHE',
      awayTeamId: 4,
      awayTeamName: 'Everton',
      awayTeamShortName: 'EVE',
      homeScore: 1,
      awayScore: 0,
      homePickCount: 1,
      awayPickCount: 2,
      hasMyPick: true,
      isLive: true,
      isSettled: false,
    },
  ],
  threat: {
    myPosition: 3,
    myPositionBefore: 4,
    rivals: [
      {
        userId: 'u3',
        userName: 'Cara Davies',
        position: 1,
        positionChange: 0,
        totalPoints: 3,
        pointsFromMe: 0,
        isMe: false,
        isEliminated: false,
        sharesMyPick: false,
      },
      {
        userId: 'u1',
        userName: 'Alice Johnson',
        position: 3,
        positionChange: 1,
        totalPoints: 3,
        pointsFromMe: 0,
        isMe: true,
        isEliminated: false,
        sharesMyPick: false,
      },
    ],
    leaders: [],
    sharingMyPick: 0,
    onDifferentPick: 3,
  },
  teamUsage: {
    half: 1,
    firstGameweek: 1,
    lastGameweek: 19,
    maxTimesTeamCanBePicked: 1,
    used: [{ teamId: 3, teamName: 'Chelsea', teamShortName: 'CHE', timesPicked: 1 }],
    available: [{ teamId: 1, teamName: 'Arsenal', teamShortName: 'ARS', timesPicked: 0 }],
  },
  ...overrides,
});

describe('GameweekPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(gameweekService.getGameweek).mockResolvedValue(live());
  });

  describe('while the gameweek is live', () => {
    it('leads with the gameweek and how far through it is', async () => {
      render(<GameweekPage />);

      expect(await screen.findByText('Gameweek 2')).toBeInTheDocument();
      expect(screen.getByText('1 live')).toBeInTheDocument();
      expect(screen.getByText(/0 of 2 matches played/)).toBeInTheDocument();
      expect(screen.getByText(/4 of 4 players had a pick/)).toBeInTheDocument();
    });

    it("shows the viewer's pick with its live score", async () => {
      render(<GameweekPage />);

      // Scoped to the card: the same team and score also appear in the ownership breakdown
      // and the fixture list, which is correct but makes a bare query ambiguous.
      const card = within(await screen.findByTestId('my-pick'));
      expect(card.getByText('Chelsea')).toBeInTheDocument();
      expect(card.getByText('1–0')).toBeInTheDocument();
      expect(card.getByText(/Winning · 3 pts/)).toBeInTheDocument();
    });

    it('says what the pick is worth against the field', async () => {
      render(<GameweekPage />);

      const differential = await screen.findByTestId('differential');
      expect(differential).toHaveTextContent('gaining on');
      // Nobody else took Chelsea, which is the whole point of a differential.
      expect(differential).toHaveTextContent('You are the only one on this team.');
      expect(differential).toHaveTextContent('averaging 0.75 points');
    });

    it('breaks the field down by team', async () => {
      render(<GameweekPage />);

      const ownership = await screen.findByTestId('ownership');
      expect(ownership).toHaveTextContent('50.0%');
      expect(ownership).toHaveTextContent('25.0%');
    });

    it('names who took a team only once asked', async () => {
      render(<GameweekPage />);

      await screen.findByTestId('ownership');
      expect(screen.queryByTestId('owners-4')).not.toBeInTheDocument();

      // By row, not by name: every row names its opponent too, so "Everton" matches two.
      await userEvent.click(within(screen.getByTestId('ownership-4')).getByRole('button'));

      const owners = screen.getByTestId('owners-4');
      expect(owners).toHaveTextContent('Bob Smith');
      expect(owners).toHaveTextContent('Cara Davies');
    });

    it('shows the table moving under the viewer', async () => {
      render(<GameweekPage />);

      expect(await screen.findByTestId('rivals')).toHaveTextContent(
        'You have moved from 4 to 3 so far this gameweek.'
      );
      expect(screen.getByLabelText('up 1 place')).toBeInTheDocument();
    });

    it('tags each fixture with how many players it carries', async () => {
      render(<GameweekPage />);

      const fixture = await screen.findByTestId('fixture-f1');
      expect(fixture).toHaveTextContent('1 picked');
      expect(fixture).toHaveTextContent('2 picked');
    });

    it('warns a player who is currently going out', async () => {
      vi.mocked(gameweekService.getGameweek).mockResolvedValue(
        live({
          threat: {
            ...live().threat,
            danger: {
              isSettled: false,
              eliminationCount: 1,
              amIInTheZone: true,
              myMarginToSafety: 1.5,
              players: [
                {
                  userId: 'u1',
                  userName: 'Alice Johnson',
                  isMe: true,
                  averagePointsPerGame: 0,
                  averageBehindSafety: 1.5,
                },
              ],
            },
          },
        })
      );

      render(<GameweekPage />);

      const danger = await screen.findByTestId('live-danger');
      expect(danger).toHaveTextContent('You are going out');
      expect(danger).toHaveTextContent('1.50 points per game below safety');
    });

    it('tells a player with no pick that they score nothing', async () => {
      vi.mocked(gameweekService.getGameweek).mockResolvedValue(live({ myPick: null }));

      render(<GameweekPage />);

      expect(await screen.findByTestId('my-pick-none')).toBeInTheDocument();
      expect(screen.queryByTestId('my-pick')).not.toBeInTheDocument();
      // The rest of the page still works — they can watch even if they cannot score.
      expect(screen.getByTestId('ownership')).toBeInTheDocument();
    });
  });

  describe('the archive', () => {
    it('offers the gameweeks that have locked', async () => {
      render(<GameweekPage />);

      const selector = await screen.findByTestId('gameweek-selector');
      expect(
        within(selector).getByRole('option', { name: 'Gameweek 2 — live' })
      ).toBeInTheDocument();
      expect(within(selector).getByRole('option', { name: 'Gameweek 1' })).toBeInTheDocument();
    });

    it('walks back to the earlier gameweek', async () => {
      render(<GameweekPage />);

      await screen.findByTestId('gameweek-selector');
      await userEvent.click(screen.getByLabelText('Previous gameweek: Gameweek 1'));

      expect(window.location.pathname).toBe('/gameweek/1');
    });

    it('cannot go forward from the most recent gameweek', async () => {
      render(<GameweekPage />);

      await screen.findByTestId('gameweek-selector');
      expect(screen.getByTestId('gameweek-next')).toBeDisabled();
    });

    it('hides the selector when there is only one gameweek to see', async () => {
      vi.mocked(gameweekService.getGameweek).mockResolvedValue(
        live({
          availableGameweeks: [
            {
              gameweekNumber: 2,
              deadline: '2026-08-28T18:00:00Z',
              isLive: true,
              isComplete: false,
            },
          ],
        })
      );

      render(<GameweekPage />);

      await screen.findByTestId('gameweek-page');
      expect(screen.queryByTestId('gameweek-selector')).not.toBeInTheDocument();
    });

    it('reads a finished gameweek in the past tense', async () => {
      vi.mocked(gameweekService.getGameweek).mockResolvedValue(
        live({
          isLive: false,
          isComplete: true,
          fixturesSettled: 2,
          fixturesInPlay: 0,
          fixturesToKickOff: 0,
        })
      );

      render(<GameweekPage />);

      expect(await screen.findByText('final')).toBeInTheDocument();
      expect(screen.getByText(/All 2 matches played/)).toBeInTheDocument();
      expect(screen.getByTestId('rivals')).toHaveTextContent('You moved from 4 to 3');
      expect(screen.getByTestId('teams-spent')).toHaveTextContent('Teams you had left');
    });

    it('shows a run elimination as the record rather than a warning', async () => {
      vi.mocked(gameweekService.getGameweek).mockResolvedValue(
        live({
          isLive: false,
          isComplete: true,
          threat: {
            ...live().threat,
            danger: {
              isSettled: true,
              eliminationCount: 1,
              amIInTheZone: false,
              players: [
                {
                  userId: 'u4',
                  userName: 'Dan Evans',
                  isMe: false,
                  averagePointsPerGame: 0,
                  averageBehindSafety: 0,
                },
              ],
            },
          },
        })
      );

      render(<GameweekPage />);

      const danger = await screen.findByTestId('live-danger');
      expect(danger).toHaveTextContent('Went out after this gameweek');
      expect(danger).toHaveTextContent('it is the record, not a forecast');
      // The margin to safety is a forecast figure and means nothing once the week has been run.
      expect(danger).not.toHaveTextContent('clear of the drop');
    });
  });

  describe('outside the live window', () => {
    const closed = (overrides?: Partial<LiveGameweek>): LiveGameweek => ({
      seasonId: '2026-2027',
      isLive: false,
      isRevealed: false,
      isComplete: false,
      availableGameweeks: [],
      closedReason: 'before-deadline',
      nextDeadline: '2026-08-28T18:00:00Z',
      gameweekNumber: 3,
      fixturesTotal: 0,
      fixturesSettled: 0,
      fixturesInPlay: 0,
      fixturesToKickOff: 0,
      playersWithPick: 0,
      totalPlayers: 0,
      ownership: [],
      fixtures: [],
      threat: { rivals: [], leaders: [], sharingMyPick: 0, onDifferentPick: 0 },
      ...overrides,
    });

    it('counts down to the deadline instead of opening early', async () => {
      vi.mocked(gameweekService.getGameweek).mockResolvedValue(closed());

      render(<GameweekPage />);

      expect(await screen.findByTestId('gameweek-closed')).toHaveTextContent(
        'Gameweek 3 has not locked yet'
      );
      expect(screen.getByText('Picks close')).toBeInTheDocument();
    });

    it('reveals nothing at all about the field', async () => {
      vi.mocked(gameweekService.getGameweek).mockResolvedValue(closed());

      render(<GameweekPage />);

      await screen.findByTestId('gameweek-closed');
      expect(screen.queryByTestId('my-pick')).not.toBeInTheDocument();
      expect(screen.queryByTestId('ownership')).not.toBeInTheDocument();
      expect(screen.queryByTestId('live-fixtures')).not.toBeInTheDocument();
      expect(screen.queryByTestId('rivals')).not.toBeInTheDocument();
    });

    it('says so plainly when nothing is left to play', async () => {
      vi.mocked(gameweekService.getGameweek).mockResolvedValue(
        closed({ closedReason: 'no-gameweek', nextDeadline: null, gameweekNumber: null })
      );

      render(<GameweekPage />);

      expect(await screen.findByTestId('gameweek-closed')).toHaveTextContent(
        'No gameweek is being played'
      );
    });
  });

  it('reports a failure rather than an empty page', async () => {
    vi.mocked(gameweekService.getGameweek).mockRejectedValue(new Error('boom'));

    render(<GameweekPage />);

    expect(await screen.findByTestId('gameweek-error')).toBeInTheDocument();
  });
});
