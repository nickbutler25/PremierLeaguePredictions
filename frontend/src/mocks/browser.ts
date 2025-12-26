import { setupWorker } from 'msw/browser';
import { handlers } from './handlers';

/**
 * MSW Browser Worker (Development)
 *
 * This configures MSW to intercept requests in the browser
 * Useful for development without a backend
 *
 * To use:
 * 1. Generate service worker: npx msw init public/ --save
 * 2. Enable in main.tsx with VITE_ENABLE_MSW=true
 */

export const worker = setupWorker(...handlers);
