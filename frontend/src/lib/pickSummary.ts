import type { PickSummary } from '@/types';

/**
 * Human-readable description of a pick — "GW5 — Arsenal 2-1 Chelsea (won)".
 *
 * Shared by every way a pick is drawn (crest, form badge), each of which shows too little on
 * its own to identify the match. It is the tooltip and the screen-reader label for all of them.
 */
export function describePick(pick: PickSummary, showGameweek = false): string {
  const { teamName, opponentName, teamScore, opponentScore, outcome, isLive } = pick;

  const prefix = showGameweek ? `GW${pick.gameweekNumber} — ` : '';

  if (teamScore == null || opponentScore == null) {
    return prefix + (opponentName ? `${teamName} vs ${opponentName} — not started` : teamName);
  }

  const score = `${teamName} ${teamScore}-${opponentScore} ${opponentName ?? ''}`.trim();
  const verb = isLive
    ? { Win: 'winning', Draw: 'drawing', Loss: 'losing', Pending: 'yet to start' }[outcome]
    : { Win: 'won', Draw: 'drew', Loss: 'lost', Pending: 'yet to start' }[outcome];

  return `${prefix}${score} (${verb})`;
}
