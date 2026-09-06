import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

// Aspire passes the port it allocated via --port and PORT; strictPort makes a clash fail instead of drifting to
// another port, which would break the CORS origin the APIs were told to allow.
const port = Number(process.env.PORT ?? 5173)

export default defineConfig({
  plugins: [react()],
  server: { port, strictPort: true },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    css: false,
  },
})
