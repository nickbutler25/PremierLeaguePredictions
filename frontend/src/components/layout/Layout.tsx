import { Button } from '@/components/ui/button';
import { UserAvatar } from '@/components/UserAvatar';
import { MobileNav } from '@/components/layout/MobileNav';
import { useAuth } from '@/contexts/AuthContext';
import { useTheme } from '@/contexts/ThemeContext';
import { useAutoPickNotifications } from '@/hooks/useAutoPickNotifications';
import { useResultsUpdates } from '@/hooks/useResultsUpdates';
import { useSeasonCreatedNotification } from '@/hooks/useSeasonCreatedNotification';
import { usersService } from '@/services/users';
import { useEffect, type ReactNode } from 'react';
import { Link, NavLink, useNavigate } from 'react-router-dom';

interface LayoutProps {
  children: ReactNode;
}

export function Layout({ children }: LayoutProps) {
  const { user, logout, isAdmin, updateUser } = useAuth();
  const { theme, setTheme } = useTheme();
  const navigate = useNavigate();

  // Apply the user's saved (server-side) theme preference once it's known.
  useEffect(() => {
    if (user?.themePreference && user.themePreference !== theme) {
      setTheme(user.themePreference);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [user?.themePreference]);

  const handleToggleTheme = async () => {
    const next = theme === 'light' ? 'dark' : 'light';
    setTheme(next);

    // Persist to the account, and put the saved value back on the user. The effect above treats
    // user.themePreference as the truth and re-applies it every time this layout mounts — which
    // is on every navigation — so leaving it stale made the next page flip the theme back.
    try {
      updateUser(await usersService.updateTheme(next));
    } catch {
      // Best-effort: the theme still applies locally and persists to localStorage.
    }
  };

  // Subscribe to real-time updates
  useResultsUpdates();
  useAutoPickNotifications();
  useSeasonCreatedNotification();

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  // Transparent in both themes so the body's gradient shows through: an opaque wrapper here
  // paints over it across the whole viewport, and body already covers the canvas.
  return (
    <div className="min-h-screen bg-transparent">
      <a
        href="#main-content"
        className="sr-only focus:not-sr-only focus:absolute focus:top-4 focus:left-4 focus:z-50 focus:px-4 focus:py-2 focus:bg-primary focus:text-primary-foreground focus:rounded"
      >
        Skip to main content
      </a>
      <header
        className="border-b pt-[env(safe-area-inset-top)] bg-card/70 backdrop-blur-sm dark:bg-card/40"
        role="banner"
        data-testid="main-header"
      >
        {/* Tighter on a phone, where this bar sits above a tab bar carrying the navigation and
            every pixel it takes is one the content loses. It cannot go entirely: the theme
            toggle, the profile link and logout live here and have nowhere else on a phone. */}
        <div className="container mx-auto px-3 sm:px-4 py-1.5 sm:py-3 flex items-center justify-between">
          <div className="flex items-center space-x-4 sm:space-x-8">
            <Link
              to="/dashboard"
              className="flex items-center"
              aria-label="Home"
              data-testid="logo-link"
            >
              <img
                src={theme === 'dark' ? '/pl-banner-logo-dark.png' : '/pl-banner-logo-light.png'}
                alt="Premier League Predictions"
                className="h-7 sm:h-10 w-auto"
              />
            </Link>
            <nav
              className="hidden md:flex items-center space-x-1"
              role="navigation"
              aria-label="Main navigation"
              data-testid="main-navigation"
            >
              <NavLink
                to="/dashboard"
                className={({ isActive }) =>
                  `text-sm px-3 py-1.5 rounded-md font-medium transition-colors ${
                    isActive
                      ? 'bg-primary text-primary-foreground'
                      : 'text-foreground hover:bg-accent'
                  }`
                }
                data-testid="dashboard-link"
              >
                Dashboard
              </NavLink>
              <NavLink
                to="/gameweek"
                className={({ isActive }) =>
                  `text-sm px-3 py-1.5 rounded-md font-medium transition-colors ${
                    isActive
                      ? 'bg-primary text-primary-foreground'
                      : 'text-foreground hover:bg-accent'
                  }`
                }
                data-testid="gameweek-link"
              >
                Gameweek
              </NavLink>
              <NavLink
                to="/league"
                className={({ isActive }) =>
                  `text-sm px-3 py-1.5 rounded-md font-medium transition-colors ${
                    isActive
                      ? 'bg-primary text-primary-foreground'
                      : 'text-foreground hover:bg-accent'
                  }`
                }
                data-testid="league-link"
              >
                League
              </NavLink>
              <NavLink
                to="/users"
                className={({ isActive }) =>
                  `text-sm px-3 py-1.5 rounded-md font-medium transition-colors ${
                    isActive
                      ? 'bg-primary text-primary-foreground'
                      : 'text-foreground hover:bg-accent'
                  }`
                }
                data-testid="players-link"
              >
                Players
              </NavLink>
              <NavLink
                to="/danger-zone"
                className={({ isActive }) =>
                  `text-sm px-3 py-1.5 rounded-md font-medium transition-colors ${
                    isActive
                      ? 'bg-primary text-primary-foreground'
                      : 'text-foreground hover:bg-accent'
                  }`
                }
                data-testid="danger-zone-link"
              >
                Danger Zone
              </NavLink>
              <NavLink
                to="/eliminations"
                className={({ isActive }) =>
                  `text-sm px-3 py-1.5 rounded-md font-medium transition-colors ${
                    isActive
                      ? 'bg-primary text-primary-foreground'
                      : 'text-foreground hover:bg-accent'
                  }`
                }
                data-testid="eliminations-link"
              >
                Eliminations
              </NavLink>
              {isAdmin && (
                <NavLink
                  to="/admin"
                  className={({ isActive }) =>
                    `text-sm px-3 py-1.5 rounded-md font-medium transition-colors ${
                      isActive
                        ? 'bg-primary text-primary-foreground'
                        : 'text-foreground hover:bg-accent'
                    }`
                  }
                  data-testid="admin-link"
                >
                  Admin
                </NavLink>
              )}
            </nav>
          </div>
          <div className="flex items-center space-x-2 sm:space-x-4">
            <Button
              data-testid="theme-toggle-button"
              variant="outline"
              size="sm"
              onClick={handleToggleTheme}
              className="w-8 h-8 sm:w-9 sm:h-9 px-0"
              aria-label={`Switch to ${theme === 'light' ? 'dark' : 'light'} mode`}
              title={`Switch to ${theme === 'light' ? 'dark' : 'light'} mode`}
            >
              {theme === 'light' ? (
                <svg
                  className="h-4 w-4"
                  fill="none"
                  viewBox="0 0 24 24"
                  stroke="currentColor"
                  aria-hidden="true"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth={2}
                    d="M20.354 15.354A9 9 0 018.646 3.646 9.003 9.003 0 0012 21a9.003 9.003 0 008.354-5.646z"
                  />
                </svg>
              ) : (
                <svg
                  className="h-4 w-4"
                  fill="none"
                  viewBox="0 0 24 24"
                  stroke="currentColor"
                  aria-hidden="true"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth={2}
                    d="M12 3v1m0 16v1m9-9h-1M4 12H3m15.364 6.364l-.707-.707M6.343 6.343l-.707-.707m12.728 0l-.707.707M6.343 17.657l-.707.707M16 12a4 4 0 11-8 0 4 4 0 018 0z"
                  />
                </svg>
              )}
            </Button>
            {user && (
              <>
                <Link
                  to="/profile"
                  className="flex items-center space-x-2 rounded-md hover:bg-accent px-1 py-0.5 transition-colors"
                  data-testid="user-info"
                  aria-label="View profile"
                >
                  <UserAvatar
                    firstName={user.firstName}
                    lastName={user.lastName}
                    className="w-6 h-6 sm:w-8 sm:h-8 text-xs"
                  />
                  <span
                    className="text-xs sm:text-sm font-medium hidden sm:inline"
                    aria-label="Current user"
                    data-testid="user-name"
                  >
                    {user.firstName} {user.lastName}
                  </span>
                </Link>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={handleLogout}
                  aria-label="Logout"
                  className="text-xs sm:text-sm px-2 sm:px-3 h-8 sm:h-9"
                  data-testid="logout-button"
                >
                  Logout
                </Button>
              </>
            )}
          </div>
        </div>
      </header>
      {/* The bar is fixed, so the page has to reserve its height or the last card on every
          screen sits under it. Only below md, where the bar exists. */}
      <main
        id="main-content"
        role="main"
        className="pb-[calc(3.5rem+env(safe-area-inset-bottom))] md:pb-0"
      >
        {children}
      </main>
      {user && <MobileNav isAdmin={isAdmin} />}
    </div>
  );
}
