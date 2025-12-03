import { AlertCircle, RefreshCw } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';

interface ErrorDisplayProps {
  title?: string;
  message?: string;
  error?: Error | unknown;
  onRetry?: () => void;
  showDetails?: boolean;
}

export function ErrorDisplay({
  title = 'Error',
  message = 'An error occurred while loading data',
  error,
  onRetry,
  showDetails = import.meta.env.DEV,
}: ErrorDisplayProps) {
  const getErrorMessage = (err: unknown): string => {
    if (!err) return message;

    // Handle Axios errors
    if (typeof err === 'object' && err !== null && 'response' in err) {
      const axiosError = err as any;
      return axiosError.response?.data?.message ||
             axiosError.response?.data?.title ||
             axiosError.message ||
             message;
    }

    // Handle standard errors
    if (err instanceof Error) {
      return err.message;
    }

    // Handle string errors
    if (typeof err === 'string') {
      return err;
    }

    return message;
  };

  const errorMessage = getErrorMessage(error);

  return (
    <Card className="border-destructive/50" role="alert" aria-live="assertive">
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-destructive">
          <AlertCircle className="w-5 h-5" aria-hidden="true" />
          {title}
        </CardTitle>
        <CardDescription>{errorMessage}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {showDetails && error ? (
          <details className="bg-muted rounded-lg p-3">
            <summary className="cursor-pointer text-sm font-medium mb-2">
              Technical Details
            </summary>
            <pre className="text-xs overflow-auto bg-background p-2 rounded" aria-label="Error details">
              {(() => {
                try {
                  return JSON.stringify(error, null, 2);
                } catch {
                  return String(error);
                }
              })()}
            </pre>
          </details>
        ) : null}

        {onRetry && (
          <Button onClick={onRetry} variant="default" size="sm" aria-label="Retry loading">
            <RefreshCw className="w-4 h-4 mr-2" aria-hidden="true" />
            Retry
          </Button>
        )}
      </CardContent>
    </Card>
  );
}

interface InlineErrorProps {
  message?: string;
  error?: Error | unknown;
  className?: string;
}

export function InlineError({ message, error, className = '' }: InlineErrorProps) {
  const errorMessage = error instanceof Error ? error.message : message || 'An error occurred';

  return (
    <div className={`flex items-center gap-2 text-destructive text-sm ${className}`} role="alert" aria-live="polite">
      <AlertCircle className="w-4 h-4 flex-shrink-0" aria-hidden="true" />
      <span>{errorMessage}</span>
    </div>
  );
}
