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
        <CardDescription>
          Current season rankings - Updated after each gameweek
        </CardDescription>
      </CardHeader>
      <CardContent>
        <div className="rounded-md border overflow-x-auto">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-12 text-center">#</TableHead>
                <TableHead className="min-w-[150px]">Name</TableHead>
                <TableHead className="text-center w-12">P</TableHead>
                <TableHead className="text-center w-12">W</TableHead>
                <TableHead className="text-center w-12">D</TableHead>
                <TableHead className="text-center w-12">L</TableHead>
                <TableHead className="text-center w-16 font-bold">PT</TableHead>
                <TableHead className="text-center w-16">GF</TableHead>
                <TableHead className="text-center w-16">GA</TableHead>
                <TableHead className="text-center w-16">GD</TableHead>
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
                    <TableCell className="text-center font-medium">
                      {entry.position}
                    </TableCell>
                    <TableCell className={isCurrentUser ? 'font-bold' : ''}>
                      {entry.userName}
                      {isCurrentUser && (
                        <span className="ml-2 text-xs text-blue-600 dark:text-blue-400">(You)</span>
                      )}
                    </TableCell>
                    <TableCell className="text-center">{entry.picksMade}</TableCell>
                    <TableCell className="text-center text-green-600 dark:text-green-400">
                      {entry.wins}
                    </TableCell>
                    <TableCell className="text-center text-yellow-600 dark:text-yellow-400">
                      {entry.draws}
                    </TableCell>
                    <TableCell className="text-center text-red-600 dark:text-red-400">
                      {entry.losses}
                    </TableCell>
                    <TableCell className="text-center font-bold">
                      {entry.totalPoints}
                    </TableCell>
                    <TableCell className="text-center">{entry.goalsFor}</TableCell>
                    <TableCell className="text-center">{entry.goalsAgainst}</TableCell>
                    <TableCell className={`text-center ${
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

        <div className="mt-4 text-sm text-muted-foreground">
          <p className="font-semibold mb-2">Column Key:</p>
          <div className="grid grid-cols-2 md:grid-cols-5 gap-2">
            <div><strong>P:</strong> Played</div>
            <div><strong>W:</strong> Won</div>
            <div><strong>D:</strong> Drawn</div>
            <div><strong>L:</strong> Lost</div>
            <div><strong>PT:</strong> Points</div>
            <div><strong>GF:</strong> Goals For</div>
            <div><strong>GA:</strong> Goals Against</div>
            <div><strong>GD:</strong> Goal Difference</div>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
