import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// Aspire injects the BFF's address (WithReference(bff) in AppHost). The fallback is the
// BFF's own launchSettings port, for running `npm run dev` without Aspire.
const bffUrl =
  process.env.BFF_HTTP ??
  process.env.services__bff__http__0 ??
  process.env.BFF_HTTPS ??
  process.env.services__bff__https__0 ??
  'http://localhost:5008'

// The browser only ever talks to this dev server, so the BFF's auth cookie is same-origin.
const proxyToBff = { target: bffUrl, changeOrigin: true, secure: false }

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': proxyToBff,
      '/bff': proxyToBff,
    },
  },
})
