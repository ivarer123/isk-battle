import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import { VitePWA } from 'vite-plugin-pwa';

export default defineConfig({
  plugins: [
    react(),
    VitePWA({
      registerType: 'autoUpdate',
      // Service worker caches the app shell only; API calls always go to the network.
      workbox: { navigateFallbackDenylist: [/^\/api\//] },
      manifest: {
        name: 'ISK Battle',
        short_name: 'ISK Battle',
        description: 'Tävla med vännerna om årets utveckling på ISK-kontot.',
        lang: 'sv',
        theme_color: '#14532d',
        background_color: '#f7f7f4',
        display: 'standalone',
        start_url: '/',
        icons: [
          { src: '/icon-192.png', sizes: '192x192', type: 'image/png' },
          { src: '/icon-512.png', sizes: '512x512', type: 'image/png' },
          { src: '/icon-512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
        ],
      },
    }),
  ],
  server: {
    // Proxy keeps local dev same-origin: no CORS, and phones on the LAN work (ARCHITECTURE.md §10).
    host: true,
    proxy: {
      '/api': { target: 'http://localhost:5080', rewrite: (path) => path.replace(/^\/api/, '') },
    },
  },
  test: { environment: 'jsdom' },
});
