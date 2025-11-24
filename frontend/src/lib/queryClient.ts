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
      onError: (error: any) => {
        const message = error?.response?.data?.message ||
                       error?.response?.data?.title ||
                       error?.message ||
                       'An unexpected error occurred';

        // Don't show toast for 409 Conflict errors (duplicates are often expected)
        if (error?.response?.status !== 409) {
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
