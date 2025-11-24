import { useEffect } from 'react';
import { useSignalR, type AutoPickAssignedData } from '@/contexts/SignalRContext';
import { useToast } from '@/hooks/use-toast';
import { useQueryClient } from '@tanstack/react-query';

export function useAutoPickNotifications() {
  const { onAutoPickAssigned, offAutoPickAssigned, isConnected } = useSignalR();
  const { toast } = useToast();
  const queryClient = useQueryClient();

  useEffect(() => {
    if (!isConnected) return;

    const handleAutoPickAssigned = (data: AutoPickAssignedData) => {
      console.log('Auto-pick assigned:', data);

      // Show toast notification
      toast({
        title: 'Pick Automatically Assigned',
        description: `${data.teamName} has been assigned for Gameweek ${data.gameweekNumber} because you missed the deadline.`,
        duration: 10000, // Show for 10 seconds
      });

      // Invalidate queries to refresh the UI
      queryClient.invalidateQueries({ queryKey: ['dashboard'] });
      queryClient.invalidateQueries({ queryKey: ['picks'] });
      queryClient.invalidateQueries({ queryKey: ['league-standings'] });
    };

    onAutoPickAssigned(handleAutoPickAssigned);

    return () => {
      offAutoPickAssigned(handleAutoPickAssigned);
    };
  }, [isConnected, onAutoPickAssigned, offAutoPickAssigned, toast, queryClient]);
}
