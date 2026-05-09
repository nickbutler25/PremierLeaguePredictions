import { useRef, useState, useEffect } from 'react';
import type { Team } from '@/types';

interface TeamNameProps {
  team: Team;
  className?: string;
}

/**
 * Renders a team name that adapts to its actual available width:
 *   < 60px   → shortName  (e.g. "BHA")
 *   < 180px  → mediumName (e.g. "Brighton")
 *   >= 180px → name       (e.g. "Brighton & Hove Albion")
 *
 * Uses ResizeObserver on the span itself. The span uses flex-1 so its width
 * is determined by available space (not text content), preventing feedback loops.
 */
export function TeamName({ team, className }: TeamNameProps) {
  const ref = useRef<HTMLSpanElement>(null);
  const [width, setWidth] = useState(999);

  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    const ro = new ResizeObserver(([entry]) => {
      setWidth(entry.contentRect.width);
    });
    ro.observe(el);
    return () => ro.disconnect();
  }, []);

  let displayName: string;
  if (width < 60) {
    displayName = team.shortName ?? team.name;
  } else if (width < 180) {
    displayName = team.mediumName ?? team.name;
  } else {
    displayName = team.name;
  }

  return (
    <span ref={ref} className={`flex-1 min-w-0 ${className ?? ''}`}>
      {displayName}
    </span>
  );
}
