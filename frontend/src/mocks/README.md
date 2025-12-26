# MSW (Mock Service Worker) Setup

Mock API responses for testing and development using [MSW](https://mswjs.io/).

## What is MSW?

MSW intercepts network requests at the **network level** and returns mock responses. This means:

- ✅ Works with any HTTP client (fetch, axios, etc.)
- ✅ No need to mock individual services
- ✅ Same mocks work in tests and browser
- ✅ Realistic network behavior

---

## Directory Structure

```
src/mocks/
├── handlers/
│   ├── auth.handlers.ts       # Auth API mocks
│   ├── dashboard.handlers.ts  # Dashboard API mocks
│   ├── picks.handlers.ts      # Picks API mocks
│   ├── teams.handlers.ts      # Teams API mocks
│   ├── league.handlers.ts     # League standings mocks
│   ├── fixtures.handlers.ts   # Fixtures data mocks
│   ├── gameweeks.handlers.ts  # Gameweeks info mocks
│   ├── admin.handlers.ts      # Admin endpoints mocks
│   └── index.ts              # Combined handlers
├── server.ts                  # Node server (for tests)
├── browser.ts                 # Browser worker (for dev)
└── README.md                  # This file
```

---

## Usage in Tests

MSW is **automatically enabled** in all Vitest tests via `src/test/setup.ts`.

### Example Test

```typescript
import { render, screen, waitFor } from '@testing-library/react';
import { test, expect } from 'vitest';
import { DashboardPage } from './DashboardPage';

test('displays user dashboard', async () => {
  render(<DashboardPage />);

  // MSW will intercept the API call and return mock data
  await waitFor(() => {
    expect(screen.getByText('Total Points: 42')).toBeInTheDocument();
  });
});
```

That's it! No manual mocking needed.

---

## Customizing Responses Per Test

### Override a Handler

```typescript
import { test, expect } from 'vitest';
import { server } from '@/mocks/server';
import { http, HttpResponse } from 'msw';

test('handles API error', async () => {
  // Override the default handler for this test only
  server.use(
    http.get('/api/v1/dashboard/:userId', () => {
      return HttpResponse.json(
        { success: false, message: 'Server error' },
        { status: 500 }
      );
    })
  );

  render(<DashboardPage />);

  await waitFor(() => {
    expect(screen.getByText('Failed to load dashboard')).toBeInTheDocument();
  });
});
```

### Mock Specific Scenarios

```typescript
test('handles no picks', async () => {
  server.use(
    http.get('/api/v1/users/:userId/picks', () => {
      return HttpResponse.json({
        success: true,
        data: [], // Empty picks
      });
    })
  );

  render(<PicksComponent />);

  await waitFor(() => {
    expect(screen.getByText('No picks yet')).toBeInTheDocument();
  });
});
```

---

## Usage in Browser (Development)

You can use MSW in the browser to develop without a backend.

### 1. Initialize Service Worker

```bash
cd frontend
npx msw init public/ --save
```

This creates `public/mockServiceWorker.js`

### 2. Enable in Development

Update `src/main.tsx`:

```typescript
import { worker } from './mocks/browser';

// Enable MSW in development
if (import.meta.env.DEV && import.meta.env.VITE_ENABLE_MSW === 'true') {
  worker.start({
    onUnhandledRequest: 'bypass', // Don't warn for real API calls
  });
}

// Rest of your app...
```

### 3. Start with MSW

```bash
# Add to .env.local
VITE_ENABLE_MSW=true

# Start dev server
npm run dev
```

Now all API calls will use mocks!

---

## Creating New Handlers

### 1. Create Handler File

`src/mocks/handlers/league.handlers.ts`:

```typescript
import { http, HttpResponse } from 'msw';

const API_BASE = '/api/v1';

export const leagueHandlers = [
  http.get(`${API_BASE}/league/standings`, async () => {
    return HttpResponse.json({
      success: true,
      data: {
        standings: [
          {
            userId: 'user-1',
            name: 'Test User',
            points: 42,
            position: 1,
          },
        ],
      },
    });
  }),
];
```

### 2. Add to Index

`src/mocks/handlers/index.ts`:

```typescript
import { leagueHandlers } from './league.handlers';

export const handlers = [
  ...authHandlers,
  ...dashboardHandlers,
  ...picksHandlers,
  ...teamsHandlers,
  ...leagueHandlers, // Add new handlers
];
```

---

## Handler Patterns

### GET Request

```typescript
http.get('/api/v1/teams', () => {
  return HttpResponse.json({
    success: true,
    data: [{ id: 1, name: 'Arsenal' }],
  });
});
```

### GET with Path Parameters

```typescript
http.get('/api/v1/teams/:teamId', ({ params }) => {
  const { teamId } = params;

  return HttpResponse.json({
    success: true,
    data: { id: teamId, name: 'Arsenal' },
  });
});
```

### POST with Request Body

```typescript
http.post('/api/v1/picks', async ({ request }) => {
  const body = await request.json();

  return HttpResponse.json(
    {
      success: true,
      data: {
        id: 'new-pick',
        ...body,
      },
    },
    { status: 201 }
  );
});
```

### DELETE Request

```typescript
http.delete('/api/v1/picks/:pickId', () => {
  return HttpResponse.json({
    success: true,
    data: {},
  });
});
```

### Error Response

```typescript
http.get('/api/v1/dashboard/:userId', () => {
  return HttpResponse.json(
    {
      success: false,
      message: 'User not found',
    },
    { status: 404 }
  );
});
```

### Network Error

```typescript
http.get('/api/v1/dashboard/:userId', () => {
  return HttpResponse.error(); // Simulates network failure
});
```

### Delayed Response

```typescript
import { delay, http, HttpResponse } from 'msw';

http.get('/api/v1/dashboard/:userId', async () => {
  await delay(2000); // 2 second delay

  return HttpResponse.json({
    success: true,
    data: {},
  });
});
```

---

## Best Practices

### 1. Match Real API Structure

Keep mock responses identical to real API responses:

```typescript
// ✅ Good: Matches real API
{
  success: true,
  data: { id: 1, name: 'Arsenal' }
}

// ❌ Avoid: Different structure
{
  id: 1,
  name: 'Arsenal'
}
```

### 2. Use Realistic Data

```typescript
// ✅ Good: Realistic test data
{
  email: 'test@example.com',
  name: 'Test User',
  createdAt: '2024-01-15T10:30:00Z'
}

// ❌ Avoid: Fake/silly data
{
  email: 'foo@bar.com',
  name: 'Foo Bar',
  createdAt: 'yesterday'
}
```

### 3. Organize by Feature

Group handlers by API domain:

- `auth.handlers.ts` - Authentication
- `picks.handlers.ts` - Picks
- `teams.handlers.ts` - Teams
- etc.

### 4. Test Both Success and Failure

```typescript
// Success case
test('loads dashboard successfully', ...);

// Error case
test('handles dashboard load failure', () => {
  server.use(
    http.get('/api/v1/dashboard/:userId', () => {
      return HttpResponse.json(
        { success: false, message: 'Error' },
        { status: 500 }
      );
    })
  );
  // Test error handling...
});
```

---

## Debugging

### See Matched Requests

```typescript
import { server } from '@/mocks/server';

beforeAll(() => {
  server.listen({
    onUnhandledRequest: 'warn', // Log unhandled requests
  });

  // Log all requests
  server.events.on('request:start', ({ request }) => {
    console.log('MSW intercepted:', request.method, request.url);
  });
});
```

### Check Handler Order

Handlers are matched in order. First match wins:

```typescript
export const handlers = [
  // ✅ Specific handler first
  http.get('/api/v1/teams/1', ...),

  // ✅ Generic handler last
  http.get('/api/v1/teams/:id', ...),
];
```

---

## Testing Scenarios

### Test Loading States

```typescript
server.use(
  http.get('/api/v1/dashboard/:userId', async () => {
    await delay(1000); // Slow response
    return HttpResponse.json({ success: true, data: {} });
  })
);
```

### Test Empty States

```typescript
server.use(
  http.get('/api/v1/picks', () => {
    return HttpResponse.json({
      success: true,
      data: [], // No picks
    });
  })
);
```

### Test Authentication Errors

```typescript
server.use(
  http.get('/api/v1/dashboard/:userId', () => {
    return HttpResponse.json({ success: false, message: 'Unauthorized' }, { status: 401 });
  })
);
```

### Test Validation Errors

```typescript
server.use(
  http.post('/api/v1/picks', () => {
    return HttpResponse.json(
      {
        success: false,
        message: 'Validation failed',
        errors: ['Team already picked this gameweek'],
      },
      { status: 400 }
    );
  })
);
```

---

## Troubleshooting

### Handler Not Matching

**Problem:** Request goes to real API instead of mock

**Solutions:**

1. Check URL path matches exactly
2. Check HTTP method matches
3. Verify server is started in tests
4. Check handler order (specific before generic)

### Type Errors

**Problem:** TypeScript errors with request/response types

**Solution:** Use `any` for quick prototyping, add proper types later:

```typescript
http.post('/api/v1/picks', async ({ request }) => {
  const body = (await request.json()) as any;
  // ...
});
```

### Real API Calls Leaking Through

**Problem:** Some requests bypass MSW

**Solution:** Add catch-all handler during development:

```typescript
http.all('*', ({ request }) => {
  console.warn(`Unhandled ${request.method} ${request.url}`);
  return HttpResponse.error();
});
```

---

## Resources

- [MSW Documentation](https://mswjs.io/)
- [MSW Examples](https://github.com/mswjs/examples)
- [MSW Best Practices](https://kentcdodds.com/blog/stop-mocking-fetch)
