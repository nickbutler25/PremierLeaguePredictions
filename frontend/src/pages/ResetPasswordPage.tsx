import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useAuth } from '@/contexts/AuthContext';
import { authService } from '@/services/auth';
import { useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';

/**
 * Sets a new password from an emailed link, then lands the player on the dashboard.
 *
 * The API signs them in as part of the reset, so there is no second login step: they have proved
 * they own the inbox and just chosen the password.
 */
export function ResetPasswordPage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { login } = useAuth();

  const token = searchParams.get('token') ?? '';

  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    // Checked here as well as on the server so the mismatch is caught before a round trip.
    if (password !== confirmPassword) {
      setError('Passwords do not match');
      return;
    }

    setIsLoading(true);

    try {
      const authResponse = await authService.resetPassword(token, password, confirmPassword);
      login(authResponse);
      navigate('/dashboard', { replace: true });
    } catch (err) {
      // Covers every way a token can be no good — unknown, expired, already spent. The API says
      // the same thing for all of them, so there is nothing more specific to show.
      const message =
        err instanceof Error && err.message
          ? err.message
          : 'This reset link is no longer valid. Please request a new one.';
      setError(message);
      setIsLoading(false);
    }
  };

  return (
    <div
      className="min-h-screen flex items-center justify-center bg-gradient-to-br from-[#E8F2FF] to-[#F4F8FC] dark:from-[#0B0820] dark:to-[#150F35] p-4"
      data-testid="reset-password-page"
    >
      <Card className="w-full max-w-md">
        <CardHeader className="space-y-1 text-center">
          <CardTitle className="text-2xl font-bold">Set a new password</CardTitle>
          <CardDescription>Then we'll sign you straight in</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {!token ? (
            // Someone has reached the page without following a link from an email.
            <div className="space-y-4" data-testid="reset-password-no-token">
              <p className="text-sm text-muted-foreground">
                This page needs a reset link to work. Ask for one and we'll email it to you.
              </p>
              <Button asChild className="w-full">
                <Link to="/forgot-password">Request a reset link</Link>
              </Button>
            </div>
          ) : (
            <form onSubmit={handleSubmit} className="space-y-3">
              <div className="space-y-1">
                <Label htmlFor="password">New password</Label>
                <Input
                  id="password"
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  required
                  minLength={8}
                  autoComplete="new-password"
                  autoFocus
                />
                <p className="text-xs text-muted-foreground">At least 8 characters.</p>
              </div>

              <div className="space-y-1">
                <Label htmlFor="confirmPassword">Confirm new password</Label>
                <Input
                  id="confirmPassword"
                  type="password"
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                  required
                  minLength={8}
                  autoComplete="new-password"
                />
              </div>

              {error && (
                <div className="space-y-2">
                  <p className="text-sm text-destructive" role="alert">
                    {error}
                  </p>
                  <Link
                    to="/forgot-password"
                    className="text-sm text-primary underline-offset-4 hover:underline"
                  >
                    Request a new link
                  </Link>
                </div>
              )}

              <Button type="submit" className="w-full" disabled={isLoading}>
                {isLoading ? 'Saving...' : 'Save password'}
              </Button>
            </form>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
