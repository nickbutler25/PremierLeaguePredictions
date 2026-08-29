import { render, screen, within } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import { describe, it, expect, vi } from 'vitest';
import { Layout } from './Layout';
import { AuthContext } from '@/contexts/AuthContext';
import { ThemeProvider } from '@/contexts/ThemeContext';

// Mock the hooks
vi.mock('@/hooks/useResultsUpdates', () => ({
  useResultsUpdates: () => {},
}));

vi.mock('@/hooks/useAutoPickNotifications', () => ({
  useAutoPickNotifications: () => {},
}));

vi.mock('@/hooks/useSeasonCreatedNotification', () => ({
  useSeasonCreatedNotification: () => {},
}));

const mockAuthContextValue = {
  user: {
    id: '123',
    firstName: 'John',
    lastName: 'Doe',
    email: 'john@example.com',
    photoUrl: 'https://example.com/photo.jpg',
    isAdmin: false,
  },
  token: 'mock-token',
  isAuthenticated: true,
  isAdmin: false,
  isLoading: false,
  login: vi.fn(),
  logout: vi.fn(),
  updateUser: vi.fn(),
};

const renderWithProviders = (children: React.ReactNode) => {
  return render(
    <BrowserRouter>
      <AuthContext.Provider value={mockAuthContextValue}>
        <ThemeProvider>{children}</ThemeProvider>
      </AuthContext.Provider>
    </BrowserRouter>
  );
};

describe('Layout Accessibility', () => {
  it('should have a skip navigation link', () => {
    renderWithProviders(
      <Layout>
        <div>Content</div>
      </Layout>
    );

    const skipLink = screen.getByText('Skip to main content');
    expect(skipLink).toBeInTheDocument();
    expect(skipLink).toHaveAttribute('href', '#main-content');
  });

  it('should have a header with role="banner"', () => {
    renderWithProviders(
      <Layout>
        <div>Content</div>
      </Layout>
    );

    const header = screen.getByRole('banner');
    expect(header).toBeInTheDocument();
  });

  it('should have main content with role="main" and id="main-content"', () => {
    renderWithProviders(
      <Layout>
        <div>Test Content</div>
      </Layout>
    );

    const main = screen.getByRole('main');
    expect(main).toBeInTheDocument();
    expect(main).toHaveAttribute('id', 'main-content');
    expect(screen.getByText('Test Content')).toBeInTheDocument();
  });

  it('should have theme toggle button with aria-label', () => {
    renderWithProviders(
      <Layout>
        <div>Content</div>
      </Layout>
    );

    const themeButton = screen.getByLabelText('Switch to dark mode');
    expect(themeButton).toBeInTheDocument();
    expect(themeButton).toHaveAttribute('title', 'Switch to dark mode');
  });

  it('should have logout button with aria-label', () => {
    renderWithProviders(
      <Layout>
        <div>Content</div>
      </Layout>
    );

    const logoutButton = screen.getByLabelText('Logout');
    expect(logoutButton).toBeInTheDocument();
  });

  it('should have home link with aria-label', () => {
    renderWithProviders(
      <Layout>
        <div>Content</div>
      </Layout>
    );

    const homeLink = screen.getByLabelText('Home');
    expect(homeLink).toBeInTheDocument();
    expect(homeLink).toHaveAttribute('href', '/dashboard');
  });

  it('should have a user profile avatar with a descriptive accessible name', () => {
    renderWithProviders(
      <Layout>
        <div>Content</div>
      </Layout>
    );

    // The header avatar shows the user's initials (not their photo), exposed via
    // role="img" + aria-label so it still carries a descriptive accessible name.
    const profileAvatar = screen.getByRole('img', { name: "John Doe's profile picture" });
    expect(profileAvatar).toBeInTheDocument();
  });

  it('should have decorative SVG icons marked with aria-hidden', () => {
    const { container } = renderWithProviders(
      <Layout>
        <div>Content</div>
      </Layout>
    );

    const svgs = container.querySelectorAll('svg[aria-hidden="true"]');
    expect(svgs.length).toBeGreaterThan(0);
  });

  it('should show admin navigation when user is admin', () => {
    const adminContextValue = {
      ...mockAuthContextValue,
      user: { ...mockAuthContextValue.user, isAdmin: true },
      isAdmin: true,
    };

    render(
      <BrowserRouter>
        <AuthContext.Provider value={adminContextValue}>
          <ThemeProvider>
            <Layout>
              <div>Content</div>
            </Layout>
          </ThemeProvider>
        </AuthContext.Provider>
      </BrowserRouter>
    );

    const nav = screen.getByRole('navigation', { name: 'Main navigation' });
    expect(nav).toBeInTheDocument();
    // Scoped to the header: the bottom tab bar carries an Admin tab of its own, so a bare
    // query matches twice.
    expect(within(nav).getByText('Admin')).toBeInTheDocument();
  });

  describe('bottom tab bar', () => {
    it('gives a phone a way to reach every section', () => {
      render(
        <BrowserRouter>
          <AuthContext.Provider value={mockAuthContextValue}>
            <ThemeProvider>
              <Layout>
                <div>Content</div>
              </Layout>
            </ThemeProvider>
          </AuthContext.Provider>
        </BrowserRouter>
      );

      // The header nav is hidden below md, so without these the league, the gameweek and the
      // eliminations cannot be reached on a phone at all.
      const bar = screen.getByTestId('mobile-navigation');
      expect(within(bar).getByTestId('mobile-dashboard-link')).toHaveAttribute(
        'href',
        '/dashboard'
      );
      expect(within(bar).getByTestId('mobile-gameweek-link')).toHaveAttribute('href', '/gameweek');
      expect(within(bar).getByTestId('mobile-league-link')).toHaveAttribute('href', '/league');
      expect(within(bar).getByTestId('mobile-eliminations-link')).toHaveAttribute(
        'href',
        '/eliminations'
      );
    });

    it('keeps admin out of it for an ordinary player', () => {
      render(
        <BrowserRouter>
          <AuthContext.Provider value={mockAuthContextValue}>
            <ThemeProvider>
              <Layout>
                <div>Content</div>
              </Layout>
            </ThemeProvider>
          </AuthContext.Provider>
        </BrowserRouter>
      );

      expect(screen.queryByTestId('mobile-admin-link')).not.toBeInTheDocument();
    });

    it('is named apart from the header nav', () => {
      render(
        <BrowserRouter>
          <AuthContext.Provider value={mockAuthContextValue}>
            <ThemeProvider>
              <Layout>
                <div>Content</div>
              </Layout>
            </ThemeProvider>
          </AuthContext.Provider>
        </BrowserRouter>
      );

      // Both are in the DOM at once — only CSS hides one — so a screen reader needs to be able
      // to tell the two landmarks apart.
      expect(screen.getByRole('navigation', { name: 'Main navigation' })).toBeInTheDocument();
      expect(screen.getByRole('navigation', { name: 'Mobile navigation' })).toBeInTheDocument();
    });
  });
});
