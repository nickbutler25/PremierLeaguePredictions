import { NavLink } from 'react-router-dom';
import { Home, Radio, Trophy, Skull, Settings } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { cn } from '@/lib/utils';

interface Tab {
  to: string;
  label: string;
  icon: LucideIcon;
  testId: string;
  /** Matches child routes too, so /gameweek/12 keeps the Gameweek tab lit. */
  matchPrefix?: boolean;
}

const tabs: Tab[] = [
  { to: '/dashboard', label: 'Home', icon: Home, testId: 'mobile-dashboard-link' },
  {
    to: '/gameweek',
    label: 'Gameweek',
    icon: Radio,
    testId: 'mobile-gameweek-link',
    matchPrefix: true,
  },
  { to: '/league', label: 'League', icon: Trophy, testId: 'mobile-league-link' },
  { to: '/eliminations', label: 'Out', icon: Skull, testId: 'mobile-eliminations-link' },
];

const adminTab: Tab = {
  to: '/admin',
  label: 'Admin',
  icon: Settings,
  testId: 'mobile-admin-link',
  matchPrefix: true,
};

/**
 * Bottom tab bar for phones.
 *
 * The header nav is `hidden md:flex`, so without this there is no way to reach the league,
 * the gameweek or the eliminations on a phone at all — only the logo and the avatar are
 * tappable. Hidden at `md` and up for the same reason: the header nav takes over there, and
 * two navigations on one screen is one too many.
 *
 * Sits above the home indicator via `env(safe-area-inset-bottom)`, which is zero on anything
 * that has no such bar, so the same padding is correct on Android and in a desktop browser
 * shrunk to phone width.
 */
export function MobileNav({ isAdmin }: { isAdmin: boolean }) {
  // Five is the most iOS shows before collapsing the rest behind "More", and admins land
  // exactly on it.
  const items = isAdmin ? [...tabs, adminTab] : tabs;

  // Named apart from the header's "Main navigation": both are in the DOM at once — only CSS
  // hides one — so sharing a name would leave a screen reader two landmarks it cannot tell
  // apart.

  return (
    <nav
      className={cn(
        'fixed inset-x-0 bottom-0 z-40 md:hidden',
        'border-t bg-card/95 backdrop-blur-sm dark:bg-card/90',
        'pb-[env(safe-area-inset-bottom)]'
      )}
      role="navigation"
      aria-label="Mobile navigation"
      data-testid="mobile-navigation"
    >
      <ul className="flex items-stretch justify-around">
        {items.map((tab) => (
          <li key={tab.to} className="flex-1">
            <NavLink
              to={tab.to}
              end={!tab.matchPrefix}
              className={({ isActive }) =>
                cn(
                  // 56px clears Apple's 44px minimum touch target with room for the label.
                  'flex h-14 flex-col items-center justify-center gap-0.5 text-[10px] font-medium',
                  'transition-colors',
                  isActive ? 'text-primary' : 'text-muted-foreground hover:text-foreground'
                )
              }
              data-testid={tab.testId}
            >
              {({ isActive }) => (
                <>
                  <tab.icon
                    className={cn('h-5 w-5', isActive && 'stroke-[2.5]')}
                    aria-hidden="true"
                  />
                  <span>{tab.label}</span>
                </>
              )}
            </NavLink>
          </li>
        ))}
      </ul>
    </nav>
  );
}
