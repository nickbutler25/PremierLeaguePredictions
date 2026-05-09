import type { Team } from '@/types';

interface TeamNameProps {
  team: Team;
  className?: string;
}

/**
 * Renders a team name at three sizes based on available viewport width:
 *   < 480px  → shortName  (e.g. "BHA")
 *   480–639px → mediumName (e.g. "Brighton")
 *   640px+   → name       (e.g. "Brighton & Hove Albion")
 *
 * Falls back gracefully: mediumName → name, shortName → name.
 */
export function TeamName({ team, className }: TeamNameProps) {
  return (
    <span className={className}>
      <span className="xs:hidden">{team.shortName ?? team.name}</span>
      <span className="hidden xs:inline sm:hidden">{team.mediumName ?? team.name}</span>
      <span className="hidden sm:inline">{team.name}</span>
    </span>
  );
}
