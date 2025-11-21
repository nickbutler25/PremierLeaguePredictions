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

  const { user: userStats, upcomingGameweeks } = data;
  const nextGameweek = upcomingGameweeks.length > 0 ? upcomingGameweeks[0] : null;
  const formattedDeadline = nextGameweek
    ? new Date(nextGameweek.deadline).toLocaleDateString('en-US', {
        month: 'short',
        day: 'numeric',
        hour: 'numeric',
        minute: '2-digit',
      })
    : 'TBD';

  return (
    <div className="space-y-3">
      {/* Stats Cards */}
      <div className="grid gap-3 md:grid-cols-2 lg:grid-cols-4">
        <Card className="py-2">
          <CardHeader className="pb-2 pt-3">
            <CardTitle className="text-xs font-medium">Current Gameweek</CardTitle>
          </CardHeader>
          <CardContent className="pb-3">
            <div className="text-lg font-bold">
              {nextGameweek ? `GW ${nextGameweek.weekNumber}` : 'No GW'}
            </div>
            <p className="text-xs text-muted-foreground">Deadline: {formattedDeadline}</p>
          </CardContent>
        </Card>

        <Card className="py-2">
          <CardHeader className="pb-2 pt-3">
            <CardTitle className="text-xs font-medium">Your Points</CardTitle>
          </CardHeader>
          <CardContent className="pb-3">
            <div className="text-lg font-bold">{userStats.totalPoints}</div>
            <p className="text-xs text-muted-foreground">
              {userStats.totalPicks} picks made
            </p>
          </CardContent>
        </Card>

        <Card className="py-2">
          <CardHeader className="pb-2 pt-3">
            <CardTitle className="text-xs font-medium">Your Record</CardTitle>
          </CardHeader>
          <CardContent className="pb-3">
            <div className="text-lg font-bold">
              {userStats.totalWins}-{userStats.totalDraws}-{userStats.totalLosses}
            </div>
            <p className="text-xs text-muted-foreground">W-D-L</p>
          </CardContent>
        </Card>

        <Card className="py-2">
          <CardHeader className="pb-2 pt-3">
            <CardTitle className="text-xs font-medium">Welcome</CardTitle>
          </CardHeader>
          <CardContent className="pb-3">
            <div className="text-lg font-bold">
              {userStats.firstName}
            </div>
            <p className="text-xs text-muted-foreground">Keep pushing!</p>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
