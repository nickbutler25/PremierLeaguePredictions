import { defineConfig, type Plugin } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'path';
import fs from 'fs';
import { sentryVitePlugin } from '@sentry/vite-plugin';

/**
 * Emits sw.js from src/sw.template.js with a per-build id substituted in.
 *
 * The service worker cannot be a static file in public/. A browser only re-runs a worker's
 * install step when the file's bytes change, so a fixed sw.js is installed once on a user's
 * first visit and never again - and with the cache names baked in, whatever it cached that day
 * is served for ever. Stamping the build id makes the bytes differ on every deploy, so the
 * update is detected, and scopes the cache names to one build so activate clears the old ones.
 */
function serviceWorkerPlugin(): Plugin {
  const buildId = Date.now().toString(36);
  return {
    name: 'epl-service-worker',
    apply: 'build',
    generateBundle() {
      const template = fs.readFileSync(path.resolve(__dirname, 'src/sw.template.js'), 'utf-8');
      this.emitFile({
        type: 'asset',
        fileName: 'sw.js',
        source: template.replaceAll('__BUILD_ID__', buildId),
      });
    },
  };
}

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    serviceWorkerPlugin(),
    // Upload source maps to Sentry in production builds
    process.env.VITE_SENTRY_DSN && process.env.SENTRY_AUTH_TOKEN
      ? sentryVitePlugin({
          org: process.env.SENTRY_ORG,
          project: process.env.SENTRY_PROJECT,
          authToken: process.env.SENTRY_AUTH_TOKEN,
        })
      : null,
  ].filter(Boolean),
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    headers: {
      'Referrer-Policy': 'no-referrer-when-downgrade',
    },
    proxy: {
      '/api': {
        target: 'http://localhost:5154',
        changeOrigin: true,
      },
      '/hubs': {
        target: 'http://localhost:5154',
        changeOrigin: true,
        ws: true,
      },
    },
  },
  build: {
    sourcemap: true, // Enable source maps for Sentry
    rollupOptions: {
      output: {
        manualChunks: {
          'react-vendor': ['react', 'react-dom', 'react-router-dom'],
          'query-vendor': ['@tanstack/react-query'],
          'ui-vendor': ['@radix-ui/react-toast', '@radix-ui/react-label'],
        },
      },
    },
  },
});
