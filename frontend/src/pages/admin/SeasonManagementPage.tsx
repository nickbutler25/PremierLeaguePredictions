import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { adminService, type TeamStatus } from '@/services/admin';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import { useToast } from '@/hooks/use-toast';

export function SeasonManagementPage() {
  const [isCreatingseason, setIsCreatingSeason] = useState(false);
  const [selectedSeasonName, setSelectedSeasonName] = useState('');
  const [maxTeamPicks, setMaxTeamPicks] = useState(1);
  const [maxOppositionTargets, setMaxOppositionTargets] = useState(1);
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const { data: seasons, isLoading: loadingSeasons } = useQuery({
    queryKey: ['admin', 'seasons'],
    queryFn: () => adminService.getSeasons(),
  });

  const { data: teams, isLoading: loadingTeams } = useQuery({
    queryKey: ['admin', 'teams'],
    queryFn: () => adminService.getTeamStatuses(),
  });

  // Generate available season options (only past and current seasons)
  const currentYear = new Date().getFullYear();
  const currentMonth = new Date().getMonth(); // 0-indexed (0 = January, 7 = August)

  // Determine the current season year (if before August, current season started last year)
  const currentSeasonYear = currentMonth < 7 ? currentYear - 1 : currentYear;

  // Generate seasons from 2020 to current season, ordered newest to oldest
  const availableSeasons = Array.from({ length: currentSeasonYear - 2019 }, (_, i) => {
    const year = currentSeasonYear - i;
    return `${year}/${year + 1}`;
  }).filter(seasonName => !seasons?.some(s => s.name === seasonName));

  const createSeasonMutation = useMutation({
    mutationFn: adminService.createSeason,
    onSuccess: async (response) => {
      toast({
        title: 'Season Created Successfully',
        description: `Teams Created: ${response.teamsCreated}, Deactivated: ${response.teamsDeactivated}, Fixtures: ${response.fixturesCreated}`,
        duration: 7000,
      });

      // Create pick rules for first half
      try {
        await adminService.createPickRule({
          seasonId: response.seasonId,
          half: 1,
          maxTimesTeamCanBePicked: maxTeamPicks,
          maxTimesOppositionCanBeTargeted: maxOppositionTargets,
        });

        // Create pick rules for second half (same as first half by default)
        await adminService.createPickRule({
          seasonId: response.seasonId,
          half: 2,
          maxTimesTeamCanBePicked: maxTeamPicks,
          maxTimesOppositionCanBeTargeted: maxOppositionTargets,
        });

        toast({
          title: 'Pick Rules Created',
          description: `Pick rules set: Teams ${maxTeamPicks}x, Opposition ${maxOppositionTargets}x per half`,
        });
      } catch (error: any) {
        toast({
          title: 'Warning',
          description: 'Season created but pick rules failed: ' + (error.response?.data?.message || error.message),
          variant: 'destructive',
        });
      }

      queryClient.invalidateQueries({ queryKey: ['admin', 'seasons'] });
      queryClient.invalidateQueries({ queryKey: ['admin', 'teams'] });
      queryClient.invalidateQueries({ queryKey: ['active-season'] });
      queryClient.invalidateQueries({ queryKey: ['season-approval'] });
      setIsCreatingSeason(false);
      setSelectedSeasonName('');
      setMaxTeamPicks(1);
      setMaxOppositionTargets(1);
    },
    onError: (error: any) => {
      toast({
        title: 'Error',
        description: error.response?.data?.message || 'Failed to create season',
        variant: 'destructive',
      });
    },
  });

  const updateTeamStatusMutation = useMutation({
    mutationFn: ({ teamId, isActive }: { teamId: number; isActive: boolean }) =>
      adminService.updateTeamStatus(teamId, isActive),
    onSuccess: () => {
      toast({
        title: 'Success',
        description: 'Team status updated',
      });
      queryClient.invalidateQueries({ queryKey: ['admin', 'teams'] });
    },
    onError: (error: any) => {
      toast({
        title: 'Error',
        description: error.response?.data?.message || 'Failed to update team status',
        variant: 'destructive',
      });
    },
  });

  const syncTeamsMutation = useMutation({
    mutationFn: adminService.syncTeams,
    onSuccess: (response) => {
      toast({
        title: 'Teams Synced Successfully',
        description: `Created: ${response.teamsCreated}, Updated: ${response.teamsUpdated}, Active: ${response.totalActiveTeams}`,
      });
      queryClient.invalidateQueries({ queryKey: ['admin', 'teams'] });
    },
    onError: (error: any) => {
      toast({
        title: 'Error',
        description: error.response?.data?.message || 'Failed to sync teams',
        variant: 'destructive',
      });
    },
  });

  const syncFixturesMutation = useMutation({
    mutationFn: (season?: number) => adminService.syncFixtures(season),
    onSuccess: (response) => {
      toast({
        title: 'Fixtures Synced Successfully',
        description: `Fixtures Created: ${response.fixturesCreated}, Updated: ${response.fixturesUpdated}, Gameweeks Created: ${response.gameweeksCreated}`,
      });
    },
    onError: (error: any) => {
      toast({
        title: 'Error',
        description: error.response?.data?.message || 'Failed to sync fixtures',
        variant: 'destructive',
      });
    },
  });

  const syncResultsMutation = useMutation({
    mutationFn: adminService.syncResults,
    onSuccess: (response) => {
      toast({
        title: 'Results Synced Successfully',
        description: `Fixtures Updated: ${response.fixturesUpdated}, Gameweeks Processed: ${response.gameweeksProcessed}, Picks Recalculated: ${response.picksRecalculated}`,
        duration: 7000,
      });
      queryClient.invalidateQueries({ queryKey: ['dashboard'] });
      queryClient.invalidateQueries({ queryKey: ['fixtures'] });
      queryClient.invalidateQueries({ queryKey: ['picks'] });
    },
    onError: (error: any) => {
      toast({
        title: 'Error',
        description: error.response?.data?.message || 'Failed to sync results',
        variant: 'destructive',
      });
    },
  });

  const handleCreateSeason = () => {
    if (!selectedSeasonName) {
      toast({
        title: 'Validation Error',
        description: 'Please select a season',
        variant: 'destructive',
      });
      return;
    }

    // Extract the starting year from the season name (e.g., "2025/2026" -> 2025)
    const startYear = parseInt(selectedSeasonName.split('/')[0]);
    const endYear = startYear + 1;

    // Create start and end dates (August 1st to May 31st)
    const startDate = new Date(Date.UTC(startYear, 7, 1)).toISOString(); // Month is 0-indexed, so 7 = August
    const endDate = new Date(Date.UTC(endYear, 4, 31)).toISOString(); // 4 = May

    createSeasonMutation.mutate({
      name: selectedSeasonName,
      startDate: startDate,
      endDate: endDate,
      externalSeasonYear: startYear,
    });
  };

  const handleToggleTeam = (team: TeamStatus) => {
    updateTeamStatusMutation.mutate({
      teamId: team.id,
      isActive: !team.isActive,
    });
  };

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div>
        <h1 className="text-3xl font-bold">Season Management</h1>
        <p className="text-muted-foreground mt-2">Manage seasons, teams, and synchronize data</p>
      </div>

      {/* Create New Season Section */}
      <Card>
        <CardHeader>
          <CardTitle>Create New Season</CardTitle>
          <CardDescription>
            Create a new season and sync teams/fixtures from the Football Data API
          </CardDescription>
        </CardHeader>
        <CardContent>
          {!isCreatingseason ? (
            <Button onClick={() => setIsCreatingSeason(true)}>Create New Season</Button>
          ) : createSeasonMutation.isPending ? (
            <div className="flex flex-col items-center justify-center p-8 space-y-4">
              <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-primary"></div>
              <div className="text-center">
                <p className="font-medium">Creating Season...</p>
                <p className="text-sm text-muted-foreground mt-1">
                  Please wait while we set up the new season.
                </p>
              </div>
            </div>
          ) : (
            <div className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="seasonName">Select Season</Label>
                <select
                  id="seasonName"
                  value={selectedSeasonName}
                  onChange={(e) => setSelectedSeasonName(e.target.value)}
                  className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  <option value="">-- Select a season --</option>
                  {availableSeasons.map((season) => (
                    <option key={season} value={season}>
                      {season}
                    </option>
                  ))}
                </select>
              </div>

              <div className="border-t pt-4 space-y-4">
                <h3 className="font-medium">Pick Rules (First Half)</h3>
                <p className="text-sm text-muted-foreground">
                  These rules will apply to both halves of the season. You can modify the second half rules later if needed.
                </p>

                <div className="space-y-2">
                  <Label htmlFor="maxTeamPicks">Max times a team can be picked per half</Label>
                  <Input
                    id="maxTeamPicks"
                    type="number"
                    min="1"
                    max="19"
                    value={maxTeamPicks}
                    onChange={(e) => setMaxTeamPicks(parseInt(e.target.value) || 1)}
                  />
                  <p className="text-xs text-muted-foreground">
                    Range: 1-19 (1 = each team once per half, 19 = allows picking every week)
                  </p>
                </div>

                <div className="space-y-2">
                  <Label htmlFor="maxOppositionTargets">Max times an opposition can be targeted per half</Label>
                  <Input
                    id="maxOppositionTargets"
                    type="number"
                    min="1"
                    max="19"
                    value={maxOppositionTargets}
                    onChange={(e) => setMaxOppositionTargets(parseInt(e.target.value) || 1)}
                  />
                  <p className="text-xs text-muted-foreground">
                    Range: 1-19 (prevents repeatedly targeting weak teams)
                  </p>
                </div>
              </div>

              <div className="flex gap-2">
                <Button
                  onClick={handleCreateSeason}
                  disabled={!selectedSeasonName}
                >
                  Create Season
                </Button>
                <Button
                  variant="outline"
                  onClick={() => {
                    setIsCreatingSeason(false);
                    setSelectedSeasonName('');
                    setMaxTeamPicks(1);
                    setMaxOppositionTargets(1);
                  }}
                >
                  Cancel
                </Button>
              </div>
              <div className="rounded-lg bg-blue-50 dark:bg-blue-950 p-4 border border-blue-200 dark:border-blue-800">
                <p className="text-sm font-medium text-blue-900 dark:text-blue-100 mb-2">
                  Steps after creating a season:
                </p>
                <ol className="text-sm text-blue-800 dark:text-blue-200 list-decimal list-inside space-y-1">
                  <li>Sync teams from the Football Data API (automatic)</li>
                  <li>Mark relegated teams as inactive in Team Status section</li>
                  <li>Sync fixtures for the new season (automatic)</li>
                  <li>Pick rules are created for both halves with your specified limits</li>
                </ol>
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Existing Seasons */}
      <Card>
        <CardHeader>
          <CardTitle>Existing Seasons</CardTitle>
          <CardDescription>View and manage all seasons</CardDescription>
        </CardHeader>
        <CardContent>
          {loadingSeasons ? (
            <p>Loading seasons...</p>
          ) : seasons && seasons.length > 0 ? (
            <div className="space-y-2">
              {seasons.map((season) => (
                <div
                  key={season.id}
                  className="flex items-center justify-between p-3 border rounded-lg"
                >
                  <div>
                    <p className="font-medium">{season.name}</p>
                    <p className="text-sm text-muted-foreground">
                      {new Date(season.startDate).toLocaleDateString()} -{' '}
                      {new Date(season.endDate).toLocaleDateString()}
                    </p>
                  </div>
                  <div className="flex items-center gap-2">
                    {season.isActive && (
                      <span className="px-2 py-1 text-xs bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200 rounded">
                        Active
                      </span>
                    )}
                    {season.isArchived && (
                      <span className="px-2 py-1 text-xs bg-gray-100 text-gray-800 dark:bg-gray-800 dark:text-gray-200 rounded">
                        Archived
                      </span>
                    )}
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <p className="text-muted-foreground">No seasons found</p>
          )}
        </CardContent>
      </Card>

      {/* Sync Operations */}
      <Card>
        <CardHeader>
          <CardTitle>Data Synchronization</CardTitle>
          <CardDescription>
            Sync teams, fixtures, and results from the Football Data API
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {(syncTeamsMutation.isPending || syncFixturesMutation.isPending || syncResultsMutation.isPending) ? (
            <div className="flex flex-col items-center justify-center p-8 space-y-4">
              <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-primary"></div>
              <div className="text-center">
                <p className="font-medium">
                  {syncTeamsMutation.isPending
                    ? 'Syncing Teams...'
                    : syncFixturesMutation.isPending
                      ? 'Syncing Fixtures...'
                      : 'Syncing Results...'}
                </p>
                <p className="text-sm text-muted-foreground mt-1">
                  This may take a few moments. Please wait...
                </p>
              </div>
            </div>
          ) : (
            <>
              <div className="flex flex-wrap gap-2">
                <Button
                  onClick={() => syncTeamsMutation.mutate()}
                  disabled={syncTeamsMutation.isPending}
                >
                  Sync Teams
                </Button>
                <Button
                  onClick={() => syncFixturesMutation.mutate(undefined)}
                  disabled={syncFixturesMutation.isPending}
                >
                  Sync Current Season Fixtures
                </Button>
                <Button
                  onClick={() => {
                    const year = prompt('Enter season year (e.g., 2025):');
                    if (year) syncFixturesMutation.mutate(parseInt(year));
                  }}
                  disabled={syncFixturesMutation.isPending}
                  variant="outline"
                >
                  Sync Specific Season
                </Button>
              </div>
              <div className="border-t pt-4">
                <h4 className="text-sm font-medium mb-2">Match Results</h4>
                <Button
                  onClick={() => syncResultsMutation.mutate()}
                  disabled={syncResultsMutation.isPending}
                  variant="default"
                  className="bg-green-600 hover:bg-green-700"
                >
                  🔄 Sync Results & Update Points
                </Button>
                <p className="text-sm text-muted-foreground mt-2">
                  Updates fixture scores and recalculates all user points for recent gameweeks.
                </p>
              </div>
              <p className="text-sm text-muted-foreground">
                Sync teams first, then sync fixtures. Gameweeks will be created automatically. Use "Sync Results" after games finish to update scores.
              </p>
            </>
          )}
        </CardContent>
      </Card>

      {/* Team Status Management */}
      <Card>
        <CardHeader>
          <CardTitle>Team Status</CardTitle>
          <CardDescription>
            Manage which teams are active. When creating a new season, mark relegated teams as inactive before syncing fixtures.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {loadingTeams ? (
            <p>Loading teams...</p>
          ) : teams && teams.length > 0 ? (
            <div className="grid gap-2 md:grid-cols-2 lg:grid-cols-3">
              {teams.map((team) => (
                <div
                  key={team.id}
                  className="flex items-center justify-between p-3 border rounded-lg"
                >
                  <div className="flex items-center gap-2">
                    {team.logoUrl && (
                      <img src={team.logoUrl} alt={team.name} className="w-6 h-6" />
                    )}
                    <span className="text-sm font-medium">{team.name}</span>
                  </div>
                  <Button
                    size="sm"
                    variant={team.isActive ? 'default' : 'outline'}
                    onClick={() => handleToggleTeam(team)}
                    disabled={updateTeamStatusMutation.isPending}
                  >
                    {team.isActive ? 'Active' : 'Inactive'}
                  </Button>
                </div>
              ))}
            </div>
          ) : (
            <p className="text-muted-foreground">
              No teams found. Sync teams from the Football Data API first.
            </p>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
