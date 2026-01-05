import { QueryClient } from '@tanstack/react-query';
import { toast } from '@/hooks/use-toast';

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 1000 * 60 * 5, // 5 minutes
      refetchOnWindowFocus: false,
      retry: 1,
    },
    mutations: {
      onError: (error: unknown) => {
        const err = error as {
          response?: { data?: { message?: string; title?: string }; status?: number };
          message?: string;
        };
        const message =
          err?.response?.data?.message ||
          err?.response?.data?.title ||
          err?.message ||
          'An unexpected error occurred';

        // Don't show toast for 409 Conflict errors (duplicates are often expected)
        if (err?.response?.status !== 409) {
          toast({
            title: 'Operation Failed',
            description: message,
            variant: 'destructive',
          });
        }

        console.error('Mutation error:', error);
      },
    },
  },
});
