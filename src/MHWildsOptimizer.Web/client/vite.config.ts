import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// Dev: `npm run dev` serves the client on :5173 and proxies /api to the ASP.NET host (dotnet run --project ../).
// Build: `npm run build` writes the static site into ../wwwroot, which the ASP.NET host serves.
export default defineConfig({
  plugins: [react()],
  build: {
    outDir: '../wwwroot',
    emptyOutDir: true,
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: process.env.API_URL ?? 'http://localhost:5214',
        changeOrigin: false,
      },
    },
  },
});
