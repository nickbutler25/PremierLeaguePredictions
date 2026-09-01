import { useQuery } from '@tanstack/react-query';
import { leagueService } from '@/services/league';
import { useAuth } from '@/contexts/AuthContext';
import type { StandingEntry } from '@/types';

/**
 * The signed-in player's row in the league table, and whether they are out.
 *
 * The summary, the picks control and the dashboard layout all need the same answer, and three
 * copies of "find my row, read isEliminated" is three chances for them to disagree — one showing
 * the eliminated banner while another still offers a pick. React Query dedupes the underlying
 * request, so calling this from several components costs nothing.
 */
export function useMyStanding(): {
  standing: StandingEntry | undefined;
  isEliminated: boolean;
  eliminatedInGameweek: number | undefined;
  isLoading: boolean;
} {
  const { user } = useAuth();

  const { data, isLoading } = useQuery({
    queryKey: ['league-standings'],
    queryFn: () => leagueService.getStandings(),
  });

  const standing = data?.standings.find((s) => s.userId === user?.id);

  return {
    standing,
    isEliminated: standing?.isEliminated ?? false,
    eliminatedInGameweek: standing?.eliminatedInGameweek,
    isLoading,
  };
}
