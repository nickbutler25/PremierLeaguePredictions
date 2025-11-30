# Frontend Testing Guide

This directory contains the testing infrastructure and utilities for the Premier League Predictions frontend application.

## Testing Stack

- **Vitest**: Fast unit test framework
- **React Testing Library**: For testing React components
- **Jest DOM**: Custom matchers for DOM elements
- **jsdom**: Browser environment simulation

## Running Tests

### Run all tests
```bash
npm test
```

### Run tests in watch mode
```bash
npm test -- --watch
```

### Run tests with UI
```bash
npm run test:ui
```

### Run tests with coverage
```bash
npm run test:coverage
```

### Run specific test file
```bash
npm test DashboardPage
```

## Test Files

### `setup.ts`
Global test setup file that:
- Configures jest-dom matchers
- Sets up automatic cleanup after each test
- Mocks window.matchMedia for responsive design tests

### `test-utils.tsx`
Provides testing utilities including:
- Custom render function with all necessary providers (Auth, Router, React Query)
- Mock user creation helper
- Centralized test configuration

## Writing Tests

### Basic Component Test

```tsx
import { describe, it, expect, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { render, createMockUser } from '@/test/test-utils';
import { MyComponent } from './MyComponent';

describe('MyComponent', () => {
  it('should render correctly', () => {
    const mockUser = createMockUser();
    render(<MyComponent />, { user: mockUser, token: 'test-token' });

    expect(screen.getByText('Expected Text')).toBeInTheDocument();
  });
});
```

### Testing with Authenticated User

```tsx
it('should show user-specific content', () => {
  const mockUser = createMockUser({
    firstName: 'John',
    isAdmin: true
  });

  render(<MyComponent />, { user: mockUser, token: 'test-token' });

  expect(screen.getByText('John')).toBeInTheDocument();
});
```

### Testing with Unauthenticated User

```tsx
it('should redirect when not authenticated', () => {
  render(<MyComponent />, { user: null, token: null });

  // Component should handle unauthenticated state
  expect(screen.queryByText('Protected Content')).not.toBeInTheDocument();
});
```

### Mocking API Services

```tsx
import { vi } from 'vitest';
import { myService } from '@/services/myService';

vi.mock('@/services/myService', () => ({
  myService: {
    getData: vi.fn(),
  },
}));

it('should fetch and display data', async () => {
  vi.mocked(myService.getData).mockResolvedValue({ data: 'test' });

  render(<MyComponent />);

  await waitFor(() => {
    expect(screen.getByText('test')).toBeInTheDocument();
  });
});
```

## Test Coverage Goals

- **Component Tests**: All major page components (Dashboard, PendingApproval, etc.)
- **Route Guards**: Authentication and authorization checks
- **Service Integration**: API call handling and error states
- **User Interactions**: Button clicks, form submissions, navigation
- **Edge Cases**: No data, loading states, error states

## No Active Season Tests

The following test files specifically cover the "No Active Season" scenario:

1. **DashboardPage.test.tsx**
   - Empty dashboard when no gameweeks exist
   - Admin action message for admin users
   - Regular user view when no season
   - Loading and error states

2. **PendingApprovalPage.test.tsx**
   - No active season message
   - Auto-request participation flow
   - Approval pending UI
   - Admin/user differences
   - Payment status warnings

3. **App.test.tsx**
   - Route guard behavior
   - Approval check logic
   - Redirect flows

## Best Practices

1. **Test user behavior, not implementation**
   - Focus on what users see and do
   - Avoid testing internal state directly

2. **Use semantic queries**
   - Prefer `getByRole`, `getByLabelText`, `getByText`
   - Avoid `getByTestId` unless necessary

3. **Mock external dependencies**
   - Mock API services
   - Mock complex context providers when needed
   - Keep mocks simple and focused

4. **Test accessibility**
   - Use `getByRole` to ensure proper ARIA attributes
   - Test keyboard navigation where applicable

5. **Keep tests isolated**
   - Each test should be independent
   - Use `beforeEach` for setup
   - Don't rely on test execution order

## Debugging Tests

### Run a single test
```bash
npm test -- -t "test name"
```

### Debug in VS Code
Add breakpoints and run the "Debug Test" configuration

### View test output
```bash
npm test -- --reporter=verbose
```

### Check what's rendered
```tsx
import { screen } from '@testing-library/react';

// In your test:
screen.debug(); // Prints the DOM
```

## Common Issues

### "Unable to find element"
- Use `screen.debug()` to see what's actually rendered
- Check if you're waiting for async operations with `waitFor`
- Verify the element isn't conditionally hidden

### "Query returned more than one element"
- Make your query more specific
- Use `getAllBy*` if multiple elements are expected
- Add test IDs to disambiguate similar elements

### "Network request failed"
- Ensure the service is mocked
- Check mock return values match expected types
- Verify the mock is called with correct parameters
