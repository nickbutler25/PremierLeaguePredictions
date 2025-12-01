import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { seasonParticipationService } from '@/services/seasonParticipation';
import { adminService } from '@/services/admin';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { useToast } from '@/hooks/use-toast';
import { CheckCircle2, XCircle, Clock } from 'lucide-react';
import { ErrorDisplay } from '@/components/ErrorDisplay';

export function SeasonApprovalsPage() {
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [selectedSeasonId, setSelectedSeasonId] = useState<string | undefined>(undefined);

  const { data: seasons = [], isError: seasonsError, error: seasonsErrorObj, refetch: refetchSeasons } = useQuery({
    queryKey: ['admin', 'seasons'],
    queryFn: () => adminService.getSeasons(),
  });

  const {
    data: pendingApprovals = [],
    isLoading: loadingApprovals,
    isError: approvalsError,
    error: approvalsErrorObj,
    refetch: refetchApprovals
  } = useQuery({
    queryKey: ['season-approvals', selectedSeasonId],
    queryFn: () => seasonParticipationService.getPendingApprovals(selectedSeasonId),
  });

  const approveMutation = useMutation({
    mutationFn: ({ participationId, isApproved }: { participationId: string; isApproved: boolean }) =>
      seasonParticipationService.approveParticipation(participationId, isApproved),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ['season-approvals'] });
      queryClient.invalidateQueries({ queryKey: ['active-season'] });
      queryClient.invalidateQueries({ queryKey: ['season-approval'] });
      queryClient.invalidateQueries({ queryKey: ['participation'] });
      queryClient.invalidateQueries({ queryKey: ['dashboard'] });
      toast({
        title: variables.isApproved ? 'Approved' : 'Rejected',
        description: `User participation has been ${variables.isApproved ? 'approved' : 'rejected'}.`,
      });
    },
    onError: (error: any) => {
      toast({
        title: 'Error',
        description: error.response?.data?.message || 'Failed to process approval',
        variant: 'destructive',
      });
    },
  });

  const handleApprove = (participationId: string) => {
    if (confirm('Are you sure you want to approve this user for the season?')) {
      approveMutation.mutate({ participationId, isApproved: true });
    }
  };

  const handleReject = (participationId: string) => {
    if (confirm('Are you sure you want to reject this user for the season?')) {
      approveMutation.mutate({ participationId, isApproved: false });
    }
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString('en-GB', {
      day: '2-digit',
      month: 'short',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  const activeSeason = seasons.find(s => s.isActive);

  return (
    <div>
      <Card>
        <CardHeader>
          <CardTitle>Season Participation Approvals</CardTitle>
          <CardDescription>
            Review and approve user requests to participate in seasons
          </CardDescription>
        </CardHeader>
        <CardContent>
          {/* Season Filter */}
          {seasonsError ? (
            <div className="mb-6">
              <ErrorDisplay
                title="Failed to Load Seasons"
                message="Could not load season data for filtering"
                error={seasonsErrorObj}
                onRetry={refetchSeasons}
              />
            </div>
          ) : (
            <div className="mb-6 flex gap-2 items-center">
              <label className="font-medium">Filter by Season:</label>
              <select
                className="border rounded px-3 py-2 min-w-[200px]"
                value={selectedSeasonId || ''}
                onChange={(e) => setSelectedSeasonId(e.target.value || undefined)}
              >
                <option value="">All Seasons</option>
                {seasons.map((season) => (
                  <option key={season.id} value={season.id}>
                    {season.name} {season.isActive && '(Active)'}
                  </option>
                ))}
              </select>
            </div>
          )}

          {/* Pending Approvals Table */}
          {approvalsError ? (
            <ErrorDisplay
              title="Failed to Load Pending Approvals"
              message="Could not load pending approval requests"
              error={approvalsErrorObj}
              onRetry={refetchApprovals}
            />
          ) : loadingApprovals ? (
            <div className="text-center py-8 text-muted-foreground">Loading approvals...</div>
          ) : pendingApprovals.length === 0 ? (
            <div className="text-center py-12 space-y-2">
              <CheckCircle2 className="w-12 h-12 mx-auto text-green-500" />
              <p className="text-lg font-medium">All caught up!</p>
              <p className="text-muted-foreground">
                No pending approval requests for {selectedSeasonId ? 'this season' : 'any season'}.
              </p>
            </div>
          ) : (
            <div className="rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>User</TableHead>
                    <TableHead>Email</TableHead>
                    <TableHead>Season</TableHead>
                    <TableHead>Requested</TableHead>
                    <TableHead>Payment Status</TableHead>
                    <TableHead className="text-right">Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {pendingApprovals.map((approval) => (
                    <TableRow key={approval.participationId}>
                      <TableCell>
                        <div className="flex items-center gap-3">
                          {approval.photoUrl && (
                            <img
                              src={approval.photoUrl}
                              alt={`${approval.firstName} ${approval.lastName}`}
                              className="w-8 h-8 rounded-full"
                            />
                          )}
                          <div>
                            <div className="font-medium">
                              {approval.firstName} {approval.lastName}
                            </div>
                          </div>
                        </div>
                      </TableCell>
                      <TableCell className="text-muted-foreground">
                        {approval.email}
                      </TableCell>
                      <TableCell>
                        <Badge variant={approval.seasonId === activeSeason?.id ? 'default' : 'outline'}>
                          {approval.seasonName}
                        </Badge>
                      </TableCell>
                      <TableCell>
                        <div className="flex items-center gap-1 text-sm text-muted-foreground">
                          <Clock className="w-4 h-4" />
                          {formatDate(approval.requestedAt)}
                        </div>
                      </TableCell>
                      <TableCell>
                        <Badge variant={approval.isPaid ? 'default' : 'destructive'}>
                          {approval.isPaid ? 'Paid' : 'Not Paid'}
                        </Badge>
                      </TableCell>
                      <TableCell className="text-right">
                        <div className="flex gap-2 justify-end">
                          <Button
                            size="sm"
                            variant="default"
                            onClick={() => handleApprove(approval.participationId)}
                            disabled={approveMutation.isPending}
                            className="bg-green-600 hover:bg-green-700"
                          >
                            <CheckCircle2 className="w-4 h-4 mr-1" />
                            Approve
                          </Button>
                          <Button
                            size="sm"
                            variant="destructive"
                            onClick={() => handleReject(approval.participationId)}
                            disabled={approveMutation.isPending}
                          >
                            <XCircle className="w-4 h-4 mr-1" />
                            Reject
                          </Button>
                        </div>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}

          {/* Summary */}
          {pendingApprovals.length > 0 && (
            <div className="mt-4 text-sm text-muted-foreground">
              Showing {pendingApprovals.length} pending approval{pendingApprovals.length !== 1 ? 's' : ''}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
