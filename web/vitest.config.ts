import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import path from 'path';

// Unit/component test config, kept separate from vite.config.ts so the app
// build (`tsc -b && vite build`) and the top-level Playwright e2e suite are
// untouched. Runs only `src/**/*.test.{ts,tsx}` in jsdom.
export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.test.{ts,tsx}'],
    css: false,
  },
});
