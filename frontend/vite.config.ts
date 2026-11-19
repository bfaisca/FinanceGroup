import { fileURLToPath, URL } from 'node:url'
import { defineConfig, loadEnv } from 'vite'
import vue from '@vitejs/plugin-vue'

// Em desenvolvimento, /api e /health são encaminhados para a API (mesma origem — D-006).
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), 'VITE_')
  const apiTarget = env.VITE_API_PROXY_TARGET || 'http://localhost:5000'

  return {
    plugins: [vue()],
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url)),
      },
    },
    server: {
      port: 5173,
      proxy: {
        '/api': { target: apiTarget, changeOrigin: false, xfwd: true },
        '/health': { target: apiTarget, changeOrigin: false, xfwd: true },
      },
    },
    preview: {
      proxy: {
        '/api': { target: apiTarget, changeOrigin: false, xfwd: true },
        '/health': { target: apiTarget, changeOrigin: false, xfwd: true },
      },
    },
    build: {
      target: 'es2022',
      sourcemap: false,
    },
  }
})
