import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { adminService, type TeamStatus } from '@/services/admin';
import { teamsService } from '@/services/teams';
import { usersService } from '@/services/users';
import { useAuth } from '@/contexts/AuthContext';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { useToast } from '@/hooks/use-toast';

type TabType = 'seasons' | 'backfill';

export function SeasonManagementPage() {
  const [activeTab, setActiveTab] = useState<TabType>('seasons');
  const [isCreatingseason, setIsCreatingSeason] = useState(false);
  const [selectedSeasonName, setSelectedSeasonName] = useState('');
  const { user } = useAuth();
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

  // Backfill picks state and queries
  const [selectedUserId, setSelectedUserId] = useState<string>('');
  const [picks, setPicks] = useState<Array<{ gameweekNumber: number; teamId: string }>>(
    Array.from({ length: 11 }, (_, i) => ({ gameweekNumber: i + 1, teamId: '' }))
  );

  const { data: allTeams = [] } = useQuery({
    queryKey: ['teams'],
    queryFn: () => teamsService.getTeams(),
    enabled: activeTab === 'backfill',
  });

  const { data: users = [] } = useQuery({
    queryKey: ['users'],
    queryFn: () => usersService.getUsers(),
    enabled: activeTab === 'backfill',
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
    onSuccess: (response) => {
      toast({
        title: 'Season Created Successfully',
        description: `Teams Created: ${response.teamsCreated}, Deactivated: ${response.teamsDeactivated}, Fixtures: ${response.fixturesCreated}`,
        duration: 7000,
      });
      queryClient.invalidateQueries({ queryKey: ['admin', 'seasons'] });
      queryClient.invalidateQueries({ queryKey: ['admin', 'teams'] });
      setIsCreatingSeason(false);
      setSelectedSeasonName('');
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
    mutationFn: ({ teamId, isActive }: { teamId: string; isActive: boolean }) =>
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

  // Backfill picks mutation and handlers
  const backfillMutation = useMutation({
    mutationFn: () => {
      const userId = selectedUserId || user?.id;
      if (!userId) throw new Error('No user selected');
      const validPicks = picks.filter(p => p.teamId !== '');
      return adminService.backfillPicks(userId, validPicks);
    },
    onSuccess: (response) => {
      console.log('Backfill response:', response);

      const message = response.picksSkipped > 0
        ? `Created: ${response.picksCreated}, Updated: ${response.picksUpdated}, Skipped: ${response.picksSkipped} (gameweeks may not exist yet)`
        : `Created: ${response.picksCreated}, Updated: ${response.picksUpdated}`;

      toast({
        title: 'Picks Backfilled Successfully',
        description: message,
        duration: 7000,
      });
      queryClient.invalidateQueries({ queryKey: ['picks'] });
      // Reset form
      setSelectedUserId('');
      setPicks(Array.from({ length: 11 }, (_, i) => ({ gameweekNumber: i + 1, teamId: '' })));
    },
    onError: (error: any) => {
      const errorMessage = error.response?.data?.message
        || error.response?.data?.title
        || error.message
        || 'Failed to backfill picks';

      toast({
        title: 'Error Backfilling Picks',
        description: errorMessage,
        variant: 'destructive',
        duration: 7000,
      });
    },
  });

  const handlePickChange = (index: number, teamId: string) => {
    const newPicks = [...picks];
    newPicks[index] = { ...newPicks[index], teamId };
    setPicks(newPicks);
  };

  const handleBackfillSubmit = (e: React.FormEvent) => {
    e.preventDefault();

    const userId = selectedUserId || user?.id;
    if (!userId) {
      toast({
        title: 'Validation Error',
        description: 'Please select a user',
        variant: 'destructive',
      });
      return;
    }

    const validPicks = picks.filter(p => p.teamId !== '');
    if (validPicks.length === 0) {
      toast({
        title: 'Validation Error',
        description: 'Please select at least one team',
        variant: 'destructive',
      });
      return;
    }

    backfillMutation.mutate();
  };

  return (
    <div className="container mx-auto p-6 space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-3xl font-bold">Admin Panel</h1>
      </div>

      {/* Tab Navigation */}
      <div className="flex gap-2 border-b">
        <button
          onClick={() => setActiveTab('seasons')}
          className={`px-4 py-2 font-medium transition-colors ${
            activeTab === 'seasons'
              ? 'text-primary border-b-2 border-primary'
              : 'text-muted-foreground hover:text-foreground'
          }`}
        >
          Season Management
        </button>
        <button
          onClick={() => setActiveTab('backfill')}
          className={`px-4 py-2 font-medium transition-colors ${
            activeTab === 'backfill'
              ? 'text-primary border-b-2 border-primary'
              : 'text-muted-foreground hover:text-foreground'
          }`}
        >
          Backfill Picks
        </button>
      </div>

      {activeTab === 'seasons' && (
        <div className="space-y-6">{renderSeasonManagement()}</div>
      )}

      {activeTab === 'backfill' && (
        <div className="space-y-6">{renderBackfillPicks()}</div>
      )}
    </div>
  );

  function renderSeasonManagement() {
    return (
      <>

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
                  <li>Sync teams from the Football Data API</li>
                  <li>Mark relegated teams as inactive in the Team Status section below</li>
                  <li>Sync fixtures for the new season</li>
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
            Sync teams and fixtures from the Football Data API
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {(syncTeamsMutation.isPending || syncFixturesMutation.isPending) ? (
            <div className="flex flex-col items-center justify-center p-8 space-y-4">
              <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-primary"></div>
              <div className="text-center">
                <p className="font-medium">
                  {syncTeamsMutation.isPending ? 'Syncing Teams...' : 'Syncing Fixtures...'}
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
              <p className="text-sm text-muted-foreground">
                Sync teams first, then sync fixtures. Gameweeks will be created automatically.
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
      </>
    );
  }

  function renderBackfillPicks() {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Backfill Gameweeks 1-11</CardTitle>
          <CardDescription>
            Select team picks for the first 11 gameweeks. Points will be automatically calculated based on results.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleBackfillSubmit} className="space-y-4">
            {/* User Selection */}
            <div className="space-y-2">
              <Label htmlFor="user-select">Select User</Label>
              <select
                id="user-select"
                value={selectedUserId}
                onChange={(e) => setSelectedUserId(e.target.value)}
                className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
              >
                <option value="">-- Select a user --</option>
                {users.map((u) => (
                  <option key={u.id} value={u.id}>
                    {u.firstName} {u.lastName} ({u.email})
                  </option>
                ))}
              </select>
            </div>

            <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
              {picks.map((pick, index) => (
                <div key={pick.gameweekNumber} className="space-y-2">
                  <Label htmlFor={`gw-${pick.gameweekNumber}`}>
                    Gameweek {pick.gameweekNumber}
                  </Label>
                  <select
                    id={`gw-${pick.gameweekNumber}`}
                    value={pick.teamId}
                    onChange={(e) => handlePickChange(index, e.target.value)}
                    className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    <option value="">-- Select a team --</option>
                    {allTeams.map((team) => (
                      <option key={team.id} value={team.id}>
                        {team.name}
                      </option>
                    ))}
                  </select>
                </div>
              ))}
            </div>

            <div className="flex gap-2 pt-4">
              <Button
                type="submit"
                disabled={backfillMutation.isPending}
              >
                {backfillMutation.isPending ? 'Backfilling...' : 'Backfill Picks'}
              </Button>
            </div>

            <div className="rounded-lg bg-blue-50 dark:bg-blue-950 p-4 border border-blue-200 dark:border-blue-800">
              <p className="text-sm font-medium text-blue-900 dark:text-blue-100 mb-2">
                Important Notes:
              </p>
              <ul className="text-sm text-blue-800 dark:text-blue-200 list-disc list-inside space-y-1">
                <li>You can leave gameweeks blank if you don't want to backfill them</li>
                <li>Points will be automatically calculated based on match results</li>
                <li>If a pick already exists for a gameweek, it will be updated</li>
                <li>Make sure fixtures have been synced for accurate point calculation</li>
              </ul>
            </div>
          </form>
        </CardContent>
      </Card>
    );
  }
}
