import { ReactElement, ReactNode } from 'react';
import { render, RenderOptions } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter } from 'react-router-dom';
import { vi } from 'vitest';
import { AuthContext } from '@/contexts/AuthContext';
import type { User } from '@/types';

interface AuthProviderProps {
  user?: User | null;
  token?: string | null;
  children: ReactNode;
}

function TestAuthProvider({ user = null, token = null, children }: AuthProviderProps) {
  const authValue = {
    user,
    token,
    login: vi.fn(),
    logout: vi.fn(),
    isAuthenticated: !!user && !!token,
    isAdmin: user?.isAdmin ?? false,
  };

  return <AuthContext.Provider value={authValue}>{children}</AuthContext.Provider>;
}

interface WrapperProps {
  children: ReactNode;
  user?: User | null;
  token?: string | null;
}

function AllTheProviders({ children, user, token }: WrapperProps) {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
    },
  });

  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <TestAuthProvider user={user} token={token}>
          {children}
        </TestAuthProvider>
      </BrowserRouter>
    </QueryClientProvider>
  );
}

interface CustomRenderOptions extends Omit<RenderOptions, 'wrapper'> {
  user?: User | null;
  token?: string | null;
}

const customRender = (
  ui: ReactElement,
  options?: CustomRenderOptions
) => {
  const { user, token, ...renderOptions } = options || {};

  return render(ui, {
    wrapper: ({ children }) => (
      <AllTheProviders user={user} token={token}>
        {children}
      </AllTheProviders>
    ),
    ...renderOptions,
  });
};

export * from '@testing-library/react';
export { customRender as render };

// Helper to create mock users
export const createMockUser = (overrides?: Partial<User>): User => ({
  id: 'test-user-id',
  email: 'test@example.com',
  firstName: 'Test',
  lastName: 'User',
  photoUrl: 'https://example.com/photo.jpg',
  isActive: true,
  isAdmin: false,
  isPaid: true,
  ...overrides,
});
