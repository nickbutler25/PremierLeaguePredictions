import { useRef, useState, useEffect } from 'react';
import type { Team } from '@/types';

interface TeamNameProps {
  team: Team;
  className?: string;
}

export function TeamName({ team, className }: TeamNameProps) {
  const ref = useRef<HTMLDivElement>(null);
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
    displayName = team.code ?? team.name;
  } else if (width < 180) {
    displayName = team.mediumName ?? team.name;
  } else {
    displayName = team.name;
  }

  return (
    <div
      ref={ref}
      className={`flex-1 min-w-0 overflow-hidden whitespace-nowrap ${className ?? ''}`}
    >
      {displayName}
    </div>
  );
}
