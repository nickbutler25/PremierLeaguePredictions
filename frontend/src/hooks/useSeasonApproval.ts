import { useQuery } from '@tanstack/react-query';
import { seasonParticipationService } from '@/services/seasonParticipation';
import { adminService } from '@/services/admin';

export function useSeasonApproval() {
  // Get active season (using public endpoint)
  const { data: activeSeason, isLoading: loadingSeasons } = useQuery({
    queryKey: ['active-season'],
    queryFn: () => adminService.getActiveSeason(),
    retry: false,
  });

  // Get user's approval status for active season
  const { data: isApproved, isLoading: loadingApproval, isError } = useQuery({
    queryKey: ['season-approval', activeSeason?.id],
    queryFn: async () => {
      if (!activeSeason?.id) return false;
      try {
        const result = await seasonParticipationService.checkParticipation(activeSeason.id);
        return result;
      } catch (error) {
        console.error('Error checking season approval:', error);
        return false;
      }
    },
    enabled: !!activeSeason,
    retry: false,
  });

  const needsApproval = !!activeSeason && (isApproved === false || isError);

  return {
    activeSeason,
    isApproved: isApproved ?? false,
    isLoading: loadingSeasons || loadingApproval,
    needsApproval,
  };
}
