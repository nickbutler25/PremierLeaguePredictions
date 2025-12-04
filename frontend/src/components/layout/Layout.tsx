import { Button } from "@/components/ui/button";
import { useAuth } from "@/contexts/AuthContext";
import { useTheme } from "@/contexts/ThemeContext";
import { useResultsUpdates } from "@/hooks/useResultsUpdates";
import { useAutoPickNotifications } from "@/hooks/useAutoPickNotifications";
import { useSeasonCreatedNotification } from "@/hooks/useSeasonCreatedNotification";
import type { ReactNode } from "react";
import { Link, useNavigate } from "react-router-dom";

interface LayoutProps {
  children: ReactNode;
}

export function Layout({ children }: LayoutProps) {
  const { user, logout, isAdmin } = useAuth();
  const { theme, toggleTheme } = useTheme();
  const navigate = useNavigate();

  // Subscribe to real-time updates
  useResultsUpdates();
  useAutoPickNotifications();
  useSeasonCreatedNotification();

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <div className="min-h-screen bg-background">
      <a
        href="#main-content"
        className="sr-only focus:not-sr-only focus:absolute focus:top-4 focus:left-4 focus:z-50 focus:px-4 focus:py-2 focus:bg-primary focus:text-primary-foreground focus:rounded"
      >
        Skip to main content
      </a>
      <header className="border-b" role="banner">
        <div className="container mx-auto px-4 py-3 flex items-center justify-between">
          <div className="flex items-center space-x-8">
            <Link to="/dashboard" className="flex items-center" aria-label="Home">
              <img
                src="/pl-logo-alt.png"
                alt="Premier League Predictions"
                className="h-10 w-auto"
              />
            </Link>
            {isAdmin && (
              <nav className="hidden md:flex space-x-6" role="navigation" aria-label="Main navigation">
                <Link to="/admin" className="text-sm hover:text-primary">
                  Admin
                </Link>
              </nav>
            )}
          </div>
          <div className="flex items-center space-x-4">
            <Button
              variant="outline"
              size="sm"
              onClick={toggleTheme}
              className="w-9 px-0"
              aria-label={`Switch to ${theme === 'light' ? 'dark' : 'light'} mode`}
              title={`Switch to ${theme === 'light' ? 'dark' : 'light'} mode`}
            >
              {theme === 'light' ? (
                <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M20.354 15.354A9 9 0 018.646 3.646 9.003 9.003 0 0012 21a9.003 9.003 0 008.354-5.646z" />
                </svg>
              ) : (
                <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 3v1m0 16v1m9-9h-1M4 12H3m15.364 6.364l-.707-.707M6.343 6.343l-.707-.707m12.728 0l-.707.707M6.343 17.657l-.707.707M16 12a4 4 0 11-8 0 4 4 0 018 0z" />
                </svg>
              )}
            </Button>
            {user && (
              <>
                <div className="flex items-center space-x-2">
                  {user.photoUrl && (
                    <img
                      src={user.photoUrl}
                      alt={`${user.firstName} ${user.lastName}'s profile picture`}
                      className="w-8 h-8 rounded-full"
                    />
                  )}
                  <span className="text-sm font-medium hidden sm:inline" aria-label="Current user">
                    {user.firstName} {user.lastName}
                  </span>
                </div>
                <Button variant="outline" size="sm" onClick={handleLogout} aria-label="Logout">
                  Logout
                </Button>
              </>
            )}
          </div>
        </div>
      </header>
      <main id="main-content" role="main">{children}</main>
    </div>
  );
}
