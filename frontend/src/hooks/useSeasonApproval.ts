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
  const { data: isApproved, isLoading: loadingApproval } = useQuery({
    queryKey: ['season-approval', activeSeason?.name],
    queryFn: async () => {
      if (!activeSeason?.name) return false;
      try {
        const result = await seasonParticipationService.checkParticipation(activeSeason.name);
        console.log('useSeasonApproval: checkParticipation returned:', result, 'for season:', activeSeason.name);
        return result;
      } catch (error) {
        console.error('Error checking season approval:', error);
        // Return false instead of throwing - let the component handle the no-approval state
        return false;
      }
    },
    enabled: !!activeSeason,
    retry: false,
  });

  const needsApproval = !!activeSeason && isApproved === false;
  console.log('useSeasonApproval: needsApproval=', needsApproval, 'isApproved=', isApproved, 'activeSeason=', activeSeason?.name);

  return {
    activeSeason,
    isApproved: isApproved ?? false,
    isLoading: loadingSeasons || loadingApproval,
    needsApproval,
  };
}
