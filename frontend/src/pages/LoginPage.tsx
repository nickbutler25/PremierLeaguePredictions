import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { GoogleLogin } from '@react-oauth/google';
import type { CredentialResponse } from '@react-oauth/google';
import { useAuth } from '@/contexts/AuthContext';
import { authService } from '@/services/auth';
import { apiClient } from '@/services/api';
import { useState } from 'react';
import type { ApiResponse, AuthResponse } from '@/types';

type AuthMode = 'google' | 'email-login' | 'email-register';

export function LoginPage() {
  const { login } = useAuth();
  const [mode, setMode] = useState<AuthMode>('google');
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');

  const resetForm = () => {
    setEmail('');
    setPassword('');
    setConfirmPassword('');
    setFirstName('');
    setLastName('');
    setError(null);
  };

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
    } catch {
      setError('Failed to login. Please try again.');
    } finally {
      setIsLoading(false);
    }
  };

  const handleEmailLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    setError(null);
    try {
      const authResponse = await authService.passwordLogin(email, password);
      await login(authResponse);
    } catch (err: unknown) {
      const status = (err as { response?: { status?: number } })?.response?.status;
      setError(status === 401 ? 'Invalid email or password.' : 'Login failed. Please try again.');
    } finally {
      setIsLoading(false);
    }
  };

  const handleRegister = async (e: React.FormEvent) => {
    e.preventDefault();
    if (password !== confirmPassword) {
      setError('Passwords do not match.');
      return;
    }
    setIsLoading(true);
    setError(null);
    try {
      const authResponse = await authService.register(
        email,
        firstName,
        lastName,
        password,
        confirmPassword
      );
      await login(authResponse);
    } catch (err: unknown) {
      const message = (err as { response?: { data?: { message?: string } } })?.response?.data
        ?.message;
      setError(message ?? 'Registration failed. Please try again.');
    } finally {
      setIsLoading(false);
    }
  };

  const handleDevLogin = async () => {
    setIsLoading(true);
    setError(null);
    try {
      const response = await apiClient.post<ApiResponse<AuthResponse>>(
        '/api/v1/dev/login-as-admin'
      );
      await login(response.data.data!);
    } catch {
      setError('Failed to login as admin. Please try again.');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div
      className="min-h-screen flex items-center justify-center bg-gradient-to-br from-blue-50 to-indigo-100 dark:from-slate-900 dark:to-slate-800 p-4"
      data-testid="login-page"
    >
      <Card className="w-full max-w-md" data-testid="login-card">
        <CardHeader className="space-y-1 text-center">
          <CardTitle className="text-3xl font-bold">Premier League Predictions</CardTitle>
          <CardDescription>Sign in to join the competition</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {/* Method toggle */}
          <div className="flex rounded-lg border p-1 gap-1">
            <button
              type="button"
              onClick={() => {
                setMode('google');
                resetForm();
              }}
              className={`flex-1 rounded-md py-1.5 text-sm font-medium transition-colors ${
                mode === 'google'
                  ? 'bg-primary text-primary-foreground'
                  : 'text-muted-foreground hover:text-foreground'
              }`}
            >
              Google
            </button>
            <button
              type="button"
              onClick={() => {
                setMode('email-login');
                resetForm();
              }}
              className={`flex-1 rounded-md py-1.5 text-sm font-medium transition-colors ${
                mode !== 'google'
                  ? 'bg-primary text-primary-foreground'
                  : 'text-muted-foreground hover:text-foreground'
              }`}
            >
              Email
            </button>
          </div>

          {/* Google login */}
          {mode === 'google' && (
            <div className="flex justify-center" data-testid="google-login-container">
              <GoogleLogin
                onSuccess={handleGoogleSuccess}
                onError={() => setError('Google login failed. Please try again.')}
                size="large"
                text="continue_with"
                shape="rectangular"
                theme="outline"
              />
            </div>
          )}

          {/* Email sign in */}
          {mode === 'email-login' && (
            <form onSubmit={handleEmailLogin} className="space-y-3">
              <div className="space-y-1">
                <Label htmlFor="email">Email</Label>
                <Input
                  id="email"
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  required
                  autoComplete="email"
                />
              </div>
              <div className="space-y-1">
                <Label htmlFor="password">Password</Label>
                <Input
                  id="password"
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  required
                  autoComplete="current-password"
                />
              </div>
              <Button type="submit" className="w-full" disabled={isLoading}>
                {isLoading ? 'Signing in...' : 'Sign In'}
              </Button>
              <p className="text-center text-sm text-muted-foreground">
                No account?{' '}
                <button
                  type="button"
                  onClick={() => {
                    setMode('email-register');
                    setError(null);
                  }}
                  className="text-primary underline-offset-4 hover:underline"
                >
                  Create one
                </button>
              </p>
            </form>
          )}

          {/* Email register */}
          {mode === 'email-register' && (
            <form onSubmit={handleRegister} className="space-y-3">
              <div className="grid grid-cols-2 gap-2">
                <div className="space-y-1">
                  <Label htmlFor="firstName">First name</Label>
                  <Input
                    id="firstName"
                    value={firstName}
                    onChange={(e) => setFirstName(e.target.value)}
                    required
                    autoComplete="given-name"
                  />
                </div>
                <div className="space-y-1">
                  <Label htmlFor="lastName">Last name</Label>
                  <Input
                    id="lastName"
                    value={lastName}
                    onChange={(e) => setLastName(e.target.value)}
                    required
                    autoComplete="family-name"
                  />
                </div>
              </div>
              <div className="space-y-1">
                <Label htmlFor="reg-email">Email</Label>
                <Input
                  id="reg-email"
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  required
                  autoComplete="email"
                />
              </div>
              <div className="space-y-1">
                <Label htmlFor="reg-password">Password</Label>
                <Input
                  id="reg-password"
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  required
                  autoComplete="new-password"
                  placeholder="Min. 8 characters"
                />
              </div>
              <div className="space-y-1">
                <Label htmlFor="confirm-password">Confirm password</Label>
                <Input
                  id="confirm-password"
                  type="password"
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                  required
                  autoComplete="new-password"
                />
              </div>
              <Button type="submit" className="w-full" disabled={isLoading}>
                {isLoading ? 'Creating account...' : 'Create Account'}
              </Button>
              <p className="text-center text-sm text-muted-foreground">
                Already have an account?{' '}
                <button
                  type="button"
                  onClick={() => {
                    setMode('email-login');
                    setError(null);
                  }}
                  className="text-primary underline-offset-4 hover:underline"
                >
                  Sign in
                </button>
              </p>
            </form>
          )}

          {isLoading && mode === 'google' && (
            <p className="text-center text-sm text-muted-foreground" data-testid="login-loading">
              Logging in...
            </p>
          )}

          {error && (
            <div
              className="p-3 text-sm text-red-600 dark:text-red-400 bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-800 rounded-md"
              data-testid="login-error"
            >
              {error}
            </div>
          )}

          {(import.meta.env.DEV || import.meta.env.VITE_ENABLE_DEV_LOGIN === 'true') && (
            <div className="space-y-2">
              <div className="relative">
                <div className="absolute inset-0 flex items-center">
                  <span className="w-full border-t" />
                </div>
                <div className="relative flex justify-center text-xs uppercase">
                  <span className="bg-background px-2 text-muted-foreground">Development Only</span>
                </div>
              </div>
              <Button
                data-testid="dev-login-button"
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
