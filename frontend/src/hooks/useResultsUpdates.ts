import { useEffect } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { useSignalR } from '@/contexts/SignalRContext';
import type { ResultsUpdateData } from '@/contexts/SignalRContext';

export function useResultsUpdates() {
  const { onResultsUpdated, offResultsUpdated, subscribeToResults, unsubscribeFromResults, isConnected } = useSignalR();
  const queryClient = useQueryClient();

  useEffect(() => {
    if (!isConnected) return;

    // Subscribe to results updates
    subscribeToResults();

    // Handler for results updated
    const handleResultsUpdated = (data: ResultsUpdateData) => {
      console.log('✨ Results updated:', data);
      console.log(`📊 ${data.fixturesUpdated} fixture(s) updated - ${data.message}`);

      // Log fixture details
      data.updatedFixtures.forEach((fixture) => {
        console.log(
          `⚽ ${fixture.homeTeam} ${fixture.homeScore ?? '-'} - ${fixture.awayScore ?? '-'} ${fixture.awayTeam} (${fixture.status})`
        );
      });

      // Invalidate relevant queries to refresh data
      queryClient.invalidateQueries({ queryKey: ['dashboard'] });
      queryClient.invalidateQueries({ queryKey: ['fixtures'] });
      queryClient.invalidateQueries({ queryKey: ['gameweeks'] });
      queryClient.invalidateQueries({ queryKey: ['picks'] });
      queryClient.invalidateQueries({ queryKey: ['league-standings'] });
    };

    // Register handler
    onResultsUpdated(handleResultsUpdated);

    // Cleanup
    return () => {
      offResultsUpdated(handleResultsUpdated);
      unsubscribeFromResults();
    };
  }, [isConnected, onResultsUpdated, offResultsUpdated, subscribeToResults, unsubscribeFromResults, queryClient]);
}
