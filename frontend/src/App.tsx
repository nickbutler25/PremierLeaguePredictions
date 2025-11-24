import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import { GoogleOAuthProvider } from '@react-oauth/google';
import { AuthProvider, useAuth } from '@/contexts/AuthContext';
import { ThemeProvider } from '@/contexts/ThemeContext';
import { SignalRProvider } from '@/contexts/SignalRContext';
import { queryClient } from '@/lib/queryClient';
import { Layout } from '@/components/layout/Layout';
import { LoginPage } from '@/pages/LoginPage';
import { DashboardPage } from '@/pages/DashboardPage';
import { PendingApprovalPage } from '@/pages/PendingApprovalPage';
import { LeagueStandings } from '@/components/league/LeagueStandings';
import { SeasonManagementPage } from '@/pages/admin/SeasonManagementPage';
import { SeasonApprovalsPage } from '@/pages/admin/SeasonApprovalsPage';
import EliminationManagementPage from '@/pages/admin/EliminationManagementPage';
import { BackfillPicksPage } from '@/pages/admin/BackfillPicksPage';
import { Toaster } from '@/components/ui/toaster';
import { useSeasonApproval } from '@/hooks/useSeasonApproval';
import { ErrorBoundary } from '@/components/ErrorBoundary';

function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const { isAuthenticated } = useAuth();
  return isAuthenticated ? <>{children}</> : <Navigate to="/login" replace />;
}

function ApprovalCheckRoute({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, user } = useAuth();
  const { needsApproval, isLoading } = useSeasonApproval();

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  // Admins bypass approval check
  if (user?.isAdmin) {
    return <>{children}</>;
  }

  if (isLoading) {
    return (
      <div className="min-h-screen bg-background flex items-center justify-center">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-primary"></div>
      </div>
    );
  }

  if (needsApproval) {
    return <Navigate to="/pending-approval" replace />;
  }

  return <>{children}</>;
}

function AdminRoute({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, user } = useAuth();

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  if (!user?.isAdmin) {
    console.log('User is not admin, redirecting to dashboard');
    return <Navigate to="/" replace />;
  }

  return <>{children}</>;
}

function AppRoutes() {
  const { isAuthenticated } = useAuth();

  return (
    <Routes>
      <Route
        path="/login"
        element={isAuthenticated ? <Navigate to="/" replace /> : <LoginPage />}
      />
      <Route
        path="/pending-approval"
        element={
          <ProtectedRoute>
            <PendingApprovalPage />
          </ProtectedRoute>
        }
      />
      <Route
        path="/"
        element={
          <ApprovalCheckRoute>
            <Layout>
              <DashboardPage />
            </Layout>
          </ApprovalCheckRoute>
        }
      />
      <Route
        path="/league"
        element={
          <ApprovalCheckRoute>
            <Layout>
              <div className="container mx-auto p-6">
                <LeagueStandings />
              </div>
            </Layout>
          </ApprovalCheckRoute>
        }
      />
      <Route
        path="/admin"
        element={
          <AdminRoute>
            <Layout>
              <SeasonManagementPage />
            </Layout>
          </AdminRoute>
        }
      />
      <Route
        path="/admin/approvals"
        element={
          <AdminRoute>
            <Layout>
              <SeasonApprovalsPage />
            </Layout>
          </AdminRoute>
        }
      />
      <Route
        path="/admin/eliminations"
        element={
          <AdminRoute>
            <Layout>
              <EliminationManagementPage />
            </Layout>
          </AdminRoute>
        }
      />
      <Route
        path="/admin/backfill"
        element={
          <AdminRoute>
            <Layout>
              <BackfillPicksPage />
            </Layout>
          </AdminRoute>
        }
      />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

function App() {
  const googleClientId = import.meta.env.VITE_GOOGLE_CLIENT_ID;

  if (!googleClientId) {
    throw new Error('Google Client ID is not configured');
  }

  return (
    <ErrorBoundary>
      <ThemeProvider>
        <GoogleOAuthProvider clientId={googleClientId}>
          <QueryClientProvider client={queryClient}>
            <BrowserRouter>
              <AuthProvider>
                <SignalRProvider>
                  <AppRoutes />
                  <Toaster />
                </SignalRProvider>
              </AuthProvider>
            </BrowserRouter>
            <ReactQueryDevtools initialIsOpen={false} />
          </QueryClientProvider>
        </GoogleOAuthProvider>
      </ThemeProvider>
    </ErrorBoundary>
  );
}

export default App;
