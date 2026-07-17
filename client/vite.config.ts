/// <reference types="vitest/config" />
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

const API_TARGET = process.env.VITE_API_PROXY ?? 'http://localhost:5180'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': { target: API_TARGET, changeOrigin: true },
      '/health': { target: API_TARGET, changeOrigin: true },
    },
  },
  test: {
    globals: false,
    environment: 'jsdom',
    setupFiles: './src/test/setup.ts',
    css: false,
    include: ['src/**/*.{test,spec}.{ts,tsx}'],
  },
})
