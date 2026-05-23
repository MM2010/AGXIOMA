import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  resolve: {
    // Rolldown (Vite 8) errors on /*#__PURE__*/ in signalr ESM.
    // Point to CJS build which doesn't have that issue.
    alias: {
      '@microsoft/signalr': '@microsoft/signalr/dist/cjs/index.js',
    },
  },
  server: {
    port: 3001,
    proxy: {
      '/api': { target: 'http://localhost:5013', changeOrigin: true },
      '/hub': { target: 'http://localhost:5013', changeOrigin: true, ws: true },
    },
  },
});
