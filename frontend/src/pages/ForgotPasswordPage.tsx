import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { authService } from '@/services/auth';
import { useState } from 'react';
import { Link } from 'react-router-dom';

/**
 * Asks for a reset link.
 *
 * The confirmation is deliberately the same whatever the address turns out to be — a known
 * account, an unknown one, or one that signs in with Google. The API answers identically for all
 * three so nobody can use this page to ask who is in the league, and saying anything more
 * specific here would give away what the API declines to.
 */
export function ForgotPasswordPage() {
  const [email, setEmail] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sentTo, setSentTo] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setIsLoading(true);

    try {
      await authService.forgotPassword(email);
      setSentTo(email);
    } catch {
      // Only a genuine failure lands here — an unrecognised address still succeeds.
      setError('Something went wrong. Please try again in a moment.');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div
      className="min-h-screen flex items-center justify-center bg-gradient-to-br from-[#E8F2FF] to-[#F4F8FC] dark:from-[#0B0820] dark:to-[#150F35] p-4"
      data-testid="forgot-password-page"
    >
      <Card className="w-full max-w-md">
        <CardHeader className="space-y-1 text-center">
          <CardTitle className="text-2xl font-bold">Forgotten password</CardTitle>
          <CardDescription>
            {sentTo ? 'Check your inbox' : "We'll email you a link to set a new one"}
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {sentTo ? (
            <div className="space-y-4" data-testid="forgot-password-sent">
              <p className="text-sm text-muted-foreground">
                If an account exists for <strong className="text-foreground">{sentTo}</strong>,
                we've sent a link to reset the password. It works once and expires in an hour.
              </p>
              <p className="text-sm text-muted-foreground">
                Nothing arrived? Check your spam folder, or{' '}
                <button
                  type="button"
                  onClick={() => setSentTo(null)}
                  className="text-primary underline-offset-4 hover:underline"
                >
                  try a different address
                </button>
                .
              </p>
              <Button asChild variant="outline" className="w-full">
                <Link to="/login">Back to sign in</Link>
              </Button>
            </div>
          ) : (
            <form onSubmit={handleSubmit} className="space-y-3">
              <div className="space-y-1">
                <Label htmlFor="email">Email</Label>
                <Input
                  id="email"
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  required
                  autoComplete="email"
                  autoFocus
                />
              </div>

              {error && (
                <p className="text-sm text-destructive" role="alert">
                  {error}
                </p>
              )}

              <Button type="submit" className="w-full" disabled={isLoading}>
                {isLoading ? 'Sending...' : 'Send reset link'}
              </Button>

              <p className="text-center text-sm text-muted-foreground">
                <Link to="/login" className="text-primary underline-offset-4 hover:underline">
                  Back to sign in
                </Link>
              </p>
            </form>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
