import { useEffect } from 'react';
import { useQuery, useMutation } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '@/contexts/AuthContext';
import { useSignalR } from '@/contexts/SignalRContext';
import { seasonParticipationService } from '@/services/seasonParticipation';
import { adminService } from '@/services/admin';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Clock, CheckCircle2, AlertCircle } from 'lucide-react';
import { useToast } from '@/hooks/use-toast';

export function PendingApprovalPage() {
  const { user, logout } = useAuth();
  const { onSeasonApprovalUpdate, offSeasonApprovalUpdate, isConnected } = useSignalR();
  const { toast } = useToast();
  const navigate = useNavigate();

  // Get active season
  const { data: activeSeason } = useQuery({
    queryKey: ['active-season'],
    queryFn: () => adminService.getActiveSeason(),
    retry: false,
  });

  // Get user's participation status for active season
  const { data: participation, refetch: refetchParticipation } = useQuery({
    queryKey: ['participation', activeSeason?.id],
    queryFn: () => seasonParticipationService.getParticipation(activeSeason!.id),
    enabled: !!activeSeason,
    retry: false,
  });

  // Request participation mutation
  const requestParticipationMutation = useMutation({
    mutationFn: (seasonId: string) => seasonParticipationService.requestParticipation(seasonId),
    onSuccess: () => {
      refetchParticipation();
    },
    onError: (error: any) => {
      // If it's a 409 Conflict (duplicate), just refetch to get the existing participation
      if (error?.response?.status === 409) {
        refetchParticipation();
      }
    },
  });

  // Auto-request participation if not already requested
  useEffect(() => {
    if (activeSeason && !participation && !requestParticipationMutation.isPending && !requestParticipationMutation.isError) {
      requestParticipationMutation.mutate(activeSeason.id);
    }
  }, [activeSeason, participation]);

  // Listen for SignalR approval updates
  useEffect(() => {
    const handleApprovalUpdate = (data: { isApproved: boolean; seasonName: string; timestamp: string }) => {
      console.log('Received approval update:', data);

      if (data.isApproved) {
        toast({
          title: 'Approved!',
          description: `You've been approved for ${data.seasonName}. Redirecting to dashboard...`,
          duration: 3000,
        });

        // Redirect to dashboard after a short delay
        setTimeout(() => {
          navigate('/');
        }, 1500);
      } else {
        toast({
          title: 'Participation Rejected',
          description: `Your participation request for ${data.seasonName} was not approved.`,
          variant: 'destructive',
        });
      }
    };

    onSeasonApprovalUpdate(handleApprovalUpdate);

    return () => {
      offSeasonApprovalUpdate(handleApprovalUpdate);
    };
  }, [onSeasonApprovalUpdate, offSeasonApprovalUpdate, toast, navigate]);

  const handleLogout = () => {
    logout();
    window.location.href = '/login';
  };

  if (!activeSeason) {
    return (
      <div className="min-h-screen bg-background flex items-center justify-center p-4">
        <Card className="max-w-md w-full">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <AlertCircle className="w-6 h-6 text-yellow-500" />
              No Active Season
            </CardTitle>
            <CardDescription>
              There is currently no active season. Please check back later.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <Button onClick={handleLogout} variant="outline" className="w-full">
              Logout
            </Button>
          </CardContent>
        </Card>
      </div>
    );
  }

  if (requestParticipationMutation.isPending) {
    return (
      <div className="min-h-screen bg-background flex items-center justify-center p-4">
        <Card className="max-w-md w-full">
          <CardContent className="pt-6">
            <div className="text-center space-y-4">
              <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-primary mx-auto"></div>
              <p className="text-muted-foreground">Submitting your participation request...</p>
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }

  if (participation?.isApproved) {
    // This shouldn't happen as they should be redirected, but just in case
    window.location.href = '/';
    return null;
  }

  return (
    <div className="min-h-screen bg-background flex items-center justify-center p-4">
      <Card className="max-w-md w-full">
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Clock className="w-6 h-6 text-blue-500" />
            Approval Pending
          </CardTitle>
          <CardDescription>
            Your participation request is awaiting admin approval
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-6">
          <div className="bg-blue-50 dark:bg-blue-950/30 border border-blue-200 dark:border-blue-800 rounded-lg p-4">
            <div className="flex items-start gap-3">
              <div className="flex-shrink-0">
                {user?.photoUrl && (
                  <img
                    src={user.photoUrl}
                    alt={user.firstName}
                    className="w-12 h-12 rounded-full"
                  />
                )}
              </div>
              <div className="flex-1 space-y-1">
                <p className="font-medium">
                  {user?.firstName} {user?.lastName}
                </p>
                <p className="text-sm text-muted-foreground">{user?.email}</p>
                <p className="text-sm">
                  <span className="font-medium">Season:</span> {activeSeason.name}
                </p>
                {participation && (
                  <p className="text-sm text-muted-foreground">
                    Requested: {new Date(participation.requestedAt).toLocaleDateString('en-GB', {
                      day: '2-digit',
                      month: 'short',
                      year: 'numeric',
                      hour: '2-digit',
                      minute: '2-digit',
                    })}
                  </p>
                )}
              </div>
            </div>
          </div>

          <div className="space-y-3">
            <div className="flex items-center gap-2 text-sm">
              <CheckCircle2 className="w-4 h-4 text-green-500" />
              <span>Your request has been submitted</span>
            </div>
            <div className="flex items-center gap-2 text-sm">
              <Clock className="w-4 h-4 text-blue-500" />
              <span>Waiting for admin approval</span>
            </div>
            <div className="flex items-center gap-2 text-sm">
              <div className={`w-2 h-2 rounded-full ${isConnected ? 'bg-green-500' : 'bg-gray-400'}`}></div>
              <span className="text-muted-foreground">
                {isConnected ? 'Real-time updates active' : 'Connecting...'}
              </span>
            </div>
          </div>

          <div className="pt-4 space-y-3">
            <p className="text-sm text-muted-foreground">
              An administrator will review your request shortly. You'll be automatically redirected
              to the dashboard once your participation is approved.
            </p>

            {!user?.isPaid && (
              <div className="bg-yellow-50 dark:bg-yellow-950/30 border border-yellow-200 dark:border-yellow-800 rounded-lg p-3">
                <p className="text-sm text-yellow-800 dark:text-yellow-200">
                  <strong>Note:</strong> Make sure you've completed payment before approval.
                </p>
              </div>
            )}
          </div>

          <div className="pt-4 border-t space-y-2">
            <Button
              onClick={() => refetchParticipation()}
              variant="default"
              className="w-full"
            >
              Check Approval Status
            </Button>
            <Button onClick={handleLogout} variant="outline" className="w-full">
              Logout
            </Button>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
