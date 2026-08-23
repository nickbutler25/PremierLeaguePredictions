import { useQuery } from '@tanstack/react-query';
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
        <div className={`rounded-md border ${compact ? '' : 'overflow-x-auto'}`}>
          <Table data-testid="standings-table">
            <TableHeader>
              <TableRow>
                <TableHead className="w-12 text-center">#</TableHead>
                <TableHead className="min-w-[120px] sm:min-w-[150px]">Name</TableHead>
                <TableHead className="text-center w-12">Pick</TableHead>
                {!compact && (
                  <TableHead className="text-center w-12 hidden sm:table-cell">P</TableHead>
                )}
                {!compact && (
                  <TableHead className="text-center w-12 hidden sm:table-cell">W</TableHead>
                )}
                {!compact && (
                  <TableHead className="text-center w-12 hidden sm:table-cell">D</TableHead>
                )}
                {!compact && (
                  <TableHead className="text-center w-12 hidden sm:table-cell">L</TableHead>
                )}
                <TableHead className="text-center w-16 font-bold">PT</TableHead>
                {!compact && (
                  <TableHead className="text-center w-16 hidden md:table-cell">GF</TableHead>
                )}
                {!compact && (
                  <TableHead className="text-center w-16 hidden md:table-cell">GA</TableHead>
                )}
                {!compact && (
                  <TableHead className="text-center w-16 hidden lg:table-cell">GD</TableHead>
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
                          ? 'bg-blue-50 dark:bg-blue-950/30 hover:bg-blue-100 dark:hover:bg-blue-950/40'
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
                        <span className="block sm:inline truncate max-w-[100px] sm:max-w-none">
                          {entry.userName}
                        </span>
                        {isCurrentUser && (
                          <span
                            className="ml-1 sm:ml-2 text-xs text-blue-600 dark:text-blue-400"
                            data-testid="current-user-indicator"
                          >
                            (You)
                          </span>
                        )}
                      </TableCell>
                      <TableCell
                        className="text-center"
                        data-testid={`standing-pick-${entry.position}`}
                      >
                        <PickCrest pick={entry.currentPick} showResultLetter />
                      </TableCell>
                      {!compact && (
                        <TableCell
                          className="text-center text-xs sm:text-sm hidden sm:table-cell"
                          data-testid={`standing-played-${entry.position}`}
                        >
                          {entry.picksMade}
                        </TableCell>
                      )}
                      {!compact && (
                        <TableCell
                          className="text-center text-xs sm:text-sm hidden sm:table-cell text-green-600 dark:text-green-400"
                          data-testid={`standing-wins-${entry.position}`}
                        >
                          {entry.wins}
                        </TableCell>
                      )}
                      {!compact && (
                        <TableCell
                          className="text-center text-xs sm:text-sm hidden sm:table-cell text-yellow-600 dark:text-yellow-400"
                          data-testid={`standing-draws-${entry.position}`}
                        >
                          {entry.draws}
                        </TableCell>
                      )}
                      {!compact && (
                        <TableCell
                          className="text-center text-xs sm:text-sm hidden sm:table-cell text-red-600 dark:text-red-400"
                          data-testid={`standing-losses-${entry.position}`}
                        >
                          {entry.losses}
                        </TableCell>
                      )}
                      <TableCell
                        className="text-center font-bold text-xs sm:text-sm"
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
                      {!compact && (
                        <TableCell
                          className={`text-center text-xs sm:text-sm hidden lg:table-cell ${
                            entry.goalDifference > 0
                              ? 'text-green-600 dark:text-green-400'
                              : entry.goalDifference < 0
                                ? 'text-red-600 dark:text-red-400'
                                : ''
                          }`}
                        >
                          {entry.goalDifference > 0 ? '+' : ''}
                          {entry.goalDifference}
                        </TableCell>
                      )}
                      {!compact && (
                        <TableCell
                          className="hidden lg:table-cell"
                          data-testid={`standing-form-${entry.position}`}
                        >
                          {entry.form && entry.form.length > 0 ? (
                            <span className="flex items-center gap-1">
                              {entry.form.map((pick) => (
                                <PickCrest
                                  key={pick.gameweekNumber}
                                  pick={pick}
                                  size="sm"
                                  showGameweek
                                />
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
            <div className="hidden sm:block">
              <strong>P:</strong> Played
            </div>
            <div>
              <strong>W:</strong> Won
            </div>
            <div>
              <strong>D:</strong> Drawn
            </div>
            <div>
              <strong>L:</strong> Lost
            </div>
            <div>
              <strong>PT:</strong> Points
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
            {!compact && (
              <div className="hidden lg:block">
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
