import { createContext, useContext, useState, useEffect } from 'react';
import type { ReactNode } from 'react';
import type { User, AuthResponse } from '@/types';
import { authService } from '@/services/auth';

interface AuthContextType {
  user: User | null;
  token: string | null;
  login: (authData: AuthResponse) => Promise<void>;
  logout: () => Promise<void>;
  isAuthenticated: boolean;
  isAdmin: boolean;
  isLoading: boolean;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export { AuthContext };

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);

  useEffect(() => {
    // On mount, check if user is authenticated by making an API call
    // The auth cookie will be sent automatically
    const checkAuth = async () => {
      setIsLoading(true);
      try {
        // Try to get current user from the server using the httpOnly cookie
        const currentUser = await authService.getCurrentUser();
        if (currentUser) {
          setUser(currentUser);
          setToken('cookie'); // Placeholder to indicate auth state
        }
      } catch (error) {
        console.error('Failed to restore authentication:', error);
        // If error, user will need to log in
      } finally {
        setIsLoading(false);
      }
    };
    checkAuth();
  }, []);

  const login = async (authData: AuthResponse) => {
    // Token is now in httpOnly cookie, so we don't store it
    // We only keep the user data in memory (not localStorage)
    setUser(authData.user);
    // Note: Token is implicitly available via cookie
    setToken('cookie'); // Placeholder to indicate auth state
  };

  const logout = async () => {
    try {
      // Call logout endpoint to clear the cookie
      await authService.logout();
    } catch (error) {
      console.error('Logout API call failed:', error);
    } finally {
      setToken(null);
      setUser(null);
    }
  };

  const value: AuthContextType = {
    user,
    token,
    login,
    logout,
    isAuthenticated: !!token && !!user,
    isAdmin: user?.isAdmin ?? false,
    isLoading,
  };

  // Show loading spinner while checking authentication on initial mount
  if (isLoading) {
    return (
      <div className="min-h-screen bg-background flex items-center justify-center">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-primary"></div>
      </div>
    );
  }

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}
