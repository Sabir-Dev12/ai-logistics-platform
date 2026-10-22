import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const target = env.AUTH_BACKEND_URL || env.CHAT_BACKEND_URL || 'http://localhost:5192'
  return { plugins: [react()], server: { proxy: {
    '/api': { target, changeOrigin: true },
    '^/mcp$': { target, changeOrigin: true },
    '/hubs/chat': { target, ws: true, changeOrigin: true },
  } } }
})

