import * as Sentry from '@sentry/react';
import { useEffect } from 'react';
import {
  useLocation,
  useNavigationType,
  createRoutesFromChildren,
  matchRoutes,
} from 'react-router-dom';

/**
 * Sentry Configuration
 *
 * Error tracking and performance monitoring for production
 */

export function initializeSentry() {
  // Only initialize Sentry in production
  if (import.meta.env.PROD && import.meta.env.VITE_SENTRY_DSN) {
    Sentry.init({
      dsn: import.meta.env.VITE_SENTRY_DSN,

      // Environment
      environment: import.meta.env.MODE, // 'production' or 'staging'

      // Release tracking (for source maps)
      release: import.meta.env.VITE_SENTRY_RELEASE || 'dev',

      // Integrations
      integrations: [
        // React Router integration for better error context
        Sentry.reactRouterV6BrowserTracingIntegration({
          useEffect,
          useLocation,
          useNavigationType,
          createRoutesFromChildren,
          matchRoutes,
        }),

        // Replay sessions for debugging
        Sentry.replayIntegration({
          maskAllText: true, // Privacy: mask all text
          blockAllMedia: true, // Privacy: block all media
        }),
      ],

      // Performance Monitoring
      tracesSampleRate: 0.1, // Capture 10% of transactions

      // Session Replay
      replaysSessionSampleRate: 0.1, // Sample 10% of sessions
      replaysOnErrorSampleRate: 1.0, // Always capture errors

      // Privacy: Don't send sensitive data
      beforeSend(event) {
        // Remove sensitive query parameters
        if (event.request?.url) {
          const url = new URL(event.request.url);
          url.searchParams.delete('token');
          url.searchParams.delete('password');
          event.request.url = url.toString();
        }

        // Don't send errors in development
        if (import.meta.env.DEV) {
          return null;
        }

        return event;
      },

      // Ignore common errors
      ignoreErrors: [
        // Browser extensions
        'top.GLOBALS',
        'chrome-extension://',
        'moz-extension://',

        // Network errors
        'NetworkError',
        'Failed to fetch',

        // React errors that are handled
        'ResizeObserver loop limit exceeded',
      ],

      // Only track errors from your domain
      allowUrls: [/https?:\/\/(www\.)?eplpredict\.com/, /https?:\/\/.*\.vercel\.app/],
    });

    console.log('✅ Sentry initialized');
  } else if (import.meta.env.DEV) {
    console.log('ℹ️ Sentry disabled in development');
  } else {
    console.warn('⚠️ Sentry DSN not configured');
  }
}

/**
 * Set user context for error tracking
 */
export function setUserContext(user: {
  id: string;
  email: string;
  name?: string;
  isAdmin?: boolean;
}) {
  Sentry.setUser({
    id: user.id,
    email: user.email,
    username: user.name,
    isAdmin: user.isAdmin,
  });
}

/**
 * Clear user context on logout
 */
export function clearUserContext() {
  Sentry.setUser(null);
}

/**
 * Manually capture an exception
 */
export function captureException(error: Error, context?: Record<string, unknown>) {
  if (context) {
    Sentry.setContext('additional', context);
  }
  Sentry.captureException(error);
}

/**
 * Manually capture a message
 */
export function captureMessage(message: string, level: Sentry.SeverityLevel = 'info') {
  Sentry.captureMessage(message, level);
}

/**
 * Add breadcrumb for debugging
 */
export function addBreadcrumb(message: string, data?: Record<string, unknown>) {
  Sentry.addBreadcrumb({
    message,
    data,
    level: 'info',
  });
}
