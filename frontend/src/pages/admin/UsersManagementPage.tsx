import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { usersService } from '@/services/users';
import type { UserListItem } from '@/services/users';
import { seasonParticipationService } from '@/services/seasonParticipation';
import { adminService } from '@/services/admin';
import { useAuth } from '@/contexts/AuthContext';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { UserAvatar } from '@/components/UserAvatar';
import { useToast } from '@/hooks/use-toast';
import { ErrorDisplay } from '@/components/ErrorDisplay';
import { ShieldCheck, Trash2 } from 'lucide-react';

export function UsersManagementPage() {
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const { user: currentUser } = useAuth();
  const [search, setSearch] = useState('');
  const [selectedSeasonId, setSelectedSeasonId] = useState<string | undefined>(undefined);

  const { data: seasons = [] } = useQuery({
    queryKey: ['admin', 'seasons'],
    queryFn: () => adminService.getSeasons(),
  });
  const activeSeason = seasons.find((s) => s.isActive);
  const seasonId = selectedSeasonId ?? activeSeason?.id;

  const {
    data: users = [],
    isLoading,
    isError,
    error,
    refetch,
  } = useQuery({
    queryKey: ['admin', 'users'],
    queryFn: () => usersService.getUsers(),
  });

  // Per-season participation (approval + payment) for the selected season.
  const { data: participants = [] } = useQuery({
    queryKey: ['season-participants', seasonId],
    queryFn: () => seasonParticipationService.getSeasonParticipants(seasonId!),
    enabled: !!seasonId,
  });
  const participationByUser = new Map(participants.map((p) => [p.userId, p]));

  const showError = (err: unknown, fallback: string) => {
    const e = err as { response?: { data?: { message?: string } } };
    toast({
      title: 'Error',
      description: e.response?.data?.message || fallback,
      variant: 'destructive',
    });
  };

  const invalidateParticipants = () =>
    queryClient.invalidateQueries({ queryKey: ['season-participants'] });

  const paymentMutation = useMutation({
    mutationFn: ({ participationId, isPaid }: { participationId: string; isPaid: boolean }) =>
      seasonParticipationService.updateParticipationPayment(participationId, isPaid),
    onSuccess: (_, variables) => {
      invalidateParticipants();
      toast({ title: variables.isPaid ? 'Marked as paid' : 'Marked as not paid' });
    },
    onError: (err: unknown) => showError(err, 'Failed to update payment'),
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
      invalidateParticipants();
      queryClient.invalidateQueries({ queryKey: ['season-approvals'] });
      toast({ title: variables.isApproved ? 'Participation approved' : 'Participation revoked' });
    },
    onError: (err: unknown) => showError(err, 'Failed to update participation'),
  });

  const adminMutation = useMutation({
    mutationFn: ({ userId, isAdmin }: { userId: string; isAdmin: boolean }) =>
      usersService.updateAdmin(userId, isAdmin),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'users'] });
      toast({ title: variables.isAdmin ? 'Admin granted' : 'Admin revoked' });
    },
    onError: (err: unknown) => showError(err, 'Failed to update role'),
  });

  const deleteMutation = useMutation({
    mutationFn: (userId: string) => usersService.deleteUser(userId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'users'] });
      invalidateParticipants();
      toast({ title: 'User deleted' });
    },
    onError: (err: unknown) => showError(err, 'Failed to delete user'),
  });

  const busy =
    paymentMutation.isPending ||
    approveMutation.isPending ||
    adminMutation.isPending ||
    deleteMutation.isPending;

  const handleToggleAdmin = (u: UserListItem) => {
    const msg = u.isAdmin
      ? `Revoke admin from ${u.firstName} ${u.lastName}?`
      : `Make ${u.firstName} ${u.lastName} an admin?`;
    if (confirm(msg)) {
      adminMutation.mutate({ userId: u.id, isAdmin: !u.isAdmin });
    }
  };

  const formatDate = (dateString: string) =>
    new Date(dateString).toLocaleDateString('en-GB', {
      day: '2-digit',
      month: 'short',
      year: 'numeric',
    });

  const handleDelete = (u: UserListItem) => {
    if (confirm(`Delete ${u.firstName} ${u.lastName} (${u.email})? This cannot be undone.`)) {
      deleteMutation.mutate(u.id);
    }
  };

  const term = search.trim().toLowerCase();
  const filtered = term
    ? users.filter(
        (u) =>
          `${u.firstName} ${u.lastName}`.toLowerCase().includes(term) ||
          u.email.toLowerCase().includes(term)
      )
    : users;

  const seasonLabel = seasons.find((s) => s.id === seasonId)?.name;

  return (
    <div>
      <Card>
        <CardHeader>
          <CardTitle>Users</CardTitle>
          <CardDescription>
            Manage user accounts. Payment and participation are per-season.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {isError ? (
            <ErrorDisplay
              title="Failed to Load Users"
              message="Could not load the user list"
              error={error}
              onRetry={refetch}
            />
          ) : (
            <>
              <div className="mb-4 flex flex-col sm:flex-row gap-3 sm:items-center">
                <div className="flex items-center gap-2">
                  <label className="text-sm font-medium">Season:</label>
                  <select
                    className="border rounded px-3 py-2 min-w-[180px] bg-background text-foreground"
                    value={seasonId || ''}
                    onChange={(e) => setSelectedSeasonId(e.target.value || undefined)}
                    data-testid="season-select"
                  >
                    {seasons.length === 0 && <option value="">No seasons</option>}
                    {seasons.map((s) => (
                      <option key={s.id} value={s.id} className="bg-background text-foreground">
                        {s.name}
                        {s.isActive ? ' (Active)' : ''}
                      </option>
                    ))}
                  </select>
                </div>
                <Input
                  className="max-w-xs"
                  placeholder="Search by name or email..."
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  data-testid="user-search"
                />
              </div>

              {isLoading ? (
                <div className="text-center py-8 text-muted-foreground">Loading users...</div>
              ) : filtered.length === 0 ? (
                <div className="text-center py-12 text-muted-foreground">
                  {users.length === 0 ? 'No users found.' : 'No users match your search.'}
                </div>
              ) : (
                <div className="rounded-md border overflow-x-auto">
                  <Table data-testid="users-table">
                    <TableHeader>
                      <TableRow>
                        <TableHead>User</TableHead>
                        <TableHead>Email</TableHead>
                        <TableHead>Role</TableHead>
                        <TableHead>Payment</TableHead>
                        <TableHead>Participation</TableHead>
                        <TableHead>Joined</TableHead>
                        <TableHead className="text-right">Actions</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {filtered.map((u) => {
                        const isSelf = u.id === currentUser?.id;
                        const participation = participationByUser.get(u.id);
                        return (
                          <TableRow key={u.id} data-testid={`user-row-${u.id}`}>
                            <TableCell>
                              <div className="flex items-center gap-3">
                                <UserAvatar
                                  firstName={u.firstName}
                                  lastName={u.lastName}
                                  className="w-8 h-8 text-xs"
                                />
                                <span className="font-medium">
                                  {u.firstName} {u.lastName}
                                  {isSelf && (
                                    <span className="text-muted-foreground font-normal">
                                      {' '}
                                      (you)
                                    </span>
                                  )}
                                </span>
                              </div>
                            </TableCell>
                            <TableCell className="text-muted-foreground">{u.email}</TableCell>
                            <TableCell>
                              <Button
                                size="sm"
                                variant={u.isAdmin ? 'default' : 'outline'}
                                disabled={busy || isSelf}
                                title={isSelf ? "You can't change your own role" : undefined}
                                onClick={() => handleToggleAdmin(u)}
                                data-testid={`toggle-admin-${u.id}`}
                              >
                                {u.isAdmin ? (
                                  <>
                                    <ShieldCheck className="w-3 h-3 mr-1" /> Admin
                                  </>
                                ) : (
                                  'Member'
                                )}
                              </Button>
                            </TableCell>
                            <TableCell>
                              {participation ? (
                                <Button
                                  size="sm"
                                  variant={participation.isPaid ? 'default' : 'outline'}
                                  disabled={busy}
                                  onClick={() =>
                                    paymentMutation.mutate({
                                      participationId: participation.id,
                                      isPaid: !participation.isPaid,
                                    })
                                  }
                                  data-testid={`toggle-paid-${u.id}`}
                                >
                                  {participation.isPaid ? 'Paid' : 'Not paid'}
                                </Button>
                              ) : (
                                <span className="text-muted-foreground text-sm">—</span>
                              )}
                            </TableCell>
                            <TableCell>
                              {participation ? (
                                <Button
                                  size="sm"
                                  variant={participation.isApproved ? 'default' : 'outline'}
                                  disabled={busy}
                                  onClick={() =>
                                    approveMutation.mutate({
                                      participationId: participation.id,
                                      isApproved: !participation.isApproved,
                                    })
                                  }
                                  data-testid={`toggle-approved-${u.id}`}
                                >
                                  {participation.isApproved ? 'Approved' : 'Pending'}
                                </Button>
                              ) : (
                                <span className="text-muted-foreground text-sm">Not in season</span>
                              )}
                            </TableCell>
                            <TableCell className="text-muted-foreground text-sm">
                              {formatDate(u.createdAt)}
                            </TableCell>
                            <TableCell className="text-right">
                              <Button
                                size="sm"
                                variant="destructive"
                                disabled={busy || isSelf}
                                title={isSelf ? "You can't delete your own account" : undefined}
                                onClick={() => handleDelete(u)}
                                data-testid={`delete-user-${u.id}`}
                              >
                                <Trash2 className="w-4 h-4" />
                              </Button>
                            </TableCell>
                          </TableRow>
                        );
                      })}
                    </TableBody>
                  </Table>
                </div>
              )}

              {!isLoading && filtered.length > 0 && (
                <div className="mt-4 text-sm text-muted-foreground">
                  Showing {filtered.length} of {users.length} user{users.length !== 1 ? 's' : ''}
                  {seasonLabel ? ` · payment & participation for ${seasonLabel}` : ''}
                </div>
              )}
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
