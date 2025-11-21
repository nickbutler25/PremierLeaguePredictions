import { useQuery } from '@tanstack/react-query';
import { dashboardService } from '@/services/dashboard';
import { useAuth } from '@/contexts/AuthContext';
import { Summary } from "@/components/dashboard/Summary";
import { Picks } from "@/components/dashboard/Picks";
import { Fixtures } from "@/components/dashboard/Fixtures";
import { LeagueStandings } from "@/components/league/LeagueStandings";
import { Card, CardContent } from "@/components/ui/card";

export function DashboardPage() {
  const { user } = useAuth();

  const { isLoading } = useQuery({
    queryKey: ['dashboard', user?.id],
    queryFn: () => dashboardService.getDashboard(user?.id || ''),
    enabled: !!user?.id,
  });

  if (isLoading) {
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

  return (
    <div className="container mx-auto p-4 space-y-4">
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
