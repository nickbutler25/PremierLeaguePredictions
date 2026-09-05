import { useState, useEffect } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { adminService } from '@/services/admin';
import { teamsService } from '@/services/teams';
import { usersService } from '@/services/users';
import { gameweeksService } from '@/services/gameweeks';
import { useAuth } from '@/contexts/AuthContext';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { useToast } from '@/hooks/use-toast';

interface LoadErrorProps {
  title: string;
  error: unknown;
  fallback: string;
}

function LoadError({ title, error, fallback }: LoadErrorProps) {
  return (
    <div className="container mx-auto p-6">
      <Card>
        <CardContent className="p-6">
          <div className="text-center text-red-600 dark:text-red-400">
            <p className="font-semibold">{title}</p>
            <p className="text-sm mt-2">{(error as { message?: string })?.message || fallback}</p>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

export function BackfillPicksPage() {
  const { user } = useAuth();
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [selectedUserId, setSelectedUserId] = useState<string>('');

  const [picks, setPicks] = useState<Array<{ gameweekNumber: number; teamId: string }>>([]);

  const teamsQuery = useQuery({
    queryKey: ['teams'],
    queryFn: () => teamsService.getTeams(),
  });

  const usersQuery = useQuery({
    queryKey: ['users'],
    queryFn: () => usersService.getUsers(),
  });

  const gameweekQuery = useQuery({
    queryKey: ['current-gameweek'],
    queryFn: () => gameweeksService.getCurrentGameweek(),
  });

  const teams = (teamsQuery.data || []).sort((a, b) => a.name.localeCompare(b.name));
  const loadingTeams = teamsQuery.isLoading;
  const teamsError = teamsQuery.error;

  // By last name: the dropdown is scanned by eye to find one player, and the API returns them
  // in no order a reader can follow. Copied before sorting rather than sorted in place, because
  // the array behind it belongs to the React Query cache.
  const users = [...(usersQuery.data || [])].sort(
    (a, b) => a.lastName.localeCompare(b.lastName) || a.firstName.localeCompare(b.firstName)
  );
  const loadingUsers = usersQuery.isLoading;
  const usersError = usersQuery.error;

  const currentGameweek = gameweekQuery.data;
  const loadingGameweek = gameweekQuery.isLoading;
  const gameweekError = gameweekQuery.error;

  useEffect(() => {
    if (currentGameweek) {
      const currentGwNum = currentGameweek.weekNumber;
      const newPicks = [];
      // Generate picks for all previous gameweeks (1 to current - 1)
      for (let i = 1; i < currentGwNum; i++) {
        newPicks.push({ gameweekNumber: i, teamId: '' });
      }
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setPicks(newPicks);
    }
  }, [currentGameweek]);

  const backfillMutation = useMutation({
    mutationFn: () => {
      const userId = selectedUserId || user?.id;
      if (!userId) throw new Error('No user selected');
      const validPicks = picks
        .filter((p) => p.teamId !== '')
        .map((p) => ({ ...p, teamId: parseInt(p.teamId, 10) }));
      return adminService.backfillPicks(userId, validPicks);
    },
    onSuccess: (response) => {
      toast({
        title: 'Picks Backfilled Successfully',
        description: `Created: ${response.picksCreated}, Updated: ${response.picksUpdated}, Skipped: ${response.picksSkipped}`,
        duration: 5000,
      });
      queryClient.invalidateQueries({ queryKey: ['picks'] });
    },
    onError: (error: unknown) => {
      console.error('Backfill error:', error);
      const err = error as {
        response?: { data?: { message?: string; title?: string } };
        message?: string;
      };
      const errorMessage =
        err.response?.data?.message ||
        err.response?.data?.title ||
        err.message ||
        'Failed to backfill picks';

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

  const handleSubmit = (e: React.FormEvent) => {
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

    const validPicks = picks.filter((p) => p.teamId !== '');
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

  if (loadingTeams || loadingUsers || loadingGameweek) {
    return (
      <div className="container mx-auto p-6">
        <Card>
          <CardContent className="p-12">
            <div className="flex flex-col items-center justify-center space-y-4">
              <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-primary"></div>
              <p className="text-muted-foreground">Loading backfill page...</p>
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }

  if (teamsError) {
    return (
      <LoadError title="Error loading teams" error={teamsError} fallback="Failed to load teams" />
    );
  }

  if (usersError) {
    return (
      <LoadError title="Error loading users" error={usersError} fallback="Failed to load users" />
    );
  }

  // The gameweek decides how many slots the form has, so a failure here used to render a
  // complete-looking page with nothing in it — indistinguishable from a season with nothing
  // to backfill.
  if (gameweekError) {
    return (
      <LoadError
        title="Error loading gameweeks"
        error={gameweekError}
        fallback="Failed to load the current gameweek"
      />
    );
  }

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle>
            Backfill Gameweeks 1-{currentGameweek ? currentGameweek.weekNumber - 1 : '...'}
          </CardTitle>
          <CardDescription>
            Select your team picks for the first{' '}
            {currentGameweek ? currentGameweek.weekNumber - 1 : '...'} gameweeks. Points will be
            automatically calculated based on results.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="space-y-4">
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
                    {teams.map((team) => (
                      <option key={team.id} value={team.id}>
                        {team.name}
                      </option>
                    ))}
                  </select>
                </div>
              ))}
            </div>

            <div className="flex gap-2 pt-4">
              <Button type="submit" disabled={backfillMutation.isPending}>
                {backfillMutation.isPending ? 'Backfilling...' : 'Backfill Picks'}
              </Button>
            </div>

            <div className="rounded-lg bg-blue-50 dark:bg-violet-950/40 p-4 border border-blue-200 dark:border-violet-800/60">
              <p className="text-sm font-medium text-blue-900 dark:text-violet-100 mb-2">
                Important Notes:
              </p>
              <ul className="text-sm text-blue-800 dark:text-violet-200 list-disc list-inside space-y-1">
                <li>You can leave gameweeks blank if you don't want to backfill them</li>
                <li>Points will be automatically calculated based on match results</li>
                <li>If a pick already exists for a gameweek, it will be updated</li>
                <li>Make sure fixtures have been synced for accurate point calculation</li>
              </ul>
            </div>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
