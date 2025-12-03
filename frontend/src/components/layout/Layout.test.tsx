import { render, screen } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import { describe, it, expect, vi } from 'vitest';
import { Layout } from './Layout';
import { AuthContext } from '@/contexts/AuthContext';
import { ThemeProvider } from '@/contexts/ThemeContext';

// Mock the hooks
vi.mock('@/hooks/useResultsUpdates', () => ({
  useResultsUpdates: () => {}
}));

vi.mock('@/hooks/useAutoPickNotifications', () => ({
  useAutoPickNotifications: () => {}
}));

vi.mock('@/hooks/useSeasonCreatedNotification', () => ({
  useSeasonCreatedNotification: () => {}
}));

const mockAuthContextValue = {
  user: {
    id: '123',
    firstName: 'John',
    lastName: 'Doe',
    email: 'john@example.com',
    photoUrl: 'https://example.com/photo.jpg',
    isAdmin: false,
    isActive: true,
    isPaid: true
  },
  isAuthenticated: true,
  isAdmin: false,
  login: vi.fn(),
  logout: vi.fn(),
  updateUser: vi.fn()
};

const renderWithProviders = (children: React.ReactNode) => {
  return render(
    <BrowserRouter>
      <AuthContext.Provider value={mockAuthContextValue}>
        <ThemeProvider>
          {children}
        </ThemeProvider>
      </AuthContext.Provider>
    </BrowserRouter>
  );
};

describe('Layout Accessibility', () => {
  it('should have a skip navigation link', () => {
    renderWithProviders(<Layout><div>Content</div></Layout>);

    const skipLink = screen.getByText('Skip to main content');
    expect(skipLink).toBeInTheDocument();
    expect(skipLink).toHaveAttribute('href', '#main-content');
  });

  it('should have a header with role="banner"', () => {
    renderWithProviders(<Layout><div>Content</div></Layout>);

    const header = screen.getByRole('banner');
    expect(header).toBeInTheDocument();
  });

  it('should have main content with role="main" and id="main-content"', () => {
    renderWithProviders(<Layout><div>Test Content</div></Layout>);

    const main = screen.getByRole('main');
    expect(main).toBeInTheDocument();
    expect(main).toHaveAttribute('id', 'main-content');
    expect(screen.getByText('Test Content')).toBeInTheDocument();
  });

  it('should have theme toggle button with aria-label', () => {
    renderWithProviders(<Layout><div>Content</div></Layout>);

    const themeButton = screen.getByLabelText('Switch to dark mode');
    expect(themeButton).toBeInTheDocument();
    expect(themeButton).toHaveAttribute('title', 'Switch to dark mode');
  });

  it('should have logout button with aria-label', () => {
    renderWithProviders(<Layout><div>Content</div></Layout>);

    const logoutButton = screen.getByLabelText('Logout');
    expect(logoutButton).toBeInTheDocument();
  });

  it('should have home link with aria-label', () => {
    renderWithProviders(<Layout><div>Content</div></Layout>);

    const homeLink = screen.getByLabelText('Home');
    expect(homeLink).toBeInTheDocument();
    expect(homeLink).toHaveAttribute('href', '/');
  });

  it('should have user profile image with descriptive alt text', () => {
    renderWithProviders(<Layout><div>Content</div></Layout>);

    const profileImage = screen.getByAltText("John Doe's profile picture");
    expect(profileImage).toBeInTheDocument();
    expect(profileImage).toHaveAttribute('src', 'https://example.com/photo.jpg');
  });

  it('should have decorative SVG icons marked with aria-hidden', () => {
    const { container } = renderWithProviders(<Layout><div>Content</div></Layout>);

    const svgs = container.querySelectorAll('svg[aria-hidden="true"]');
    expect(svgs.length).toBeGreaterThan(0);
  });

  it('should show admin navigation when user is admin', () => {
    const adminContextValue = {
      ...mockAuthContextValue,
      user: { ...mockAuthContextValue.user, isAdmin: true },
      isAdmin: true
    };

    render(
      <BrowserRouter>
        <AuthContext.Provider value={adminContextValue}>
          <ThemeProvider>
            <Layout><div>Content</div></Layout>
          </ThemeProvider>
        </AuthContext.Provider>
      </BrowserRouter>
    );

    const nav = screen.getByRole('navigation', { name: 'Main navigation' });
    expect(nav).toBeInTheDocument();
    expect(screen.getByText('Admin')).toBeInTheDocument();
  });
});
