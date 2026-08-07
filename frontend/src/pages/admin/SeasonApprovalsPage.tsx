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

  // Approvals are always scoped to the active season — no season filter.
  const { data: activeSeason, isLoading: loadingSeason } = useQuery({
    queryKey: ['active-season'],
    queryFn: () => adminService.getActiveSeason(),
  });

  const {
    data: pendingApprovals = [],
    isLoading: loadingApprovals,
    isError: approvalsError,
    error: approvalsErrorObj,
    refetch: refetchApprovals,
  } = useQuery({
    queryKey: ['season-approvals', activeSeason?.id],
    queryFn: () => seasonParticipationService.getPendingApprovals(activeSeason?.id),
    enabled: !!activeSeason,
  });

  const approveMutation = useMutation({
    mutationFn: ({
      participationId,
      isApproved,
    }: {
      participationId: string;
      isApproved: boolean;
    }) => seasonParticipationService.approveParticipation(participationId, isApproved),
    onSuccess: (_, variables) => {
      // Remove immediately from cache so the row disappears without waiting for refetch
      queryClient.setQueriesData<typeof pendingApprovals>(
        { queryKey: ['season-approvals'] },
        (old) => old?.filter((a) => a.participationId !== variables.participationId) ?? []
      );
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
    onError: (error: unknown) => {
      const err = error as { response?: { data?: { message?: string } } };
      toast({
        title: 'Error',
        description: err.response?.data?.message || 'Failed to process approval',
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

  return (
    <div>
      <Card>
        <CardHeader>
          <CardTitle>Season Participation Approvals</CardTitle>
          <CardDescription>
            Review and approve requests to participate in the current season
          </CardDescription>
        </CardHeader>
        <CardContent>
          {loadingSeason ? (
            <div className="text-center py-8 text-muted-foreground">Loading...</div>
          ) : !activeSeason ? (
            <div className="text-center py-12 text-muted-foreground">
              No active season. Create one to manage participation approvals.
            </div>
          ) : approvalsError ? (
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
                No pending approval requests for the current season.
              </p>
            </div>
          ) : (
            <div className="rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>User</TableHead>
                    <TableHead>Email</TableHead>
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
                      <TableCell className="text-muted-foreground">{approval.email}</TableCell>
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
              Showing {pendingApprovals.length} pending approval
              {pendingApprovals.length !== 1 ? 's' : ''}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
