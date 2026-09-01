import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ForgotPasswordPage } from './ForgotPasswordPage';
import { ResetPasswordPage } from './ResetPasswordPage';
import { ProfilePage } from './ProfilePage';
import { render, createMockUser } from '@/test/test-utils';
import { authService } from '@/services/auth';

vi.mock('@/services/auth', () => ({
  authService: {
    forgotPassword: vi.fn(),
    resetPassword: vi.fn(),
    changePassword: vi.fn(),
  },
}));

vi.mock('@/services/users', () => ({
  usersService: { updateProfile: vi.fn(), uploadPhoto: vi.fn() },
}));

const navigate = vi.fn();
let search = '';

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual<typeof import('react-router-dom')>('react-router-dom');
  return {
    ...actual,
    useNavigate: () => navigate,
    useSearchParams: () => [new URLSearchParams(search), vi.fn()],
  };
});

describe('ForgotPasswordPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(authService.forgotPassword).mockResolvedValue('sent');
  });

  it('confirms without saying whether the address is known', async () => {
    const user = userEvent.setup();
    render(<ForgotPasswordPage />);

    await user.type(screen.getByLabelText('Email'), 'nobody@example.com');
    await user.click(screen.getByRole('button', { name: 'Send reset link' }));

    // The wording must not vary with whether an account exists — that is the whole point of the
    // API answering identically, and a page that said "no such account" would give it away.
    const confirmation = await screen.findByTestId('forgot-password-sent');
    expect(confirmation).toHaveTextContent('If an account exists');
    expect(confirmation).toHaveTextContent('nobody@example.com');
  });

  it('reports a genuine failure rather than claiming the mail went', async () => {
    vi.mocked(authService.forgotPassword).mockRejectedValue(new Error('boom'));
    const user = userEvent.setup();
    render(<ForgotPasswordPage />);

    await user.type(screen.getByLabelText('Email'), 'player@example.com');
    await user.click(screen.getByRole('button', { name: 'Send reset link' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Something went wrong');
    expect(screen.queryByTestId('forgot-password-sent')).not.toBeInTheDocument();
  });
});

describe('ResetPasswordPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    search = 'token=a-valid-token';
  });

  it('sets the password and lands on the dashboard signed in', async () => {
    vi.mocked(authService.resetPassword).mockResolvedValue({
      token: 'jwt',
      user: createMockUser(),
    });

    const user = userEvent.setup();
    render(<ResetPasswordPage />);

    await user.type(screen.getByLabelText('New password'), 'brandnewpassword');
    await user.type(screen.getByLabelText('Confirm new password'), 'brandnewpassword');
    await user.click(screen.getByRole('button', { name: 'Save password' }));

    await waitFor(() =>
      expect(authService.resetPassword).toHaveBeenCalledWith(
        'a-valid-token',
        'brandnewpassword',
        'brandnewpassword'
      )
    );

    // No second login step: they have proved they own the inbox and just chosen the password.
    expect(navigate).toHaveBeenCalledWith('/dashboard', { replace: true });
  });

  it('catches a mismatch before calling the API', async () => {
    const user = userEvent.setup();
    render(<ResetPasswordPage />);

    await user.type(screen.getByLabelText('New password'), 'brandnewpassword');
    await user.type(screen.getByLabelText('Confirm new password'), 'somethingelse');
    await user.click(screen.getByRole('button', { name: 'Save password' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Passwords do not match');
    expect(authService.resetPassword).not.toHaveBeenCalled();
  });

  it('offers a fresh link when the token is no good', async () => {
    vi.mocked(authService.resetPassword).mockRejectedValue(
      new Error('This reset link is no longer valid. Please request a new one.')
    );

    const user = userEvent.setup();
    render(<ResetPasswordPage />);

    await user.type(screen.getByLabelText('New password'), 'brandnewpassword');
    await user.type(screen.getByLabelText('Confirm new password'), 'brandnewpassword');
    await user.click(screen.getByRole('button', { name: 'Save password' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('no longer valid');
    expect(screen.getByRole('link', { name: 'Request a new link' })).toBeInTheDocument();
    expect(navigate).not.toHaveBeenCalled();
  });

  it('asks for a link when reached without a token', async () => {
    search = '';
    render(<ResetPasswordPage />);

    // Somebody has opened the page directly rather than following an email.
    expect(screen.getByTestId('reset-password-no-token')).toBeInTheDocument();
    expect(screen.queryByLabelText('New password')).not.toBeInTheDocument();
  });
});

describe('ProfilePage password section', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    search = '';
  });

  it('is offered to an account that has a password', () => {
    render(<ProfilePage />, {
      user: createMockUser({ hasPassword: true }),
      token: 'jwt',
    });

    expect(screen.getByTestId('change-password-card')).toBeInTheDocument();
  });

  it('is left out entirely for a Google-only account', () => {
    render(<ProfilePage />, {
      user: createMockUser({ hasPassword: false }),
      token: 'jwt',
    });

    // There is no current password to check a change against, so the form would have nothing to
    // do. A disabled control would read as broken.
    expect(screen.queryByTestId('change-password-card')).not.toBeInTheDocument();
  });

  it('clears the fields once the password is changed', async () => {
    vi.mocked(authService.changePassword).mockResolvedValue(createMockUser({ hasPassword: true }));

    const user = userEvent.setup();
    render(<ProfilePage />, {
      user: createMockUser({ hasPassword: true }),
      token: 'jwt',
    });

    await user.type(screen.getByLabelText('Current password'), 'oldpassword');
    await user.type(screen.getByLabelText('New password'), 'brandnewpassword');
    await user.type(screen.getByLabelText('Confirm new password'), 'brandnewpassword');
    await user.click(screen.getByTestId('change-password-button'));

    await waitFor(() => expect(authService.changePassword).toHaveBeenCalled());

    // The new password must not be left sitting in the form afterwards.
    await waitFor(() =>
      expect(screen.getByLabelText('New password')).toHaveValue('')
    );
    expect(screen.getByLabelText('Current password')).toHaveValue('');
  });

  it('surfaces a wrong current password', async () => {
    vi.mocked(authService.changePassword).mockRejectedValue(
      new Error('Your current password is incorrect')
    );

    const user = userEvent.setup();
    render(<ProfilePage />, {
      user: createMockUser({ hasPassword: true }),
      token: 'jwt',
    });

    await user.type(screen.getByLabelText('Current password'), 'wrong');
    await user.type(screen.getByLabelText('New password'), 'brandnewpassword');
    await user.type(screen.getByLabelText('Confirm new password'), 'brandnewpassword');
    await user.click(screen.getByTestId('change-password-button'));

    expect(await screen.findByText('Your current password is incorrect')).toBeInTheDocument();
  });
});
