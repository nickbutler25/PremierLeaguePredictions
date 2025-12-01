import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { adminService } from '@/services/admin';
import { eliminationService } from '@/services/elimination';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { useToast } from '@/hooks/use-toast';
import { Alert, AlertDescription } from '@/components/ui/alert';

export default function EliminationManagementPage() {
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [eliminationCounts, setEliminationCounts] = useState<Record<string, number>>({});

  // Fetch active season
  const { data: activeSeason } = useQuery({
    queryKey: ['activeSeason'],
    queryFn: () => adminService.getActiveSeason(),
  });

  // Fetch elimination configs
  const { data: configs = [], isLoading: configsLoading, error: configsError } = useQuery({
    queryKey: ['eliminationConfigs', activeSeason?.name],
    queryFn: async () => {
      console.log('EliminationManagement: Fetching configs for season:', activeSeason?.name);
      const result = await eliminationService.getEliminationConfigs(activeSeason!.name);
      console.log('EliminationManagement: Received configs:', result);
      return result;
    },
    enabled: !!activeSeason?.name,
  });

  // Log configs whenever they change
  console.log('EliminationManagement: configs=', configs, 'loading=', configsLoading, 'error=', configsError);

  // Fetch season eliminations
  const { data: eliminations = [] } = useQuery({
    queryKey: ['seasonEliminations', activeSeason?.name],
    queryFn: () => eliminationService.getSeasonEliminations(activeSeason!.name),
    enabled: !!activeSeason?.name,
  });

  // Update single gameweek elimination count
  const updateCountMutation = useMutation({
    mutationFn: ({ gameweekId, count }: { gameweekId: string; count: number }) =>
      eliminationService.updateGameweekEliminationCount(gameweekId, count),
    onSuccess: () => {
      toast({
        title: 'Success',
        description: 'Elimination count updated successfully',
      });
      queryClient.invalidateQueries({ queryKey: ['eliminationConfigs'] });
      setEliminationCounts({});
    },
    onError: (error: any) => {
      toast({
        title: 'Error',
        description: error.response?.data?.message || 'Failed to update elimination count',
        variant: 'destructive',
      });
    },
  });

  // Bulk update elimination counts
  const bulkUpdateMutation = useMutation({
    mutationFn: (counts: Record<string, number>) =>
      eliminationService.bulkUpdateEliminationCounts(counts),
    onSuccess: () => {
      toast({
        title: 'Success',
        description: 'All elimination counts updated successfully',
      });
      queryClient.invalidateQueries({ queryKey: ['eliminationConfigs'] });
      setEliminationCounts({});
    },
    onError: (error: any) => {
      toast({
        title: 'Error',
        description: error.response?.data?.message || 'Failed to update elimination counts',
        variant: 'destructive',
      });
    },
  });


  const handleCountChange = (gameweekId: string, value: string) => {
    const count = parseInt(value) || 0;
    setEliminationCounts((prev) => ({
      ...prev,
      [gameweekId]: count,
    }));
  };

  const handleBulkSave = () => {
    if (Object.keys(eliminationCounts).length > 0) {
      bulkUpdateMutation.mutate(eliminationCounts);
    }
  };


  if (configsLoading) {
    return (
      <div className="container mx-auto py-8">
        <p>Loading elimination configuration...</p>
      </div>
    );
  }

  if (!activeSeason) {
    return (
      <div className="container mx-auto py-8">
        <Alert>
          <AlertDescription>No active season found. Please create a season first.</AlertDescription>
        </Alert>
      </div>
    );
  }

  // Group configs by half
  const firstHalf = configs.filter((c) => c.weekNumber <= 20);
  const secondHalf = configs.filter((c) => c.weekNumber > 20);

  const totalEliminated = eliminations.length;
  const eliminationsByGameweek = eliminations.reduce((acc, e) => {
    acc[e.gameweekNumber] = (acc[e.gameweekNumber] || 0) + 1;
    return acc;
  }, {} as Record<number, number>);

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div>
        <h1 className="text-3xl font-bold">Elimination Management</h1>
        <p className="text-muted-foreground mt-2">Configure player eliminations for each gameweek</p>
      </div>

      <div className="flex items-center justify-end">
        {Object.keys(eliminationCounts).length > 0 && (
          <Button
            onClick={handleBulkSave}
            disabled={bulkUpdateMutation.isPending}
            className="bg-green-600 hover:bg-green-700"
          >
            💾 Save All Changes ({Object.keys(eliminationCounts).length})
          </Button>
        )}
      </div>

      {/* Summary Card */}
      <Card>
        <CardHeader>
          <CardTitle>Elimination Summary</CardTitle>
          <CardDescription>Overview of eliminations for {activeSeason.name}</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-3 gap-4">
            <div>
              <p className="text-sm text-muted-foreground">Total Players Eliminated</p>
              <p className="text-2xl font-bold">{totalEliminated}</p>
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Total Elimination Slots</p>
              <p className="text-2xl font-bold">
                {configs.reduce((sum, c) => sum + c.eliminationCount, 0)}
              </p>
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Gameweeks with Eliminations</p>
              <p className="text-2xl font-bold">
                {configs.filter((c) => c.eliminationCount > 0).length}
              </p>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* First Half Configuration */}
      <Card>
        <CardHeader>
          <CardTitle>First Half (GW 1-20)</CardTitle>
          <CardDescription>
            Configure eliminations for the first half of the season. Eliminations are processed automatically after each gameweek finishes.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            {firstHalf.map((config) => {
              const currentValue = eliminationCounts[config.gameweekId] ?? config.eliminationCount;
              const hasEliminations = eliminationsByGameweek[config.weekNumber] > 0;

              return (
                <div
                  key={config.gameweekId}
                  className={`border rounded-lg p-4 ${
                    hasEliminations ? 'bg-green-50 dark:bg-green-950/20 border-green-300 dark:border-green-800' : ''
                  }`}
                >
                  <div className="flex items-center justify-between mb-2">
                    <span className="font-semibold">GW {config.weekNumber}</span>
                    {config.hasBeenProcessed && (
                      <span className="text-xs px-2 py-1 bg-green-500 text-white rounded">
                        ✓ Auto-Processed
                      </span>
                    )}
                  </div>

                  <div className="space-y-2">
                    <div className="flex items-center gap-2">
                      <Input
                        type="number"
                        min="0"
                        max="100"
                        value={currentValue}
                        onChange={(e) => handleCountChange(config.gameweekId, e.target.value)}
                        disabled={config.hasBeenProcessed || updateCountMutation.isPending}
                        className="w-20"
                      />
                      <span className="text-sm text-muted-foreground">to eliminate</span>
                    </div>

                    {hasEliminations && (
                      <p className="text-xs text-green-600 dark:text-green-400">
                        ✓ {eliminationsByGameweek[config.weekNumber]} eliminated
                      </p>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        </CardContent>
      </Card>

      {/* Second Half Configuration */}
      <Card>
        <CardHeader>
          <CardTitle>Second Half (GW 21-38)</CardTitle>
          <CardDescription>
            Configure eliminations for the second half of the season. Eliminations are processed automatically after each gameweek finishes.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            {secondHalf.map((config) => {
              const currentValue = eliminationCounts[config.gameweekId] ?? config.eliminationCount;
              const hasEliminations = eliminationsByGameweek[config.weekNumber] > 0;

              return (
                <div
                  key={config.gameweekId}
                  className={`border rounded-lg p-4 ${
                    hasEliminations ? 'bg-green-50 dark:bg-green-950/20 border-green-300 dark:border-green-800' : ''
                  }`}
                >
                  <div className="flex items-center justify-between mb-2">
                    <span className="font-semibold">GW {config.weekNumber}</span>
                    {config.hasBeenProcessed && (
                      <span className="text-xs px-2 py-1 bg-green-500 text-white rounded">
                        ✓ Auto-Processed
                      </span>
                    )}
                  </div>

                  <div className="space-y-2">
                    <div className="flex items-center gap-2">
                      <Input
                        type="number"
                        min="0"
                        max="100"
                        value={currentValue}
                        onChange={(e) => handleCountChange(config.gameweekId, e.target.value)}
                        disabled={config.hasBeenProcessed || updateCountMutation.isPending}
                        className="w-20"
                      />
                      <span className="text-sm text-muted-foreground">to eliminate</span>
                    </div>

                    {hasEliminations && (
                      <p className="text-xs text-green-600 dark:text-green-400">
                        ✓ {eliminationsByGameweek[config.weekNumber]} eliminated
                      </p>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        </CardContent>
      </Card>

      {/* Eliminated Players List */}
      {eliminations.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle>Eliminated Players</CardTitle>
            <CardDescription>Players who have been eliminated from the competition</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="space-y-2">
              {eliminations.map((elimination) => (
                <div
                  key={elimination.id}
                  className="flex items-center justify-between p-3 border rounded-lg"
                >
                  <div>
                    <p className="font-semibold">{elimination.userName}</p>
                    <p className="text-sm text-muted-foreground">
                      GW{elimination.gameweekNumber} • Position {elimination.position} • {elimination.totalPoints} points
                    </p>
                  </div>
                  <div className="text-right text-sm text-muted-foreground">
                    {new Date(elimination.eliminatedAt).toLocaleDateString()}
                  </div>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
