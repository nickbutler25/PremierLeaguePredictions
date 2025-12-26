import { setupServer } from 'msw/node';
import { handlers } from './handlers';

/**
 * MSW Server for Node.js (Tests)
 *
 * This configures MSW to intercept requests in a Node.js environment
 * Used in Vitest tests
 */

export const server = setupServer(...handlers);
