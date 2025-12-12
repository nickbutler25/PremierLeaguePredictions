import { useQuery } from '@tanstack/react-query';
import { fixturesService } from '@/services/fixtures';
import { picksService } from '@/services/picks';
import { dashboardService } from '@/services/dashboard';
import { useAuth } from '@/contexts/AuthContext';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { useState } from 'react';
import type { Fixture } from '@/types';

export function Fixtures() {
  const { user } = useAuth();
  const [selectedGameweek, setSelectedGameweek] = useState<number | null>(null);

  // Fetch data
  const { data: fixtures = [], isLoading: fixturesLoading } = useQuery({
    queryKey: ['fixtures'],
    queryFn: () => fixturesService.getFixtures(),
  });

  const { data: picks = [] } = useQuery({
    queryKey: ['picks', user?.id],
    queryFn: () => picksService.getPicks(user?.id || ''),
    enabled: !!user?.id,
  });

  const { data: dashboard } = useQuery({
    queryKey: ['dashboard'],
    queryFn: () => dashboardService.getDashboard(''),
  });

  const currentGameweek = dashboard?.upcomingGameweeks?.[0]?.weekNumber || 1;
  const displayGameweek = selectedGameweek || currentGameweek;

  // Create a map of picks by gameweek number
  const picksByGameweek = new Map<number, typeof picks[0]>();
  picks.forEach((pick) => {
    if (pick.gameweekNumber) {
      picksByGameweek.set(pick.gameweekNumber, pick);
    }
  });

  // Group fixtures by gameweek
  const fixturesByGameweek = new Map<number, Fixture[]>();
  fixtures.forEach((fixture) => {
    const gameweekNumber = fixture.gameweekNumber || 1;
    if (!fixturesByGameweek.has(gameweekNumber)) {
      fixturesByGameweek.set(gameweekNumber, []);
    }
    fixturesByGameweek.get(gameweekNumber)?.push(fixture);
  });

  // Sort fixtures within each gameweek by kickoff time
  fixturesByGameweek.forEach((fixtures) => {
    fixtures.sort((a, b) => new Date(a.kickoffTime).getTime() - new Date(b.kickoffTime).getTime());
  });

  // Get color for a team based on pick status
  // Get color for a team based on pick status
  const getTeamColor = (teamId: number, fixtureGameweek: number): string => {
    const pick = picksByGameweek.get(fixtureGameweek);

    // Green: Selected in this gameweek
    if (pick?.teamId === teamId) {
      return 'text-green-600 dark:text-green-400 font-semibold';
    }

    // Check if team is picked in any other gameweek
    for (const [gw, gwPick] of picksByGameweek.entries()) {
      if (gwPick.teamId === teamId && gw !== fixtureGameweek) {
        // Red: Picked in a past gameweek
        if (gw < fixtureGameweek) {
          return 'text-red-600 dark:text-red-400';
        }
        // Grey: Picked in a future gameweek
        if (gw > fixtureGameweek) {
          return 'text-gray-400 dark:text-gray-500';
        }
      }
    }

    return ''; // No special color
  };

  // Format time
  const formatKickoffTime = (kickoffTime: string) => {
    const date = new Date(kickoffTime);
    return date.toLocaleTimeString('en-US', {
      hour: 'numeric',
      minute: '2-digit',
      hour12: true
    });
  };

  const formatKickoffDate = (kickoffTime: string) => {
    const date = new Date(kickoffTime);
    return date.toLocaleDateString('en-US', {
      weekday: 'short',
      month: 'short',
      day: 'numeric'
    });
  };

  if (fixturesLoading) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Fixtures</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-muted-foreground text-center py-8">Loading fixtures...</p>
        </CardContent>
      </Card>
    );
  }

  const displayFixtures = fixturesByGameweek.get(displayGameweek) || [];
  const availableGameweeks = Array.from(fixturesByGameweek.keys()).sort((a, b) => a - b);

  return (
    <Card>
      <CardHeader>
        <CardTitle>Fixtures</CardTitle>
        <CardDescription>
          <span className="text-green-600 dark:text-green-400">●</span> Your pick{' '}
          <span className="text-red-600 dark:text-red-400">●</span> Already picked{' '}
          <span className="text-gray-400 dark:text-gray-500">●</span> Future pick
        </CardDescription>
      </CardHeader>
      <CardContent>
        {/* Gameweek Navigation */}
        <div className="flex items-center justify-between mb-4 pb-3 border-b">
          <button
            onClick={() => {
              const currentIndex = availableGameweeks.indexOf(displayGameweek);
              if (currentIndex > 0) {
                setSelectedGameweek(availableGameweeks[currentIndex - 1]);
              }
            }}
            disabled={availableGameweeks.indexOf(displayGameweek) === 0}
            className="px-3 py-1 text-sm border rounded hover:bg-gray-50 dark:hover:bg-gray-800 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            ← Prev
          </button>

          <div className="text-center">
            <div className="font-bold">Gameweek {displayGameweek}</div>
            {displayGameweek === currentGameweek && (
              <div className="text-xs text-blue-600 dark:text-blue-400">Current</div>
            )}
          </div>

          <button
            onClick={() => {
              const currentIndex = availableGameweeks.indexOf(displayGameweek);
              if (currentIndex < availableGameweeks.length - 1) {
                setSelectedGameweek(availableGameweeks[currentIndex + 1]);
              }
            }}
            disabled={availableGameweeks.indexOf(displayGameweek) === availableGameweeks.length - 1}
            className="px-3 py-1 text-sm border rounded hover:bg-gray-50 dark:hover:bg-gray-800 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            Next →
          </button>
        </div>

        {/* Fixtures List */}
        <div className="space-y-3">
          {displayFixtures.length === 0 ? (
            <p className="text-muted-foreground text-center py-8">
              No fixtures available for this gameweek
            </p>
          ) : (
            displayFixtures.map((fixture) => {
              const homeColor = getTeamColor(fixture.homeTeamId, displayGameweek);
              const awayColor = getTeamColor(fixture.awayTeamId, displayGameweek);

              return (
                <div
                  key={fixture.id}
                  className="border rounded-lg p-3 hover:bg-gray-50 dark:hover:bg-gray-800 transition-colors"
                >
                  <div className="flex items-center justify-between gap-2">
                    {/* Home Team */}
                    <div className={`flex items-center gap-1.5 sm:gap-2 flex-1 min-w-0 ${homeColor}`}>
                      {fixture.homeTeam?.logoUrl && (
                        <img
                          src={fixture.homeTeam.logoUrl}
                          alt={fixture.homeTeam.name}
                          className="w-5 h-5 sm:w-6 sm:h-6 flex-shrink-0"
                        />
                      )}
                      <span className="text-xs sm:text-sm font-medium truncate">
                        {fixture.homeTeam?.name}
                      </span>
                    </div>

                    {/* Score or Time */}
                    <div className="px-2 sm:px-4 text-center min-w-[70px] sm:min-w-[80px] flex-shrink-0">
                      {fixture.status === 'FINISHED' ? (
                        <div className="font-bold text-sm">
                          {fixture.homeScore} - {fixture.awayScore}
                        </div>
                      ) : fixture.status === 'IN_PLAY' ? (
                        <div className="text-xs">
                          <div className="text-red-600 dark:text-red-400 font-semibold">LIVE</div>
                          <div className="font-bold">
                            {fixture.homeScore} - {fixture.awayScore}
                          </div>
                        </div>
                      ) : fixture.status === 'PAUSED' ? (
                        <div className="text-xs">
                          <div className="text-orange-600 dark:text-orange-400 font-semibold">HT</div>
                          <div className="font-bold">
                            {fixture.homeScore} - {fixture.awayScore}
                          </div>
                        </div>
                      ) : (
                        <div className="text-xs text-muted-foreground">
                          <div className="hidden sm:block">{formatKickoffDate(fixture.kickoffTime)}</div>
                          <div className="font-medium">{formatKickoffTime(fixture.kickoffTime)}</div>
                        </div>
                      )}
                    </div>

                    {/* Away Team */}
                    <div className={`flex items-center gap-1.5 sm:gap-2 flex-1 min-w-0 justify-end ${awayColor}`}>
                      <span className="text-xs sm:text-sm font-medium text-right truncate">
                        {fixture.awayTeam?.name}
                      </span>
                      {fixture.awayTeam?.logoUrl && (
                        <img
                          src={fixture.awayTeam.logoUrl}
                          alt={fixture.awayTeam.name}
                          className="w-5 h-5 sm:w-6 sm:h-6 flex-shrink-0"
                        />
                      )}
                    </div>
                  </div>
                </div>
              );
            })
          )}
        </div>
      </CardContent>
    </Card>
  );
}
