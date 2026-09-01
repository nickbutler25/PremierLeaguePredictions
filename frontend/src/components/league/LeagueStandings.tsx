import { useQuery } from '@tanstack/react-query';
import { useEffect, useRef, useState } from 'react';
import type { StandingEntry } from '@/types';
import { Link } from 'react-router-dom';
import { leagueService } from '@/services/league';
import { useAuth } from '@/contexts/AuthContext';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
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

  const [nameFilter, setNameFilter] = useState('');
  const myRowRef = useRef<HTMLTableRowElement>(null);
  const scrollerRef = useRef<HTMLDivElement>(null);

  // Shorter on the dashboard purely for room: the card is a third of the width and the header
  // has to carry the controls beside it. The card sits under a "League" heading in context
  // anyway, so nothing is lost.
  const title = compact ? 'Standings' : 'League Standings';

  // Jumping happens in an effect rather than in the click handler because the click also clears
  // the filter: if the player is currently filtered out, their row does not exist in the DOM yet
  // and there is nothing to scroll to. The effect runs after React has committed the cleared
  // filter, by which point the row is there.
  const [pendingJump, setPendingJump] = useState(false);
  useEffect(() => {
    if (!pendingJump) return;
    myRowRef.current?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    setPendingJump(false);
  }, [pendingJump]);

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

  // Eliminated players are not in the table, so a player who is out has no row to jump to and
  // gets no button. Their season lives on their player page instead.
  const listed = data.standings.filter((entry) => !entry.isEliminated);
  const meIsListed = listed.some((entry) => entry.userId === user?.id);

  const query = nameFilter.trim().toLowerCase();
  const visible = query
    ? listed.filter((entry) => entry.userName.toLowerCase().includes(query))
    : listed;

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
    // The dashboard card fills the height it is given rather than setting its own. DashboardPage
    // lifts it out of the grid flow from md up so that height comes from Picks and Fixtures; here
    // the card just has to stretch to it and let the table scroll inside.
    <Card
      data-testid="league-standings-card"
      className={compact ? 'flex flex-col h-full' : undefined}
    >
      <CardHeader className="space-y-3 shrink-0">
        <div className="flex flex-row items-center justify-between gap-2 space-y-0">
          <CardTitle>{title}</CardTitle>
          {compact && (
            <div className="flex items-center gap-1.5">
              {/* The card scrolls its own rows, so once you have jumped to yourself there is no
                  page scroll to get you back — this returns the table to first place. */}
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => scrollerRef.current?.scrollTo({ top: 0, behavior: 'smooth' })}
                data-testid="standings-scroll-top"
                className="h-7 px-2 text-xs whitespace-nowrap"
              >
                Top
              </Button>
              {meIsListed && (
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => setPendingJump(true)}
                  data-testid="standings-jump-to-me-compact"
                  className="h-7 px-2 text-xs whitespace-nowrap"
                >
                  Jump to me
                </Button>
              )}
              <Button
                asChild
                variant="outline"
                size="sm"
                className="h-7 px-2 text-xs whitespace-nowrap"
              >
                <Link to="/league">Full Table</Link>
              </Button>
            </div>
          )}
        </div>

        {/* At a few hundred players, scrolling to find a name is the slow way round. The filter
            runs over the rows already in hand, so it needs no request and no debounce. */}
        {!compact && (
          <div className="flex flex-col sm:flex-row gap-2 sm:items-center">
            <Input
              type="search"
              value={nameFilter}
              onChange={(e) => setNameFilter(e.target.value)}
              placeholder="Filter by name"
              aria-label="Filter standings by player name"
              data-testid="standings-filter"
              className="h-9 sm:max-w-xs"
            />
            {meIsListed && (
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => {
                  // Clearing first, because jumping to a row the filter is hiding cannot work.
                  setNameFilter('');
                  setPendingJump(true);
                }}
                data-testid="standings-jump-to-me"
                className="h-9 shrink-0"
              >
                Jump to me
              </Button>
            )}
            {query && (
              <span
                className="text-sm text-muted-foreground"
                data-testid="standings-filter-count"
                role="status"
              >
                {visible.length} of {listed.length}
              </span>
            )}
          </div>
        )}
      </CardHeader>
      <CardContent className={compact ? 'flex flex-col flex-1 min-h-0' : undefined}>
        {/* The compact table trades the default cell padding for tighter columns: at the
            dashboard's width, six columns of px-4 spend more room on padding than on values
            and push the last column off the card. overflow-x-auto is the backstop for a name
            long enough to overflow anyway — scrolling beats clipping the points. */}
        {/* The dashboard card scrolls its own rows rather than growing to fit them: at a few
            hundred players an uncapped table makes that column several screens taller than
            Picks and Fixtures beside it.

            From md up the card is given a height by DashboardPage, so flex-1 takes whatever is
            left after the header and the key. min-h-0 is what allows it to shrink below its
            content; without it a flex child refuses to and the scrollbar never appears. Below md
            there is a single column and nothing to match, so the max-height is the cap. */}
        <div
          ref={scrollerRef}
          className={`rounded-md border overflow-x-auto ${
            compact
              ? '[&_th]:px-2 [&_td]:px-2 overflow-y-auto max-h-[26rem] md:max-h-none md:flex-1 md:min-h-0'
              : ''
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
                <TableHead className={`text-center ${pointsWidth} font-bold`}>Pts</TableHead>
                {/* Points per game is still the first tiebreak in the ordering chain, but it is
                    not shown: it only ever moves players level on points, and a column that is
                    identical down the whole table on a normal week reads as noise. */}
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
              {visible.map((entry) => {
                  const isCurrentUser = entry.userId === user?.id;
                  return (
                    <TableRow
                      key={entry.userId}
                      ref={isCurrentUser ? myRowRef : undefined}
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

          {visible.length === 0 && (
            <p
              className="text-sm text-muted-foreground text-center py-8"
              data-testid="standings-no-matches"
            >
              No players match &ldquo;{nameFilter.trim()}&rdquo;.
            </p>
          )}
        </div>

        <div className="mt-4 text-xs sm:text-sm text-muted-foreground shrink-0">
          <p className="font-semibold mb-2">Column Key:</p>
          <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-5 gap-2">
            {!compact && (
              <div className="hidden sm:block">
                <strong>P:</strong> Played
              </div>
            )}
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
