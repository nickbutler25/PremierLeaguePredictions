import { useQuery } from '@tanstack/react-query';
import { useState, useEffect } from 'react';
import { dashboardService } from '@/services/dashboard';
import { leagueService } from '@/services/league';
import { useAuth } from '@/contexts/AuthContext';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Alert, AlertDescription } from '@/components/ui/alert';

export function Summary() {
  const { user } = useAuth();
  const [countdown, setCountdown] = useState<string>('');

  const { data, isLoading } = useQuery({
    queryKey: ['dashboard', user?.id],
    queryFn: () => dashboardService.getDashboard(user?.id || ''),
    enabled: !!user?.id,
  });

  // Fetch league standings to check elimination status
  const { data: leagueData } = useQuery({
    queryKey: ['league-standings'],
    queryFn: () => leagueService.getStandings(),
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

  // Check if current user is eliminated
  const currentUserStanding = leagueData?.standings.find(s => s.userId === user?.id);
  const isEliminated = currentUserStanding?.isEliminated || false;
  const eliminatedInGameweek = currentUserStanding?.eliminatedInGameweek;
  const nextGameweek = upcomingGameweeks.length > 0 ? upcomingGameweeks[0] : null;
  const isInProgress = nextGameweek?.status === 'InProgress';
  const formattedDeadline = nextGameweek
    ? new Date(nextGameweek.deadline).toLocaleDateString('en-US', {
        month: 'short',
        day: 'numeric',
        hour: 'numeric',
        minute: '2-digit',
      })
    : 'TBD';

  // Calculate countdown to deadline
  useEffect(() => {
    if (!nextGameweek || isInProgress) {
      setCountdown('');
      return;
    }

    const updateCountdown = () => {
      const now = new Date();
      const deadline = new Date(nextGameweek.deadline);
      const diff = deadline.getTime() - now.getTime();

      if (diff <= 0) {
        setCountdown('Deadline passed');
        return;
      }

      const days = Math.floor(diff / (1000 * 60 * 60 * 24));
      const hours = Math.floor((diff % (1000 * 60 * 60 * 24)) / (1000 * 60 * 60));
      const minutes = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60));

      if (days > 0) {
        setCountdown(`${days}d ${hours}h ${minutes}m`);
      } else if (hours > 0) {
        setCountdown(`${hours}h ${minutes}m`);
      } else {
        setCountdown(`${minutes}m`);
      }
    };

    // Update immediately
    updateCountdown();

    // Update every minute
    const interval = setInterval(updateCountdown, 60000);

    return () => clearInterval(interval);
  }, [nextGameweek, isInProgress]);

  return (
    <div className="space-y-3">
      {/* Elimination Warning */}
      {isEliminated && (
        <Alert className="bg-red-50 dark:bg-red-950/20 border-red-300 dark:border-red-800">
          <AlertDescription className="text-red-800 dark:text-red-200">
            <strong>⚠️ You have been eliminated from the competition.</strong>
            {eliminatedInGameweek && (
              <p className="mt-1">
                You were eliminated after Gameweek {eliminatedInGameweek}. You can still view your picks and the league standings, but you cannot make new picks.
              </p>
            )}
          </AlertDescription>
        </Alert>
      )}

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
            <p className="text-xs text-muted-foreground">
              {isInProgress ? (
                <span className="text-amber-600 dark:text-amber-400 font-medium">In Progress</span>
              ) : countdown ? (
                <span className="text-blue-600 dark:text-blue-400 font-medium">{countdown} until deadline</span>
              ) : (
                `Deadline: ${formattedDeadline}`
              )}
            </p>
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
