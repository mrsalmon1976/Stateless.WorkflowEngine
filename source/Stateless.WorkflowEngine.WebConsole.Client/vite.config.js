import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import ui from '@nuxt/ui/vite'

// The .NET host serves the built client from wwwroot - in development, run the host (port 8811) and
// `npm run dev`, and Vite proxies api calls through to the host.
export default defineConfig({
  plugins: [
    vue(),
    ui({
      ui: {
        colors: {
          primary: 'emerald',
          neutral: 'slate'
        }
      },
      // bundle icons so the console works on servers without internet access
      icon: {
        clientBundle: {
          scan: true
        }
      }
    })
  ],
  server: {
    port: 5173,
    proxy: {
      '/api': 'http://localhost:8811'
    }
  },
  build: {
    outDir: '../Stateless.WorkflowEngine.WebConsole/wwwroot',
    emptyOutDir: true,
    // viz.js (definition graphs) is large but is only loaded on demand
    chunkSizeWarningLimit: 1500
  }
})
