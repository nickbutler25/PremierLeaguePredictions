import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import { test, expect, describe } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { server } from './server';
import { http, HttpResponse } from 'msw';
import axios from 'axios';

/**
 * Example tests demonstrating MSW usage
 *
 * These tests show how MSW automatically intercepts API calls
 * No manual mocking needed!
 */

// Simple component that fetches data
function UserProfile({ userId }: { userId: string }) {
  const [user, setUser] = React.useState<{ name: string; email: string } | null>(null);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);

  React.useEffect(() => {
    axios
      .get(`/api/v1/auth/me`)
      .then((res) => {
        setUser(res.data.data);
        setLoading(false);
      })
      .catch((err) => {
        setError(err.message);
        setLoading(false);
      });
  }, [userId]);

  if (loading) return <div>Loading...</div>;
  if (error) return <div>Error: {error}</div>;
  if (!user) return <div>No user found</div>;

  return (
    <div>
      <h1>{user.name}</h1>
      <p>{user.email}</p>
    </div>
  );
}

describe('MSW Example Tests', () => {
  test('automatically mocks API calls', async () => {
    // MSW is already set up in test/setup.ts
    // The API call will be intercepted automatically

    render(<UserProfile userId="user-123" />);

    // Wait for the mocked data to load
    await waitFor(() => {
      expect(screen.getByText('Test User')).toBeInTheDocument();
      expect(screen.getByText('test@example.com')).toBeInTheDocument();
    });
  });

  test('can override handlers per test', async () => {
    // Override the default handler for this specific test
    server.use(
      http.get('/api/v1/auth/me', () => {
        return HttpResponse.json({
          success: true,
          data: {
            id: 'admin-123',
            email: 'admin@example.com',
            name: 'Admin User',
            isAdmin: true,
          },
        });
      })
    );

    render(<UserProfile userId="admin-123" />);

    await waitFor(() => {
      expect(screen.getByText('Admin User')).toBeInTheDocument();
      expect(screen.getByText('admin@example.com')).toBeInTheDocument();
    });
  });

  test('can simulate errors', async () => {
    // Simulate a network error
    server.use(
      http.get('/api/v1/auth/me', () => {
        return HttpResponse.json({ success: false, message: 'User not found' }, { status: 404 });
      })
    );

    render(<UserProfile userId="invalid-user" />);

    await waitFor(() => {
      expect(screen.getByText(/Error/)).toBeInTheDocument();
    });
  });

  test('works with React Query', async () => {
    const queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false },
      },
    });

    function TeamsList() {
      const { data, isLoading } = useQuery({
        queryKey: ['teams'],
        queryFn: async () => {
          const res = await axios.get('/api/v1/teams');
          return res.data.data;
        },
      });

      if (isLoading) return <div>Loading teams...</div>;

      return (
        <ul>
          {data.map((team: { id: number; name: string }) => (
            <li key={team.id}>{team.name}</li>
          ))}
        </ul>
      );
    }

    render(
      <QueryClientProvider client={queryClient}>
        <TeamsList />
      </QueryClientProvider>
    );

    // MSW will intercept the React Query request
    await waitFor(() => {
      expect(screen.getByText('Arsenal')).toBeInTheDocument();
      expect(screen.getByText('Liverpool')).toBeInTheDocument();
    });
  });
});

// Import React and useQuery
import React from 'react';
import { useQuery } from '@tanstack/react-query';
