import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { GoogleLogin } from '@react-oauth/google';
import type { CredentialResponse } from '@react-oauth/google';
import { useAuth } from '@/contexts/AuthContext';
import { authService } from '@/services/auth';
import { apiClient } from '@/services/api';
import { useState } from 'react';
import type { AuthResponse } from '@/types';

export function LoginPage() {
  const { login } = useAuth();
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  const handleGoogleSuccess = async (credentialResponse: CredentialResponse) => {
    if (!credentialResponse.credential) {
      setError('No credential received from Google');
      return;
    }

    setIsLoading(true);
    setError(null);

    try {
      const authResponse = await authService.login(credentialResponse.credential);
      await login(authResponse);
    } catch (err) {
      console.error('Login failed:', err);
      setError('Failed to login. Please try again.');
    } finally {
      setIsLoading(false);
    }
  };

  const handleGoogleError = () => {
    setError('Google login failed. Please try again.');
  };

  const handleDevLogin = async () => {
    setIsLoading(true);
    setError(null);

    try {
      const response = await apiClient.post<AuthResponse>('/api/dev/login-as-admin');
      await login(response.data);
    } catch (err) {
      console.error('Dev login failed:', err);
      setError('Failed to login as admin. Please try again.');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-blue-50 to-indigo-100 dark:from-slate-900 dark:to-slate-800 p-4">
      <Card className="w-full max-w-md">
        <CardHeader className="space-y-1 text-center">
          <CardTitle className="text-3xl font-bold">
            Premier League Predictions
          </CardTitle>
          <CardDescription>
            Sign in to join the competition
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex justify-center">
            <GoogleLogin
              onSuccess={handleGoogleSuccess}
              onError={handleGoogleError}
              size="large"
              text="continue_with"
              shape="rectangular"
              theme="outline"
            />
          </div>

          {isLoading && (
            <p className="text-center text-sm text-muted-foreground">
              Logging in...
            </p>
          )}

          {error && (
            <div className="p-3 text-sm text-red-600 dark:text-red-400 bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-800 rounded-md">
              {error}
            </div>
          )}

          {import.meta.env.DEV && (
            <div className="space-y-2">
              <div className="relative">
                <div className="absolute inset-0 flex items-center">
                  <span className="w-full border-t" />
                </div>
                <div className="relative flex justify-center text-xs uppercase">
                  <span className="bg-background px-2 text-muted-foreground">
                    Development Only
                  </span>
                </div>
              </div>
              <Button
                onClick={handleDevLogin}
                disabled={isLoading}
                variant="outline"
                className="w-full"
              >
                Login as Admin (Dev)
              </Button>
            </div>
          )}

          <div className="text-center text-sm text-muted-foreground">
            <p>Pick one Premier League team per week</p>
            <p>Each team can only be picked once per half-season</p>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
