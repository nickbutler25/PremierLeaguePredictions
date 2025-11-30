import { useQuery } from '@tanstack/react-query';
import { AxiosError } from 'axios';
import { seasonParticipationService } from '@/services/seasonParticipation';
import { adminService } from '@/services/admin';

export function useSeasonApproval() {
  // Get active season (using public endpoint)
  const { data: activeSeason, isLoading: loadingSeasons, error: seasonError } = useQuery({
    queryKey: ['active-season'],
    queryFn: () => adminService.getActiveSeason(),
    retry: false,
  });

  // Get user's approval status for active season
  const { data: isApproved, isLoading: loadingApproval, error: approvalError } = useQuery({
    queryKey: ['season-approval', activeSeason?.name],
    queryFn: async () => {
      if (!activeSeason?.name) return false;
      try {
        const result = await seasonParticipationService.checkParticipation(activeSeason.name);
        console.log('useSeasonApproval: checkParticipation returned:', result, 'for season:', activeSeason.name);
        return result;
      } catch (error) {
        console.error('Error checking season approval:', error);
        // Only return false for non-network errors
        // Network errors should propagate to show the API down message
        const axiosError = error as AxiosError;
        if (!axiosError.response || (axiosError.response?.status && axiosError.response.status >= 500)) {
          throw error; // Re-throw network/server errors
        }
        return false;
      }
    },
    enabled: !!activeSeason,
    retry: false,
  });

  // Check if there's an API error (network or server error)
  const hasApiError = seasonError || approvalError;
  const apiError = (seasonError || approvalError) as AxiosError | null;
  const isApiDown = apiError && (!apiError.response || (apiError.response?.status && apiError.response.status >= 500));

  const needsApproval = !!activeSeason && isApproved === false;
  console.log('useSeasonApproval: needsApproval=', needsApproval, 'isApproved=', isApproved, 'activeSeason=', activeSeason?.name, 'isApiDown=', isApiDown);

  return {
    activeSeason,
    isApproved: isApproved ?? false,
    isLoading: loadingSeasons || loadingApproval,
    needsApproval,
    isApiDown: !!isApiDown,
    error: hasApiError,
  };
}
