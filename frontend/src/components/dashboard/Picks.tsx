import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { picksService } from '@/services/picks';
import { teamsService } from '@/services/teams';
import { dashboardService } from '@/services/dashboard';
import { gameweeksService } from '@/services/gameweeks';
import { leagueService } from '@/services/league';
import { useAuth } from '@/contexts/AuthContext';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
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
    mutationFn: ({ gameweekId, teamId }: { gameweekId: string; teamId: string }) =>
      picksService.createPick(user?.id || '', { gameweekId, teamId }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['picks', user?.id] });
      queryClient.invalidateQueries({ queryKey: ['dashboard', user?.id] });
      setSelectedGameweek(null);
    },
  });

  // Delete pick mutation
  const deletePickMutation = useMutation({
    mutationFn: (pickId: string) =>
      picksService.deletePick(user?.id || '', pickId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['picks', user?.id] });
      queryClient.invalidateQueries({ queryKey: ['dashboard', user?.id] });
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

  // Determine which teams have been used in each half
  const getUsedTeamsForHalf = (gameweekNumber: number): Set<string> => {
    const usedTeams = new Set<string>();
    const isFirstHalf = gameweekNumber <= 20;
    const startGw = isFirstHalf ? 1 : 21;
    const endGw = isFirstHalf ? 20 : 38;

    for (let gw = startGw; gw <= endGw; gw++) {
      const pick = picksByGameweek.get(gw);
      if (pick) {
        usedTeams.add(pick.teamId);
      }
    }

    return usedTeams;
  };

  // Get available teams for a specific gameweek
  const getAvailableTeams = (gameweekNumber: number) => {
    const usedTeams = getUsedTeamsForHalf(gameweekNumber);
    return teams.filter((team) => !usedTeams.has(team.id));
  };

  const handleTeamSelect = (gameweekNumber: number, teamId: string) => {
    const gameweekId = gameweekIdsByNumber.get(gameweekNumber);
    if (!gameweekId) {
      console.error(`No gameweek ID found for week ${gameweekNumber}`);
      return;
    }
    setSelectedGameweek(null); // Close dropdown immediately
    createPickMutation.mutate({ gameweekId, teamId });
  };

  // Generate all 38 gameweeks
  const gameweeks = Array.from({ length: 38 }, (_, i) => i + 1);

  return (
    <Card>
      <CardHeader>
        <CardTitle>Your Picks</CardTitle>
        <CardDescription>
          Select one team per gameweek - Each team can only be picked once per half
        </CardDescription>
      </CardHeader>
      <CardContent>
        <div className="rounded-md border max-h-[600px] overflow-y-auto">
          <Table>
            <TableHeader className="sticky top-0 bg-background z-10">
              <TableRow>
                <TableHead className="w-24">GW</TableHead>
                <TableHead>Team</TableHead>
                <TableHead className="text-center w-16">Pts</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {gameweeks.map((gw) => {
                const pick = picksByGameweek.get(gw);
                const deadline = gameweekDeadlinesByNumber.get(gw);
                const deadlinePassed = deadline ? new Date(deadline) < currentTime : false;
                const isSecondHalfLocked = gw > 20 && currentGameweek <= 20; // Lock second half during first half
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
                    <TableCell className="font-medium">
                      {gw}
                      {gw === currentGameweek && (
                        <span className="ml-1 text-xs text-blue-600 dark:text-blue-400">•</span>
                      )}
                    </TableCell>
                    <TableCell>
                      {pick ? (
                        <div className="flex items-center justify-between gap-2">
                          <div className={`flex items-center gap-2 ${
                            pick.points === 3
                              ? 'text-green-600 dark:text-green-400'
                              : pick.points === 1
                              ? 'text-yellow-600 dark:text-yellow-400'
                              : pick.points === 0 && deadlinePassed
                              ? 'text-red-600 dark:text-red-400'
                              : ''
                          }`}>
                            {pick.team?.logoUrl && (
                              <img
                                src={pick.team.logoUrl}
                                alt={pick.team.name}
                                className="w-5 h-5"
                              />
                            )}
                            <span>{pick.team?.name}</span>
                          </div>
                          {canRemove && (
                            <button
                              onClick={() => deletePickMutation.mutate(pick.id)}
                              disabled={deletePickMutation.isPending}
                              className="text-red-500 hover:text-red-700 dark:text-red-400 dark:hover:text-red-300 text-xs px-2 py-1 hover:bg-red-50 dark:hover:bg-red-950/30 rounded"
                              title="Remove pick"
                            >
                              ✕
                            </button>
                          )}
                        </div>
                      ) : canSelect ? (
                        isEditing ? (
                          <select
                            className="w-full border rounded px-2 py-1 text-sm"
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
                            className="text-blue-600 dark:text-blue-400 hover:underline text-sm"
                          >
                            {availableTeams.length > 0
                              ? 'Select team...'
                              : 'No teams available'}
                          </button>
                        )
                      ) : deadlinePassed ? (
                        <span className="text-gray-400 dark:text-gray-500 text-sm italic flex items-center gap-1">
                          🔒 Deadline Passed
                        </span>
                      ) : isEliminated ? (
                        <span className="text-gray-400 dark:text-gray-500 text-sm italic">🚫 Eliminated</span>
                      ) : isSecondHalfLocked ? (
                        <span className="text-gray-400 dark:text-gray-500 text-sm italic">Locked (2nd Half)</span>
                      ) : (
                        <span className="text-gray-400 dark:text-gray-500 text-sm italic">-</span>
                      )}
                    </TableCell>
                    <TableCell className="text-center font-medium">
                      {pick ? (
                        <span
                          className={
                            pick.points === 3
                              ? 'text-green-600 dark:text-green-400'
                              : pick.points === 1
                              ? 'text-yellow-600 dark:text-yellow-400'
                              : pick.points === 0 && deadlinePassed
                              ? 'text-red-600 dark:text-red-400'
                              : ''
                          }
                        >
                          {pick.points}
                        </span>
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
        <div className="mt-4 text-xs text-muted-foreground space-y-1">
          <p>• Current gameweek highlighted in blue</p>
          <p>• Picks cannot be changed after the gameweek deadline passes 🔒</p>
          <p>• Teams reset at gameweek 21 (start of second half)</p>
          <p>• Second half picks locked until gameweek 21</p>
          {isEliminated && <p className="text-red-600 dark:text-red-400">• You have been eliminated and cannot make new picks 🚫</p>}
        </div>
      </CardContent>
    </Card>
  );
}
