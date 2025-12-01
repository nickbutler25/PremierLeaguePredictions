import React, { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { adminService, type PickRuleDto, type Season } from '@/services/admin';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import { useToast } from '@/hooks/use-toast';

export function PickRulesManagementPage() {
  const [selectedSeasonId, setSelectedSeasonId] = useState<string>('');
  const [editingRule, setEditingRule] = useState<{ half: number; rule: PickRuleDto | null } | null>(null);
  const [maxTeamPicks, setMaxTeamPicks] = useState(1);
  const [maxOppositionTargets, setMaxOppositionTargets] = useState(1);
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const { data: seasons, isLoading: loadingSeasons } = useQuery({
    queryKey: ['admin', 'seasons'],
    queryFn: () => adminService.getSeasons(),
  });

  // Set active season as default when seasons load
  const activeSeason = seasons?.find((s: Season) => s.isActive);

  // Auto-select active season on first load
  React.useEffect(() => {
    if (activeSeason && !selectedSeasonId) {
      setSelectedSeasonId(activeSeason.name);
    }
  }, [activeSeason, selectedSeasonId]);

  const { data: pickRules, isLoading: loadingRules } = useQuery({
    queryKey: ['admin', 'pick-rules', selectedSeasonId],
    queryFn: () => adminService.getPickRules(selectedSeasonId),
    enabled: !!selectedSeasonId,
  });

  const createRuleMutation = useMutation({
    mutationFn: adminService.createPickRule,
    onSuccess: () => {
      toast({
        title: 'Success',
        description: 'Pick rule created successfully',
      });
      queryClient.invalidateQueries({ queryKey: ['admin', 'pick-rules', selectedSeasonId] });
      setEditingRule(null);
      setMaxTeamPicks(1);
      setMaxOppositionTargets(1);
    },
    onError: (error: any) => {
      toast({
        title: 'Error',
        description: error.response?.data?.message || 'Failed to create pick rule',
        variant: 'destructive',
      });
    },
  });

  const updateRuleMutation = useMutation({
    mutationFn: ({ id, request }: { id: string; request: { maxTimesTeamCanBePicked: number; maxTimesOppositionCanBeTargeted: number } }) =>
      adminService.updatePickRule(id, request),
    onSuccess: () => {
      toast({
        title: 'Success',
        description: 'Pick rule updated successfully',
      });
      queryClient.invalidateQueries({ queryKey: ['admin', 'pick-rules', selectedSeasonId] });
      setEditingRule(null);
      setMaxTeamPicks(1);
      setMaxOppositionTargets(1);
    },
    onError: (error: any) => {
      toast({
        title: 'Error',
        description: error.response?.data?.message || 'Failed to update pick rule',
        variant: 'destructive',
      });
    },
  });

  const deleteRuleMutation = useMutation({
    mutationFn: adminService.deletePickRule,
    onSuccess: () => {
      toast({
        title: 'Success',
        description: 'Pick rule deleted successfully',
      });
      queryClient.invalidateQueries({ queryKey: ['admin', 'pick-rules', selectedSeasonId] });
    },
    onError: (error: any) => {
      toast({
        title: 'Error',
        description: error.response?.data?.message || 'Failed to delete pick rule',
        variant: 'destructive',
      });
    },
  });

  const initializeDefaultRulesMutation = useMutation({
    mutationFn: adminService.initializeDefaultPickRules,
    onSuccess: () => {
      toast({
        title: 'Success',
        description: 'Default pick rules initialized (max 1 for both halves)',
      });
      queryClient.invalidateQueries({ queryKey: ['admin', 'pick-rules', selectedSeasonId] });
    },
    onError: (error: any) => {
      toast({
        title: 'Error',
        description: error.response?.data?.message || 'Failed to initialize default rules',
        variant: 'destructive',
      });
    },
  });

  const handleEditRule = (half: number, rule: PickRuleDto | null) => {
    setEditingRule({ half, rule });
    if (rule) {
      setMaxTeamPicks(rule.maxTimesTeamCanBePicked);
      setMaxOppositionTargets(rule.maxTimesOppositionCanBeTargeted);
    } else {
      setMaxTeamPicks(1);
      setMaxOppositionTargets(1);
    }
  };

  const handleSaveRule = () => {
    if (!editingRule || !selectedSeasonId) return;

    if (editingRule.rule) {
      // Update existing rule
      updateRuleMutation.mutate({
        id: editingRule.rule.id,
        request: {
          maxTimesTeamCanBePicked: maxTeamPicks,
          maxTimesOppositionCanBeTargeted: maxOppositionTargets,
        },
      });
    } else {
      // Create new rule
      createRuleMutation.mutate({
        seasonId: selectedSeasonId,
        half: editingRule.half,
        maxTimesTeamCanBePicked: maxTeamPicks,
        maxTimesOppositionCanBeTargeted: maxOppositionTargets,
      });
    }
  };

  const handleDeleteRule = (ruleId: string) => {
    if (window.confirm('Are you sure you want to delete this pick rule?')) {
      deleteRuleMutation.mutate(ruleId);
    }
  };

  const handleCancelEdit = () => {
    setEditingRule(null);
    setMaxTeamPicks(1);
    setMaxOppositionTargets(1);
  };

  return (
    <div className="space-y-6">
        {/* Season Selection */}
        <Card>
          <CardHeader>
            <CardTitle>Select Season</CardTitle>
            <CardDescription>Choose a season to manage its pick rules</CardDescription>
          </CardHeader>
          <CardContent>
            {loadingSeasons ? (
              <p>Loading seasons...</p>
            ) : (
              <div className="space-y-2">
                <Label htmlFor="season">Season</Label>
                <select
                  id="season"
                  value={selectedSeasonId}
                  onChange={(e) => setSelectedSeasonId(e.target.value)}
                  className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  <option value="">-- Select a season --</option>
                  {seasons?.map((season: Season) => (
                    <option key={season.id} value={season.name}>
                      {season.name} {season.isActive ? '(Active)' : ''}
                    </option>
                  ))}
                </select>
              </div>
            )}
          </CardContent>
        </Card>

        {/* Pick Rules Display */}
        {selectedSeasonId && (
          <>
            <Card>
              <CardHeader>
                <CardTitle>Pick Rules for {selectedSeasonId}</CardTitle>
                <CardDescription>
                  Configure different pick rules for the first half (weeks 1-19) and second half (weeks 20-38) of the season
                </CardDescription>
              </CardHeader>
              <CardContent>
                {loadingRules ? (
                  <p>Loading pick rules...</p>
                ) : (
                  <div className="space-y-6">
                    {/* Initialize Default Rules Button */}
                    {!pickRules?.firstHalf && !pickRules?.secondHalf && (
                      <div className="p-4 border rounded-lg bg-muted">
                        <p className="text-sm mb-2">No pick rules found for this season.</p>
                        <Button
                          onClick={() => initializeDefaultRulesMutation.mutate(selectedSeasonId)}
                          disabled={initializeDefaultRulesMutation.isPending}
                        >
                          Initialize Default Rules (Max 1)
                        </Button>
                      </div>
                    )}

                    {/* First Half Rules */}
                    <div className="border rounded-lg p-4">
                      <div className="flex items-center justify-between mb-4">
                        <h3 className="text-lg font-semibold">First Half (Weeks 1-19)</h3>
                        {pickRules?.firstHalf ? (
                          <div className="flex gap-2">
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() => handleEditRule(1, pickRules.firstHalf)}
                            >
                              Edit
                            </Button>
                            <Button
                              size="sm"
                              variant="destructive"
                              onClick={() => handleDeleteRule(pickRules.firstHalf!.id)}
                              disabled={deleteRuleMutation.isPending}
                            >
                              Delete
                            </Button>
                          </div>
                        ) : (
                          <Button
                            size="sm"
                            onClick={() => handleEditRule(1, null)}
                          >
                            Create Rule
                          </Button>
                        )}
                      </div>

                      {editingRule?.half === 1 ? (
                        <div className="space-y-4 bg-muted p-4 rounded-md">
                          <div className="space-y-2">
                            <Label htmlFor="maxTeamPicks1">Max times a team can be picked</Label>
                            <Input
                              id="maxTeamPicks1"
                              type="number"
                              min="1"
                              max="19"
                              value={maxTeamPicks}
                              onChange={(e) => setMaxTeamPicks(parseInt(e.target.value) || 1)}
                            />
                            <p className="text-xs text-muted-foreground">
                              Range: 1-19 (entire half of season)
                            </p>
                          </div>
                          <div className="space-y-2">
                            <Label htmlFor="maxOppositionTargets1">Max times an opposition can be targeted</Label>
                            <Input
                              id="maxOppositionTargets1"
                              type="number"
                              min="1"
                              max="19"
                              value={maxOppositionTargets}
                              onChange={(e) => setMaxOppositionTargets(parseInt(e.target.value) || 1)}
                            />
                            <p className="text-xs text-muted-foreground">
                              Range: 1-19 (entire half of season)
                            </p>
                          </div>
                          <div className="flex gap-2">
                            <Button
                              onClick={handleSaveRule}
                              disabled={createRuleMutation.isPending || updateRuleMutation.isPending}
                            >
                              Save
                            </Button>
                            <Button variant="outline" onClick={handleCancelEdit}>
                              Cancel
                            </Button>
                          </div>
                        </div>
                      ) : pickRules?.firstHalf ? (
                        <div className="space-y-2">
                          <p className="text-sm">
                            <span className="font-medium">Max team picks:</span> {pickRules.firstHalf.maxTimesTeamCanBePicked}
                          </p>
                          <p className="text-sm">
                            <span className="font-medium">Max opposition targets:</span> {pickRules.firstHalf.maxTimesOppositionCanBeTargeted}
                          </p>
                        </div>
                      ) : (
                        <p className="text-sm text-muted-foreground">No rule configured</p>
                      )}
                    </div>

                    {/* Second Half Rules */}
                    <div className="border rounded-lg p-4">
                      <div className="flex items-center justify-between mb-4">
                        <h3 className="text-lg font-semibold">Second Half (Weeks 20-38)</h3>
                        {pickRules?.secondHalf ? (
                          <div className="flex gap-2">
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() => handleEditRule(2, pickRules.secondHalf)}
                            >
                              Edit
                            </Button>
                            <Button
                              size="sm"
                              variant="destructive"
                              onClick={() => handleDeleteRule(pickRules.secondHalf!.id)}
                              disabled={deleteRuleMutation.isPending}
                            >
                              Delete
                            </Button>
                          </div>
                        ) : (
                          <Button
                            size="sm"
                            onClick={() => handleEditRule(2, null)}
                          >
                            Create Rule
                          </Button>
                        )}
                      </div>

                      {editingRule?.half === 2 ? (
                        <div className="space-y-4 bg-muted p-4 rounded-md">
                          <div className="space-y-2">
                            <Label htmlFor="maxTeamPicks2">Max times a team can be picked</Label>
                            <Input
                              id="maxTeamPicks2"
                              type="number"
                              min="1"
                              max="19"
                              value={maxTeamPicks}
                              onChange={(e) => setMaxTeamPicks(parseInt(e.target.value) || 1)}
                            />
                            <p className="text-xs text-muted-foreground">
                              Range: 1-19 (entire half of season)
                            </p>
                          </div>
                          <div className="space-y-2">
                            <Label htmlFor="maxOppositionTargets2">Max times an opposition can be targeted</Label>
                            <Input
                              id="maxOppositionTargets2"
                              type="number"
                              min="1"
                              max="19"
                              value={maxOppositionTargets}
                              onChange={(e) => setMaxOppositionTargets(parseInt(e.target.value) || 1)}
                            />
                            <p className="text-xs text-muted-foreground">
                              Range: 1-19 (entire half of season)
                            </p>
                          </div>
                          <div className="flex gap-2">
                            <Button
                              onClick={handleSaveRule}
                              disabled={createRuleMutation.isPending || updateRuleMutation.isPending}
                            >
                              Save
                            </Button>
                            <Button variant="outline" onClick={handleCancelEdit}>
                              Cancel
                            </Button>
                          </div>
                        </div>
                      ) : pickRules?.secondHalf ? (
                        <div className="space-y-2">
                          <p className="text-sm">
                            <span className="font-medium">Max team picks:</span> {pickRules.secondHalf.maxTimesTeamCanBePicked}
                          </p>
                          <p className="text-sm">
                            <span className="font-medium">Max opposition targets:</span> {pickRules.secondHalf.maxTimesOppositionCanBeTargeted}
                          </p>
                        </div>
                      ) : (
                        <p className="text-sm text-muted-foreground">No rule configured</p>
                      )}
                    </div>
                  </div>
                )}
              </CardContent>
            </Card>

            {/* Info Card */}
            <Card>
              <CardHeader>
                <CardTitle>How Pick Rules Work</CardTitle>
              </CardHeader>
              <CardContent className="space-y-2 text-sm">
                <p>
                  <strong>Max times a team can be picked:</strong> Limits how many times a user can select the same team in a half of the season.
                </p>
                <p>
                  <strong>Max times an opposition can be targeted:</strong> Limits how many times a user can pick teams playing against the same opposition in a half.
                </p>
                <p className="pt-2 text-muted-foreground">
                  Example: If both are set to 1, users can only pick each team once per half, and can only target each opposition once per half.
                  Setting to 19 allows picking every week.
                </p>
              </CardContent>
            </Card>
          </>
        )}
    </div>
  );
}
