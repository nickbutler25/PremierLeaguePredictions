import { useEffect } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { useSignalR } from '@/contexts/SignalRContext';
import { useToast } from '@/hooks/use-toast';

export function useSeasonCreatedNotification() {
  const { onSeasonCreated, offSeasonCreated } = useSignalR();
  const queryClient = useQueryClient();
  const { toast } = useToast();

  useEffect(() => {
    const handleSeasonCreated = (data: { seasonId: string; seasonName: string; message: string }) => {
      console.log('Season created notification received:', data);

      // Show toast notification
      toast({
        title: 'New Season Created',
        description: `${data.seasonName} is now available`,
      });

      // Invalidate relevant queries to refresh data
      queryClient.invalidateQueries({ queryKey: ['dashboard'] });
      queryClient.invalidateQueries({ queryKey: ['seasons'] });
      queryClient.invalidateQueries({ queryKey: ['admin', 'seasons'] });
      queryClient.invalidateQueries({ queryKey: ['activeSeason'] });
    };

    onSeasonCreated(handleSeasonCreated);

    return () => {
      offSeasonCreated(handleSeasonCreated);
    };
  }, [onSeasonCreated, offSeasonCreated, queryClient, toast]);
}
