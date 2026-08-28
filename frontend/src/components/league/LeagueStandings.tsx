import { useQuery } from '@tanstack/react-query';
import type { StandingEntry } from '@/types';
import { Link } from 'react-router-dom';
import { leagueService } from '@/services/league';
import { useAuth } from '@/contexts/AuthContext';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { PickCrest } from './PickCrest';
import { FormBadge } from './FormBadge';

interface LeagueStandingsProps {
  compact?: boolean;
}

export function LeagueStandings({ compact = false }: LeagueStandingsProps) {
  const { user } = useAuth();

  const { data, isLoading, error } = useQuery({
    queryKey: ['league-standings'],
    queryFn: () => leagueService.getStandings(),
    // No polling: useResultsUpdates (mounted in Layout) invalidates this key whenever the
    // score sync reports a change, so the table refreshes on the event rather than on a timer.
    // Polling every 2 minutes from every client is also the load the server-side cache exists
    // to absorb — at a few hundred players that is a request every second or so, all day.
  });

  if (isLoading) {
    return (
      <Card data-testid="league-standings-card">
        <CardHeader>
          <CardTitle>League Standings</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-muted-foreground text-center py-8" data-testid="standings-loading">
            Loading standings...
          </p>
        </CardContent>
      </Card>
    );
  }

  if (error) {
    return (
      <Card data-testid="league-standings-card">
        <CardHeader>
          <CardTitle>League Standings</CardTitle>
        </CardHeader>
        <CardContent>
          <p
            className="text-red-600 dark:text-red-400 text-center py-8"
            data-testid="standings-error"
          >
            Failed to load standings
          </p>
        </CardContent>
      </Card>
    );
  }

  if (!data) {
    return null;
  }

  // The server attaches a current pick only once a gameweek's deadline has passed and it still
  // has a match to play — the same gameweek the dashboard calls in progress. Reading it from
  // the payload keeps the columns and the data they hold from ever disagreeing, and needs no
  // second request on the full standings route, which has no page component to thread a prop
  // through.
  const gameweekInProgress = data.standings.some((entry) => entry.currentPick);

  // Between gameweeks there is no pick to reveal, so the column would be empty in every row.
  const showPick = gameweekInProgress;

  // The full table carries the rest regardless. The compact one on the dashboard is narrow
  // enough that it has to choose: while a gameweek is being played, how everyone's goal
  // difference is moving is the live interest; between gameweeks nothing is moving, so the
  // season record is what there is to look at.
  const showRecord = !compact || !gameweekInProgress;
  const showGoalDifference = !compact || gameweekInProgress;

  // Compact columns are shown at every width — the dashboard table is only a few columns wide,
  // so there is nothing to gain by hiding them on small screens.
  const recordVisibility = compact ? '' : 'hidden sm:table-cell';
  const goalDifferenceVisibility = compact ? '' : 'hidden lg:table-cell';

  // The dashboard sits in a narrow column, and it now carries three stat columns rather than
  // one. At the full table's widths the last of them is pushed off the card, so the compact
  // table gets its own tighter set — the values are one or two characters either way.
  const positionWidth = compact ? 'w-8' : 'w-12';
  const nameWidth = compact ? 'min-w-[100px]' : 'min-w-[120px] sm:min-w-[150px]';
  const recordWidth = compact ? 'w-9' : 'w-12';
  const goalDifferenceWidth = compact ? 'w-12' : 'w-16';
  const pointsWidth = compact ? 'w-12' : 'w-16';
  const averageWidth = compact ? 'w-12' : 'w-16';

  const goalDifferenceCell = (entry: StandingEntry) => (
    <TableCell
      className={`text-center text-xs sm:text-sm ${goalDifferenceVisibility} ${
        entry.goalDifference > 0
          ? 'text-green-600 dark:text-green-400'
          : entry.goalDifference < 0
            ? 'text-red-600 dark:text-red-400'
            : ''
      }`}
      data-testid={`standing-gd-${entry.position}`}
    >
      {entry.goalDifference > 0 ? '+' : ''}
      {entry.goalDifference}
    </TableCell>
  );

  return (
    <Card data-testid="league-standings-card">
      <CardHeader className="flex flex-row items-center justify-between space-y-0">
        <CardTitle>League Standings</CardTitle>
        {compact && (
          <Link
            to="/league"
            className="text-sm text-muted-foreground hover:text-foreground transition-colors"
          >
            Full standings →
          </Link>
        )}
      </CardHeader>
      <CardContent>
        {/* The compact table trades the default cell padding for tighter columns: at the
            dashboard's width, six columns of px-4 spend more room on padding than on values
            and push the last column off the card. overflow-x-auto is the backstop for a name
            long enough to overflow anyway — scrolling beats clipping the points. */}
        <div
          className={`rounded-md border overflow-x-auto ${
            compact ? '[&_th]:px-2 [&_td]:px-2' : ''
          }`}
        >
          <Table data-testid="standings-table">
            <TableHeader>
              <TableRow>
                <TableHead className={`${positionWidth} text-center`}>#</TableHead>
                <TableHead className={nameWidth}>Name</TableHead>
                {showPick && <TableHead className="text-center w-12">Pick</TableHead>}
                {!compact && (
                  <TableHead className="text-center w-12 hidden sm:table-cell">P</TableHead>
                )}
                {showRecord && (
                  <TableHead className={`text-center ${recordWidth} ${recordVisibility}`}>
                    W
                  </TableHead>
                )}
                {showRecord && (
                  <TableHead className={`text-center ${recordWidth} ${recordVisibility}`}>
                    D
                  </TableHead>
                )}
                {showRecord && (
                  <TableHead className={`text-center ${recordWidth} ${recordVisibility}`}>
                    L
                  </TableHead>
                )}
                {/* On the dashboard GD comes before Pts so points stay the last column;
                    the full table keeps it with the other goal columns, after Pts. */}
                {showGoalDifference && compact && (
                  <TableHead
                    className={`text-center ${goalDifferenceWidth} ${goalDifferenceVisibility}`}
                  >
                    GD
                  </TableHead>
                )}
                {/* The table is ordered on this, so it has to be on screen — a table sorted
                    by a number the reader cannot see just looks wrong. */}
                <TableHead className={`text-center ${averageWidth} font-bold`}>Avg</TableHead>
                <TableHead className={`text-center ${pointsWidth}`}>Pts</TableHead>
                {!compact && (
                  <TableHead className="text-center w-16 hidden md:table-cell">GF</TableHead>
                )}
                {!compact && (
                  <TableHead className="text-center w-16 hidden md:table-cell">GA</TableHead>
                )}
                {showGoalDifference && !compact && (
                  <TableHead
                    className={`text-center ${goalDifferenceWidth} ${goalDifferenceVisibility}`}
                  >
                    GD
                  </TableHead>
                )}
                {!compact && <TableHead className="w-[200px] hidden lg:table-cell">Form</TableHead>}
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.standings
                .filter((entry) => !entry.isEliminated)
                .map((entry) => {
                  const isCurrentUser = entry.userId === user?.id;
                  return (
                    <TableRow
                      key={entry.userId}
                      data-testid={`standing-row-${entry.position}`}
                      className={
                        isCurrentUser
                          ? 'bg-blue-50 dark:bg-violet-500/10 hover:bg-blue-100 dark:hover:bg-blue-950/40'
                          : ''
                      }
                    >
                      <TableCell
                        className="text-center font-medium text-xs sm:text-sm"
                        data-testid={`standing-position-${entry.position}`}
                      >
                        {entry.position}
                      </TableCell>
                      <TableCell
                        className={`text-xs sm:text-sm ${isCurrentUser ? 'font-bold' : ''}`}
                        data-testid={`standing-name-${entry.position}`}
                      >
                        <Link
                          to={`/users/${entry.userId}`}
                          className="block sm:inline truncate max-w-[100px] sm:max-w-none hover:underline underline-offset-4"
                          data-testid={`standing-name-link-${entry.position}`}
                        >
                          {entry.userName}
                        </Link>
                        {isCurrentUser && (
                          <span
                            className="ml-1 sm:ml-2 text-xs text-blue-600 dark:text-violet-300"
                            data-testid="current-user-indicator"
                          >
                            (You)
                          </span>
                        )}
                      </TableCell>
                      {showPick && (
                        <TableCell
                          className="text-center"
                          data-testid={`standing-pick-${entry.position}`}
                        >
                          <PickCrest pick={entry.currentPick} showResultLetter />
                        </TableCell>
                      )}
                      {!compact && (
                        <TableCell
                          className="text-center text-xs sm:text-sm hidden sm:table-cell"
                          data-testid={`standing-played-${entry.position}`}
                        >
                          {entry.picksMade}
                        </TableCell>
                      )}
                      {showRecord && (
                        <TableCell
                          className={`text-center text-xs sm:text-sm ${recordVisibility} text-green-600 dark:text-green-400`}
                          data-testid={`standing-wins-${entry.position}`}
                        >
                          {entry.wins}
                        </TableCell>
                      )}
                      {showRecord && (
                        <TableCell
                          className={`text-center text-xs sm:text-sm ${recordVisibility} text-yellow-600 dark:text-yellow-400`}
                          data-testid={`standing-draws-${entry.position}`}
                        >
                          {entry.draws}
                        </TableCell>
                      )}
                      {showRecord && (
                        <TableCell
                          className={`text-center text-xs sm:text-sm ${recordVisibility} text-red-600 dark:text-red-400`}
                          data-testid={`standing-losses-${entry.position}`}
                        >
                          {entry.losses}
                        </TableCell>
                      )}
                      {showGoalDifference && compact && goalDifferenceCell(entry)}
                      <TableCell
                        className="text-center font-bold text-xs sm:text-sm tabular-nums"
                        data-testid={`standing-average-${entry.position}`}
                      >
                        {entry.averagePointsPerGame.toFixed(2)}
                      </TableCell>
                      <TableCell
                        className="text-center text-xs sm:text-sm tabular-nums"
                        data-testid={`standing-points-${entry.position}`}
                      >
                        {entry.totalPoints}
                      </TableCell>
                      {!compact && (
                        <TableCell className="text-center text-xs sm:text-sm hidden md:table-cell">
                          {entry.goalsFor}
                        </TableCell>
                      )}
                      {!compact && (
                        <TableCell className="text-center text-xs sm:text-sm hidden md:table-cell">
                          {entry.goalsAgainst}
                        </TableCell>
                      )}
                      {showGoalDifference && !compact && goalDifferenceCell(entry)}
                      {!compact && (
                        <TableCell
                          className="hidden lg:table-cell"
                          data-testid={`standing-form-${entry.position}`}
                        >
                          {entry.form && entry.form.length > 0 ? (
                            <span className="flex items-center gap-1">
                              {entry.form.map((pick) => (
                                <FormBadge key={pick.gameweekNumber} pick={pick} />
                              ))}
                            </span>
                          ) : (
                            <span className="text-xs text-muted-foreground">No results yet</span>
                          )}
                        </TableCell>
                      )}
                    </TableRow>
                  );
                })}
            </TableBody>
          </Table>
        </div>

        <div className="mt-4 text-xs sm:text-sm text-muted-foreground">
          <p className="font-semibold mb-2">Column Key:</p>
          <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-5 gap-2">
            {!compact && (
              <div className="hidden sm:block">
                <strong>P:</strong> Played
              </div>
            )}
            <div>
              <strong>Avg:</strong> Points per game &mdash; the table is ordered on this
            </div>
            {showRecord && (
              <div>
                <strong>W:</strong> Won
              </div>
            )}
            {showRecord && (
              <div>
                <strong>D:</strong> Drawn
              </div>
            )}
            {showRecord && (
              <div>
                <strong>L:</strong> Lost
              </div>
            )}
            <div>
              <strong>Pts:</strong> Points
            </div>
            {!compact && (
              <div className="hidden md:block">
                <strong>GF:</strong> Goals For
              </div>
            )}
            {!compact && (
              <div className="hidden md:block">
                <strong>GA:</strong> Goals Against
              </div>
            )}
            {showGoalDifference && (
              <div className={compact ? '' : 'hidden lg:block'}>
                <strong>GD:</strong> Goal Difference
              </div>
            )}
          </div>
          {!compact && (
            <p className="text-xs mt-2 sm:hidden">Tip: View on larger screen for more stats</p>
          )}
        </div>
      </CardContent>
    </Card>
  );
}
