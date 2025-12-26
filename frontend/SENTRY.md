# Sentry Error Tracking Setup

Sentry is configured for production error tracking, performance monitoring, and session replay.

## 🎯 What's Configured

- ✅ **Error Tracking** - Automatic error reporting
- ✅ **Performance Monitoring** - Track slow transactions
- ✅ **Session Replay** - Debug issues with video playback
- ✅ **User Context** - Track which users experience errors
- ✅ **React Error Boundary** - Catch React errors
- ✅ **Source Maps** - See original code in stack traces
- ✅ **Privacy First** - Masks sensitive data

---

## 📋 Quick Start

### 1. Create Sentry Account

1. Go to https://sentry.io/signup/
2. Create a new organization
3. Create a new project (select **React**)
4. Copy your DSN (Data Source Name)

### 2. Configure Environment Variables

Create `.env.local`:

```bash
# Required: Sentry DSN from your project settings
VITE_SENTRY_DSN=https://abc123@o123456.ingest.sentry.io/987654

# Optional: Version tracking
VITE_SENTRY_RELEASE=1.0.0
```

For **source map uploads** (production builds), also add:

```bash
# Get auth token from: https://sentry.io/settings/account/api/auth-tokens/
SENTRY_AUTH_TOKEN=your-auth-token-here

# Your Sentry organization and project slugs
SENTRY_ORG=your-org-slug
SENTRY_PROJECT=your-project-slug
```

### 3. Test It Works

**Development:**

```bash
npm run dev
```

You should see in console:

```
ℹ️ Sentry disabled in development
```

**Production:**

```bash
npm run build
npm run preview
```

Trigger an error - it will be sent to Sentry!

---

## 🔧 How It Works

### Automatic Error Capture

```typescript
// Errors are automatically caught and sent to Sentry
function MyComponent() {
  throw new Error('This will be sent to Sentry!');
}
```

No manual code needed! Sentry's ErrorBoundary catches it.

### User Context

When users log in, their info is automatically added:

```typescript
// Happens automatically in AuthContext
setUserContext({
  id: user.id,
  email: user.email,
  name: user.name,
  isAdmin: user.isAdmin,
});
```

Now errors in Sentry show which user experienced them!

### Manual Error Reporting

```typescript
import { captureException, captureMessage } from '@/lib/sentry';

try {
  await riskyOperation();
} catch (error) {
  // Send to Sentry with context
  captureException(error, {
    operation: 'riskyOperation',
    userId: user.id,
  });
}

// Or just log a message
captureMessage('User clicked dangerous button', 'warning');
```

### Breadcrumbs (Debugging Trail)

```typescript
import { addBreadcrumb } from '@/lib/sentry';

function handlePickSubmit(pick: Pick) {
  addBreadcrumb('Submitting pick', {
    gameweek: pick.gameweekNumber,
    team: pick.teamId,
  });

  // If this fails, Sentry will show the breadcrumb trail
  await submitPick(pick);
}
```

---

## 📊 Features Enabled

### 1. Performance Monitoring

Tracks slow pages and API calls:

```typescript
// Automatically tracked:
// - Page loads
// - Route changes
// - API requests
// - Long tasks
```

Sample rate: **10%** of transactions (adjustable in `src/lib/sentry.ts`)

### 2. Session Replay

Records user sessions when errors occur:

- Video playback of what user did before error
- Console logs
- Network requests
- DOM interactions

**Privacy:**

- All text is masked
- All media is blocked
- Sensitive data removed

Sample rate:

- **10%** of normal sessions
- **100%** of error sessions

### 3. React Router Integration

Better error context:

- Current route
- Route parameters
- Navigation history

### 4. Source Maps

See **original TypeScript code** in stack traces, not minified JavaScript:

```
Before: app.js:1:2345
After:  src/components/Picks.tsx:42
```

Source maps are automatically uploaded on production builds.

---

## 🔒 Privacy & Security

### What Sentry Filters Out

```typescript
// src/lib/sentry.ts
beforeSend(event) {
  // Remove sensitive query params
  url.searchParams.delete('token');
  url.searchParams.delete('password');

  // Remove auth headers
  delete event.request?.headers?.Authorization;

  return event;
}
```

### What's Masked in Replays

- ✅ All text content (masked)
- ✅ All images/videos (blocked)
- ✅ Form inputs (masked)
- ✅ Sensitive data removed

Users can't be identified from replays.

### Errors Ignored

```typescript
ignoreErrors: [
  // Browser extensions
  'chrome-extension://',
  'moz-extension://',

  // Network errors (not app bugs)
  'NetworkError',
  'Failed to fetch',

  // Noise
  'ResizeObserver loop limit exceeded',
];
```

---

## 🎛️ Configuration

### Adjust Sample Rates

`src/lib/sentry.ts`:

```typescript
Sentry.init({
  // Performance: 10% of transactions
  tracesSampleRate: 0.1, // 0.0 to 1.0

  // Replays: 10% of normal sessions
  replaysSessionSampleRate: 0.1,

  // Replays: 100% of error sessions
  replaysOnErrorSampleRate: 1.0,
});
```

**Lower = cheaper, Higher = more data**

### Add More Integrations

```typescript
integrations: [
  // Already enabled:
  Sentry.reactRouterV6BrowserTracingIntegration(),
  Sentry.replayIntegration(),

  // Add more:
  Sentry.browserProfilingIntegration(),
  Sentry.feedbackIntegration(),
],
```

See [Sentry Integrations](https://docs.sentry.io/platforms/javascript/integrations/)

---

## 🧪 Testing Sentry

### Test Error Capture

Add a test button:

```typescript
<button onClick={() => {
  throw new Error('Test Sentry error!');
}}>
  Test Sentry
</button>
```

### Test User Context

```typescript
import * as Sentry from '@sentry/react';

Sentry.captureMessage('Test with user context');
// Check Sentry dashboard - should show user email
```

### Test Performance

```typescript
import * as Sentry from '@sentry/react';

const transaction = Sentry.startTransaction({
  name: 'Test Transaction',
  op: 'test',
});

// Do some work...
await someSlowOperation();

transaction.finish();
```

---

## 📈 Viewing Errors in Sentry

### 1. Issues Dashboard

https://sentry.io/organizations/[your-org]/issues/

- See all errors
- Filter by browser, user, route
- Mark as resolved
- Assign to teammates

### 2. Performance Dashboard

https://sentry.io/organizations/[your-org]/performance/

- Slowest pages
- Slowest API calls
- Web vitals (LCP, FID, CLS)

### 3. Session Replay

Click any error → Replays tab

Watch what the user did before the error!

---

## 🚀 Production Deployment

### GitHub Actions Example

```yaml
- name: Build with Sentry
  env:
    VITE_SENTRY_DSN: ${{ secrets.SENTRY_DSN }}
    VITE_SENTRY_RELEASE: ${{ github.sha }}
    SENTRY_AUTH_TOKEN: ${{ secrets.SENTRY_AUTH_TOKEN }}
    SENTRY_ORG: your-org
    SENTRY_PROJECT: your-project
  run: |
    cd frontend
    npm run build
```

### Vercel

Add to Vercel environment variables:

- `VITE_SENTRY_DSN`
- `VITE_SENTRY_RELEASE` (use `$VERCEL_GIT_COMMIT_SHA`)
- `SENTRY_AUTH_TOKEN`
- `SENTRY_ORG`
- `SENTRY_PROJECT`

### Release Tracking

Tag releases to track which version has bugs:

```bash
# Set release in .env
VITE_SENTRY_RELEASE=v1.2.3

# Or use git SHA
VITE_SENTRY_RELEASE=$(git rev-parse HEAD)
```

---

## 🐛 Troubleshooting

### Errors Not Appearing

**Check:**

1. Is `VITE_SENTRY_DSN` set? (console will warn)
2. Is app in production mode? (`import.meta.env.PROD`)
3. Check browser console for Sentry errors
4. Verify DSN is correct

### Source Maps Not Working

**Check:**

1. `sourcemap: true` in `vite.config.ts`
2. `SENTRY_AUTH_TOKEN` is set during build
3. Build logs show "Uploading source maps to Sentry"
4. Releases match (`VITE_SENTRY_RELEASE`)

### Too Many Events (Quota Exceeded)

**Solutions:**

1. Lower sample rates in `src/lib/sentry.ts`
2. Add more errors to `ignoreErrors`
3. Use `beforeSend` to filter events
4. Upgrade Sentry plan

### Sensitive Data in Reports

**Fix:**

1. Update `beforeSend` in `src/lib/sentry.ts`
2. Add fields to mask in `replayIntegration`
3. Use `Sentry.setContext` instead of custom data

---

## 📚 Resources

- **Sentry Docs:** https://docs.sentry.io/platforms/javascript/guides/react/
- **React Error Boundaries:** https://docs.sentry.io/platforms/javascript/guides/react/features/error-boundary/
- **Performance:** https://docs.sentry.io/platforms/javascript/guides/react/performance/
- **Session Replay:** https://docs.sentry.io/platforms/javascript/guides/react/session-replay/
- **Source Maps:** https://docs.sentry.io/platforms/javascript/sourcemaps/

---

## ✅ Checklist

Before going to production:

- [ ] Create Sentry project
- [ ] Add `VITE_SENTRY_DSN` to production env vars
- [ ] Add `SENTRY_AUTH_TOKEN` to CI/CD
- [ ] Test error reporting works
- [ ] Test source maps work (see original TS in stack traces)
- [ ] Set up alerts in Sentry
- [ ] Review privacy settings
- [ ] Adjust sample rates for your traffic

---

## 💰 Pricing

**Free tier includes:**

- 5,000 errors/month
- 10,000 performance units/month
- 500 replays/month
- 1 user

Perfect for small projects!

Paid plans start at $26/month for more volume.

---

**Happy debugging!** 🐛✨
