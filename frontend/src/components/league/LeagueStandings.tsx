import { useQuery } from '@tanstack/react-query';
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
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';

export function LeagueStandings() {
  const { user } = useAuth();

  const { data, isLoading, error } = useQuery({
    queryKey: ['league-standings'],
    queryFn: () => leagueService.getStandings(),
    refetchInterval: 120000, // Refetch every 2 minutes to show live points during matches
  });

  if (isLoading) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>League Standings</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-muted-foreground text-center py-8">Loading standings...</p>
        </CardContent>
      </Card>
    );
  }

  if (error) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>League Standings</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-red-600 dark:text-red-400 text-center py-8">Failed to load standings</p>
        </CardContent>
      </Card>
    );
  }

  if (!data) {
    return null;
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>League Standings</CardTitle>
      </CardHeader>
      <CardContent>
        <div className="rounded-md border overflow-x-auto">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-12 text-center">#</TableHead>
                <TableHead className="min-w-[120px] sm:min-w-[150px]">Name</TableHead>
                <TableHead className="text-center w-12 hidden sm:table-cell">P</TableHead>
                <TableHead className="text-center w-12">W</TableHead>
                <TableHead className="text-center w-12">D</TableHead>
                <TableHead className="text-center w-12">L</TableHead>
                <TableHead className="text-center w-16 font-bold">PT</TableHead>
                <TableHead className="text-center w-16 hidden md:table-cell">GF</TableHead>
                <TableHead className="text-center w-16 hidden md:table-cell">GA</TableHead>
                <TableHead className="text-center w-16 hidden lg:table-cell">GD</TableHead>
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
                    className={
                      isCurrentUser
                        ? 'bg-blue-50 dark:bg-blue-950/30 hover:bg-blue-100 dark:hover:bg-blue-950/40'
                        : ''
                    }
                  >
                    <TableCell className="text-center font-medium text-xs sm:text-sm">
                      {entry.position}
                    </TableCell>
                    <TableCell className={`text-xs sm:text-sm ${isCurrentUser ? 'font-bold' : ''}`}>
                      <span className="block sm:inline truncate max-w-[100px] sm:max-w-none">
                        {entry.userName}
                      </span>
                      {isCurrentUser && (
                        <span className="ml-1 sm:ml-2 text-xs text-blue-600 dark:text-blue-400">(You)</span>
                      )}
                    </TableCell>
                    <TableCell className="text-center text-xs sm:text-sm hidden sm:table-cell">{entry.picksMade}</TableCell>
                    <TableCell className="text-center text-xs sm:text-sm text-green-600 dark:text-green-400">
                      {entry.wins}
                    </TableCell>
                    <TableCell className="text-center text-xs sm:text-sm text-yellow-600 dark:text-yellow-400">
                      {entry.draws}
                    </TableCell>
                    <TableCell className="text-center text-xs sm:text-sm text-red-600 dark:text-red-400">
                      {entry.losses}
                    </TableCell>
                    <TableCell className="text-center font-bold text-xs sm:text-sm">
                      {entry.totalPoints}
                    </TableCell>
                    <TableCell className="text-center text-xs sm:text-sm hidden md:table-cell">{entry.goalsFor}</TableCell>
                    <TableCell className="text-center text-xs sm:text-sm hidden md:table-cell">{entry.goalsAgainst}</TableCell>
                    <TableCell className={`text-center text-xs sm:text-sm hidden lg:table-cell ${
                      entry.goalDifference > 0
                        ? 'text-green-600 dark:text-green-400'
                        : entry.goalDifference < 0
                        ? 'text-red-600 dark:text-red-400'
                        : ''
                    }`}>
                      {entry.goalDifference > 0 ? '+' : ''}{entry.goalDifference}
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </div>

        <div className="mt-4 text-xs sm:text-sm text-muted-foreground">
          <p className="font-semibold mb-2">Column Key:</p>
          <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-5 gap-2">
            <div className="hidden sm:block"><strong>P:</strong> Played</div>
            <div><strong>W:</strong> Won</div>
            <div><strong>D:</strong> Drawn</div>
            <div><strong>L:</strong> Lost</div>
            <div><strong>PT:</strong> Points</div>
            <div className="hidden md:block"><strong>GF:</strong> Goals For</div>
            <div className="hidden md:block"><strong>GA:</strong> Goals Against</div>
            <div className="hidden lg:block"><strong>GD:</strong> Goal Difference</div>
          </div>
          <p className="text-xs mt-2 sm:hidden">Tip: View on larger screen for more stats</p>
        </div>
      </CardContent>
    </Card>
  );
}
