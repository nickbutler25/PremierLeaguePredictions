import { useQuery } from '@tanstack/react-query';
import { dashboardService } from '@/services/dashboard';
import { useAuth } from '@/contexts/AuthContext';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';

export function Summary() {
  const { user } = useAuth();

  const { data, isLoading } = useQuery({
    queryKey: ['dashboard', user?.id],
    queryFn: () => dashboardService.getDashboard(user?.id || ''),
    enabled: !!user?.id,
  });

  if (isLoading || !data) {
    return (
      <div className="grid gap-3 md:grid-cols-2 lg:grid-cols-4">
        {[1, 2, 3, 4].map((i) => (
          <Card key={i} className="py-2">
            <CardHeader className="pb-2 pt-3">
              <CardTitle className="text-xs font-medium">Loading...</CardTitle>
            </CardHeader>
            <CardContent className="pb-3">
              <div className="text-lg font-bold">--</div>
            </CardContent>
          </Card>
        ))}
      </div>
    );
  }

  const { currentGameweek, deadline, userStats } = data;
  const deadlineDate = new Date(deadline);
  const formattedDeadline = deadlineDate.toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  });

  return (
    <div className="space-y-3">
      {/* Stats Cards */}
      <div className="grid gap-3 md:grid-cols-2 lg:grid-cols-4">
        <Card className="py-2">
          <CardHeader className="pb-2 pt-3">
            <CardTitle className="text-xs font-medium">Current Gameweek</CardTitle>
          </CardHeader>
          <CardContent className="pb-3">
            <div className="text-lg font-bold">GW {currentGameweek}</div>
            <p className="text-xs text-muted-foreground">Deadline: {formattedDeadline}</p>
          </CardContent>
        </Card>

        <Card className="py-2">
          <CardHeader className="pb-2 pt-3">
            <CardTitle className="text-xs font-medium">Your Points</CardTitle>
          </CardHeader>
          <CardContent className="pb-3">
            <div className="text-lg font-bold">{userStats.points}</div>
            <p className="text-xs text-muted-foreground">
              Goal Difference: {userStats.goalDifference > 0 ? '+' : ''}
              {userStats.goalDifference}
            </p>
          </CardContent>
        </Card>

        <Card className="py-2">
          <CardHeader className="pb-2 pt-3">
            <CardTitle className="text-xs font-medium">Your Rank</CardTitle>
          </CardHeader>
          <CardContent className="pb-3">
            <div className="text-lg font-bold">{userStats.rank}rd</div>
            <p className="text-xs text-muted-foreground">Keep pushing!</p>
          </CardContent>
        </Card>

        <Card className="py-2">
          <CardHeader className="pb-2 pt-3">
            <CardTitle className="text-xs font-medium">Record</CardTitle>
          </CardHeader>
          <CardContent className="pb-3">
            <div className="text-lg font-bold">
              {userStats.won}-{userStats.drawn}-{userStats.lost}
            </div>
            <p className="text-xs text-muted-foreground">
              W-D-L • {userStats.points} pts
            </p>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
