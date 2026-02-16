import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';

// Minimal Vite config.
// - Dev proxy is optional; by default we call relative `/api/*`.
// - If your API runs on another origin during development, set:
//   VITE_API_BASE_URL=http://localhost:5198
export default defineConfig(({ mode }) => {
  // Read env variables for dev server config.
  const env = loadEnv(mode, process.cwd(), '');

  // Default backend URL for local dev; override with VITE_API_BASE_URL if needed.
  const apiTarget = (env.VITE_API_BASE_URL || 'http://localhost:5198').replace(/\/+$/, '');

  return {
    plugins: [react()],
    server: {
      // Dev-only proxy to avoid CORS and ensure `/api/*` hits Fixon.Api.
      proxy: {
        '/api': {
          target: apiTarget,
          changeOrigin: true
        }
      }
    }
  };
});

