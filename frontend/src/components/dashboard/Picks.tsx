import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { picksService } from '@/services/picks';
import { teamsService } from '@/services/teams';
import { dashboardService } from '@/services/dashboard';
import { gameweeksService } from '@/services/gameweeks';
import { leagueService } from '@/services/league';
import { useAuth } from '@/contexts/AuthContext';
import { useSeasonApproval } from '@/hooks/useSeasonApproval';
import { haptics } from '@/utils/haptics';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { useState, useEffect } from 'react';

export function Picks() {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const [selectedGameweek, setSelectedGameweek] = useState<number | null>(null);
  const [currentTime, setCurrentTime] = useState(new Date());
  const { activeSeason } = useSeasonApproval();

  // Fetch data
  const { data: picks = [], isLoading: picksLoading } = useQuery({
    queryKey: ['picks', user?.id],
    queryFn: () => picksService.getPicks(user?.id || ''),
    enabled: !!user?.id,
  });

  const { data: teams = [], isLoading: teamsLoading } = useQuery({
    queryKey: ['teams'],
    queryFn: () => teamsService.getTeams(),
  });

  const { data: dashboard } = useQuery({
    queryKey: ['dashboard', user?.id],
    queryFn: () => dashboardService.getDashboard(user?.id || ''),
    enabled: !!user?.id,
  });

  const { data: allGameweeks = [] } = useQuery({
    queryKey: ['gameweeks'],
    queryFn: () => gameweeksService.getAllGameweeks(),
  });

  // Fetch pick rules for the active season
  const { data: pickRules } = useQuery({
    queryKey: ['pick-rules', activeSeason?.name],
    queryFn: () => gameweeksService.getPickRules(activeSeason?.name || ''),
    enabled: !!activeSeason?.name,
  });

  // Check if user is eliminated
  const { data: leagueData } = useQuery({
    queryKey: ['league-standings'],
    queryFn: () => leagueService.getStandings(),
  });

  const currentUserStanding = leagueData?.standings.find(s => s.userId === user?.id);
  const isEliminated = currentUserStanding?.isEliminated || false;

  // Update current time every minute to check deadlines
  useEffect(() => {
    const interval = setInterval(() => {
      setCurrentTime(new Date());
    }, 60000); // Update every minute

    return () => clearInterval(interval);
  }, []);

  // Create/update pick mutation
  const createPickMutation = useMutation({
    mutationFn: ({ gameweekNumber, teamId, seasonId }: { gameweekNumber: number; teamId: number; seasonId: string }) =>
      picksService.createPick(user?.id || '', { seasonId, gameweekNumber, teamId }),
    onSuccess: () => {
      haptics.success();
      queryClient.invalidateQueries({ queryKey: ['picks', user?.id] });
      queryClient.invalidateQueries({ queryKey: ['dashboard', user?.id] });
      setSelectedGameweek(null);
    },
    onError: (error: any) => {
      haptics.error();
      // Display error message from the backend validation
      const errorMessage = error.response?.data?.message || error.message || 'Failed to create pick';
      alert(errorMessage); // You might want to use a toast notification instead
    },
  });

  // Delete pick mutation
  const deletePickMutation = useMutation({
    mutationFn: (pickId: string) =>
      picksService.deletePick(user?.id || '', pickId),
    onSuccess: () => {
      haptics.medium();
      queryClient.invalidateQueries({ queryKey: ['picks', user?.id] });
      queryClient.invalidateQueries({ queryKey: ['dashboard', user?.id] });
    },
    onError: () => {
      haptics.error();
    },
  });

  if (picksLoading || teamsLoading) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Your Picks</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-muted-foreground text-center py-8">Loading picks...</p>
        </CardContent>
      </Card>
    );
  }

  const currentGameweek = dashboard?.upcomingGameweeks?.[0]?.weekNumber || 1;

  // Create a map of week numbers to gameweek IDs and deadlines
  const gameweekIdsByNumber = new Map<number, string>();
  const gameweekDeadlinesByNumber = new Map<number, string>();
  allGameweeks.forEach((gw: any) => {
    gameweekIdsByNumber.set(gw.weekNumber, gw.id);
    gameweekDeadlinesByNumber.set(gw.weekNumber, gw.deadline);
  });

  // Create a map of picks by gameweek number
  const picksByGameweek = new Map<number, typeof picks[0]>();
  picks.forEach((pick) => {
    if (pick.gameweekNumber) {
      picksByGameweek.set(pick.gameweekNumber, pick);
    }
  });

  // Get pick rules for the current half
  const getPickRulesForHalf = (gameweekNumber: number) => {
    const half = gameweekNumber <= 19 ? 1 : 2;
    return half === 1 ? pickRules?.firstHalf : pickRules?.secondHalf;
  };

  // Determine which teams have been used in each half
  const getUsedTeamsForHalf = (gameweekNumber: number): Map<number, number> => {
    const usedTeamsCount = new Map<number, number>();
    const half = gameweekNumber <= 19 ? 1 : 2;
    const startGw = half === 1 ? 1 : 20;
    const endGw = half === 1 ? 19 : 38;

    for (let gw = startGw; gw <= endGw; gw++) {
      const pick = picksByGameweek.get(gw);
      if (pick) {
        const count = usedTeamsCount.get(pick.teamId) || 0;
        usedTeamsCount.set(pick.teamId, count + 1);
      }
    }

    return usedTeamsCount;
  };

  // Get available teams for a specific gameweek
  const getAvailableTeams = (gameweekNumber: number) => {
    const usedTeamsCount = getUsedTeamsForHalf(gameweekNumber);
    const rules = getPickRulesForHalf(gameweekNumber);

    let availableTeams;
    if (!rules) {
      // No rules configured, fall back to "each team once per half"
      availableTeams = teams.filter((team) => !usedTeamsCount.has(team.id));
    } else {
      // Filter teams based on how many times they've been picked
      availableTeams = teams.filter((team) => {
        const timesUsed = usedTeamsCount.get(team.id) || 0;
        return timesUsed < rules.maxTimesTeamCanBePicked;
      });
    }

    // Sort teams alphabetically by name
    return availableTeams.sort((a, b) => a.name.localeCompare(b.name));
  };

  const handleTeamSelect = (gameweekNumber: number, teamIdStr: string) => {
    const teamId = parseInt(teamIdStr, 10);
    if (isNaN(teamId)) {
      console.error(`Invalid team ID: ${teamIdStr}`);
      return;
    }

    setSelectedGameweek(null); // Close dropdown immediately
    const gameweek = allGameweeks.find((gw: any) => gw.weekNumber === gameweekNumber);
    const seasonId = gameweek?.seasonId || activeSeason?.name || '2024/2025';

    createPickMutation.mutate({ gameweekNumber, teamId, seasonId });
  };

  // Generate all 38 gameweeks
  const gameweeks = Array.from({ length: 38 }, (_, i) => i + 1);

  return (
    <Card>
      <CardHeader>
        <CardTitle>Your Picks</CardTitle>
      </CardHeader>
      <CardContent>
        <div className="rounded-md border max-h-[600px] overflow-y-auto">
          <Table>
            <TableHeader className="sticky top-0 bg-background z-10">
              <TableRow>
                <TableHead className="w-16 sm:w-24 text-xs sm:text-sm">GW</TableHead>
                <TableHead className="text-xs sm:text-sm">Team</TableHead>
                <TableHead className="text-center w-12 sm:w-16 text-xs sm:text-sm">Pts</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {gameweeks.map((gw) => {
                const pick = picksByGameweek.get(gw);
                const deadline = gameweekDeadlinesByNumber.get(gw);
                const deadlinePassed = deadline ? new Date(deadline) < currentTime : false;
                const isSecondHalfLocked = gw > 19 && currentGameweek <= 19; // Lock second half during first half
                const canEdit = !deadlinePassed && !isSecondHalfLocked && !isEliminated;
                const canRemove = canEdit && pick; // Can remove if editable and has a pick
                const canSelect = canEdit && !pick; // Can select if editable and no pick
                const availableTeams = canEdit ? getAvailableTeams(gw) : [];
                const isEditing = selectedGameweek === gw;

                return (
                  <TableRow
                    key={gw}
                    className={gw === currentGameweek ? 'bg-blue-50 dark:bg-blue-950/30' : ''}
                  >
                    <TableCell className="font-medium text-xs sm:text-sm">
                      {gw}
                      {gw === currentGameweek && (
                        <span className="ml-1 text-xs text-blue-600 dark:text-blue-400">•</span>
                      )}
                    </TableCell>
                    <TableCell className="text-xs sm:text-sm">
                      {pick ? (
                        <div className="flex items-center justify-between gap-1 sm:gap-2">
                          <div className={`flex items-center gap-1.5 sm:gap-2 min-w-0 ${pick.points === 3
                            ? 'text-green-600 dark:text-green-400'
                            : pick.points === 1
                              ? 'text-yellow-600 dark:text-yellow-400'
                              : ''
                            }`}>
                            {pick.team?.logoUrl && (
                              <img
                                src={pick.team.logoUrl}
                                alt={pick.team.name}
                                className="w-4 h-4 sm:w-5 sm:h-5 flex-shrink-0"
                              />
                            )}
                            <span className="truncate">{pick.team?.name}</span>
                          </div>
                          {canRemove && (
                            <button
                              onClick={() => deletePickMutation.mutate(pick.id)}
                              disabled={deletePickMutation.isPending}
                              className="text-red-500 hover:text-red-700 dark:text-red-400 dark:hover:text-red-300 text-xs px-1.5 sm:px-2 py-1 hover:bg-red-50 dark:hover:bg-red-950/30 rounded flex-shrink-0"
                              title="Remove pick"
                            >
                              ✕
                            </button>
                          )}
                        </div>
                      ) : canSelect ? (
                        isEditing ? (
                          <select
                            className="w-full border rounded px-1.5 sm:px-2 py-1 text-xs sm:text-sm"
                            onChange={(e) => handleTeamSelect(gw, e.target.value)}
                            defaultValue=""
                            disabled={createPickMutation.isPending}
                          >
                            <option value="" disabled>
                              Select team...
                            </option>
                            {availableTeams.map((team) => (
                              <option key={team.id} value={team.id}>
                                {team.name}
                              </option>
                            ))}
                          </select>
                        ) : (
                          <button
                            onClick={() => setSelectedGameweek(gw)}
                            className="text-blue-600 dark:text-blue-400 hover:underline text-xs sm:text-sm"
                          >
                            {availableTeams.length > 0
                              ? 'Select team...'
                              : 'No teams available'}
                          </button>
                        )
                      ) : deadlinePassed ? (
                        <span className="text-gray-400 dark:text-gray-500 text-xs sm:text-sm italic flex items-center gap-1">
                          <span className="hidden sm:inline">🔒 Deadline Passed</span>
                          <span className="sm:hidden">🔒 Locked</span>
                        </span>
                      ) : isEliminated ? (
                        <span className="text-gray-400 dark:text-gray-500 text-xs sm:text-sm italic">🚫 <span className="hidden sm:inline">Eliminated</span></span>
                      ) : isSecondHalfLocked ? (
                        <span className="text-gray-400 dark:text-gray-500 text-xs sm:text-sm italic"><span className="hidden sm:inline">Locked (2nd Half)</span><span className="sm:hidden">Locked</span></span>
                      ) : (
                        <span className="text-gray-400 dark:text-gray-500 text-xs sm:text-sm italic">-</span>
                      )}
                    </TableCell>
                    <TableCell className="text-center font-medium text-xs sm:text-sm">
                      {pick ? (
                        // Check if fixture has been played by looking at whether any goals were scored
                        // or if points were awarded. If goalsFor and goalsAgainst are both 0 and points is 0,
                        // the fixture hasn't been played yet.
                        pick.goalsFor === 0 && pick.goalsAgainst === 0 && pick.points === 0 ? (
                          '-'
                        ) : (
                          <span
                            className={
                              pick.points === 3
                                ? 'text-green-600 dark:text-green-400'
                                : pick.points === 1
                                  ? 'text-yellow-600 dark:text-yellow-400'
                                  : ''
                            }
                          >
                            {pick.points}
                          </span>
                        )
                      ) : (
                        '-'
                      )}
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </div>
        <div className="mt-4 text-xs sm:text-sm text-muted-foreground space-y-1">
          <p>• Current gameweek highlighted in blue</p>
          <p>• Picks cannot be changed after deadline 🔒</p>
          {currentGameweek <= 19 ? (
            // First half - only show first half rules
            pickRules?.firstHalf && (
              <>
                <p className="hidden sm:block">• First half (GW 1-19): Each team can be picked up to {pickRules.firstHalf.maxTimesTeamCanBePicked} time{pickRules.firstHalf.maxTimesTeamCanBePicked > 1 ? 's' : ''}</p>
                <p className="sm:hidden">• Each team max {pickRules.firstHalf.maxTimesTeamCanBePicked}x (1st half)</p>
                <p className="hidden sm:block">• First half (GW 1-19): Each opposition can be targeted up to {pickRules.firstHalf.maxTimesOppositionCanBeTargeted} time{pickRules.firstHalf.maxTimesOppositionCanBeTargeted > 1 ? 's' : ''}</p>
                <p className="sm:hidden">• Each opposition max {pickRules.firstHalf.maxTimesOppositionCanBeTargeted}x (1st half)</p>
                <p>• Second half picks locked until GW 20</p>
              </>
            )
          ) : (
            // Second half - only show second half rules
            pickRules?.secondHalf && (
              <>
                <p className="hidden sm:block">• Second half (GW 20-38): Each team can be picked up to {pickRules.secondHalf.maxTimesTeamCanBePicked} time{pickRules.secondHalf.maxTimesTeamCanBePicked > 1 ? 's' : ''}</p>
                <p className="sm:hidden">• Each team max {pickRules.secondHalf.maxTimesTeamCanBePicked}x (2nd half)</p>
                <p className="hidden sm:block">• Second half (GW 20-38): Each opposition can be targeted up to {pickRules.secondHalf.maxTimesOppositionCanBeTargeted} time{pickRules.secondHalf.maxTimesOppositionCanBeTargeted > 1 ? 's' : ''}</p>
                <p className="sm:hidden">• Each opposition max {pickRules.secondHalf.maxTimesOppositionCanBeTargeted}x (2nd half)</p>
              </>
            )
          )}
          {isEliminated && <p className="text-red-600 dark:text-red-400">• You are eliminated 🚫</p>}
        </div>
      </CardContent>
    </Card>
  );
}
