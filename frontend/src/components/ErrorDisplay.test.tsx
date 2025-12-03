import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { ErrorDisplay, InlineError } from './ErrorDisplay';

describe('ErrorDisplay Accessibility', () => {
  it('should have role="alert" and aria-live="assertive"', () => {
    render(<ErrorDisplay title="Test Error" message="Error message" />);

    const errorCard = screen.getByRole('alert');
    expect(errorCard).toBeInTheDocument();
    expect(errorCard).toHaveAttribute('aria-live', 'assertive');
  });

  it('should display error title and message', () => {
    render(<ErrorDisplay title="Test Error" message="Something went wrong" />);

    expect(screen.getByText('Test Error')).toBeInTheDocument();
    expect(screen.getByText('Something went wrong')).toBeInTheDocument();
  });

  it('should mark decorative icons with aria-hidden', () => {
    const { container } = render(<ErrorDisplay title="Test Error" message="Error message" />);

    const icons = container.querySelectorAll('svg[aria-hidden="true"]');
    expect(icons.length).toBeGreaterThan(0);
  });

  it('should have retry button with aria-label when onRetry is provided', () => {
    const mockRetry = vi.fn();
    render(<ErrorDisplay title="Test Error" message="Error message" onRetry={mockRetry} />);

    const retryButton = screen.getByLabelText('Retry loading');
    expect(retryButton).toBeInTheDocument();
    expect(retryButton).toHaveTextContent('Retry');
  });

  it('should have technical details with aria-label when shown', () => {
    const error = new Error('Detailed error message');
    render(<ErrorDisplay title="Test Error" error={error} showDetails={true} />);

    const details = screen.getByText('Technical Details');
    expect(details).toBeInTheDocument();

    const pre = screen.getByLabelText('Error details');
    expect(pre).toBeInTheDocument();
  });
});

describe('InlineError Accessibility', () => {
  it('should have role="alert" and aria-live="polite"', () => {
    const { container } = render(<InlineError message="Inline error message" />);

    const alert = container.querySelector('[role="alert"]');
    expect(alert).toBeInTheDocument();
    expect(alert).toHaveAttribute('aria-live', 'polite');
  });

  it('should display error message', () => {
    render(<InlineError message="Field is required" />);

    expect(screen.getByText('Field is required')).toBeInTheDocument();
  });

  it('should mark decorative icon with aria-hidden', () => {
    const { container } = render(<InlineError message="Error message" />);

    const icon = container.querySelector('svg[aria-hidden="true"]');
    expect(icon).toBeInTheDocument();
  });

  it('should handle Error objects', () => {
    const error = new Error('Something failed');
    render(<InlineError error={error} />);

    expect(screen.getByText('Something failed')).toBeInTheDocument();
  });
});
