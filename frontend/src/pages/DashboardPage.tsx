import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { AxiosError } from 'axios';
import { dashboardService } from '@/services/dashboard';
import { useAuth } from '@/contexts/AuthContext';
import { usePullToRefresh } from '@/hooks/usePullToRefresh';
import { haptics } from '@/utils/haptics';
import { Summary } from "@/components/dashboard/Summary";
import { Picks } from "@/components/dashboard/Picks";
import { Fixtures } from "@/components/dashboard/Fixtures";
import { LeagueStandings } from "@/components/league/LeagueStandings";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

export function DashboardPage() {
  const { user } = useAuth();
  const queryClient = useQueryClient();

  const { data, isLoading, error } = useQuery({
    queryKey: ['dashboard', user?.id],
    queryFn: () => dashboardService.getDashboard(user?.id || ''),
    enabled: !!user?.id,
    refetchInterval: 120000, // Refetch every 2 minutes to show live points during matches
  });

  // Pull-to-refresh functionality
  const { isPulling, pullDistance } = usePullToRefresh({
    onRefresh: async () => {
      await queryClient.invalidateQueries({ queryKey: ['dashboard'] });
      await queryClient.invalidateQueries({ queryKey: ['picks'] });
      await queryClient.invalidateQueries({ queryKey: ['league-standings'] });
      haptics.success();
    },
    enabled: !isLoading,
  });

  if (isLoading || !user) {
    return (
      <div className="container mx-auto p-4">
        <Card>
          <CardContent className="p-12">
            <div className="flex flex-col items-center justify-center space-y-4">
              <div className="animate-spin rounded-full h-16 w-16 border-b-2 border-primary"></div>
              <p className="text-lg text-muted-foreground">Loading your dashboard...</p>
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }

  // Check for API errors
  if (error) {
    const axiosError = error as AxiosError;
    const isNetworkError = !axiosError.response;
    const isServerError = axiosError.response?.status && axiosError.response.status >= 500;

    if (isNetworkError || isServerError) {
      return (
        <div className="container mx-auto p-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-destructive">Unable to Connect to Server</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <p className="text-muted-foreground">
                {isNetworkError
                  ? "We're having trouble connecting to the server. Please check your internet connection and try again."
                  : "The server is experiencing issues. Please try again in a few moments."}
              </p>
              <div className="bg-muted p-4 rounded-lg">
                <h3 className="font-semibold mb-2">Troubleshooting Steps:</h3>
                <ul className="list-disc list-inside space-y-1 text-sm text-muted-foreground">
                  <li>Check your internet connection</li>
                  <li>Refresh the page</li>
                  <li>Clear your browser cache</li>
                  {user?.isAdmin && <li>Check the server status in the admin panel</li>}
                </ul>
              </div>
              <button
                onClick={() => window.location.reload()}
                className="inline-flex items-center justify-center rounded-md text-sm font-medium ring-offset-background transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50 bg-primary text-primary-foreground hover:bg-primary/90 h-10 px-4 py-2"
              >
                Refresh Page
              </button>
            </CardContent>
          </Card>
        </div>
      );
    }
  }

  // Check if there is an active season
  const hasActiveSeason = (data?.upcomingGameweeks?.length ?? 0) > 0 || !!data?.currentGameweek;

  if (!hasActiveSeason) {
    return (
      <div className="container mx-auto p-4">
        <Card>
          <CardHeader>
            <CardTitle>No Active Season</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <p className="text-muted-foreground">
              There is currently no active season or gameweeks scheduled.
            </p>
            {user?.isAdmin && (
              <div className="bg-blue-50 dark:bg-blue-950/30 p-4 rounded-lg border border-blue-200 dark:border-blue-800">
                <h3 className="font-semibold text-blue-800 dark:text-blue-300 mb-2">Admin Action Required</h3>
                <p className="text-sm text-blue-700 dark:text-blue-400 mb-4">
                  As an admin, you can create a new season and generate gameweeks to get started.
                </p>
                <Link
                  to="/admin"
                  className="inline-flex items-center justify-center rounded-md text-sm font-medium ring-offset-background transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50 bg-primary text-primary-foreground hover:bg-primary/90 h-10 px-4 py-2"
                >
                  Go to Admin Panel
                </Link>
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="container mx-auto p-3 sm:p-4 space-y-3 sm:space-y-4">
      {/* Pull-to-Refresh Indicator */}
      {isPulling && (
        <div
          className="fixed top-0 left-0 right-0 flex items-center justify-center bg-primary/10 transition-all duration-200 z-50"
          style={{ height: `${pullDistance}px` }}
        >
          <div className="text-primary font-medium animate-pulse">
            {pullDistance >= 80 ? 'Release to refresh' : 'Pull to refresh'}
          </div>
        </div>
      )}

      {/* Summary Header */}
      <Summary />

      {/* Main Content Area: 3 Column Layout */}
      <div className="grid gap-4 grid-cols-1 md:grid-cols-2 lg:grid-cols-3">
        {/* Left Column - Picks */}
        <div>
          <Picks />
        </div>

        {/* Middle Column - Fixtures */}
        <div>
          <Fixtures />
        </div>

        {/* Right Column - League Standings */}
        <div>
          <LeagueStandings />
        </div>
      </div>
    </div>
  );
}
