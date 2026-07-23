import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // El backend solo autoriza este origen en CORS (Cors:FrontendUrl).
    // strictPort evita que Vite salte a otro puerto en silencio: si 5173 esta
    // ocupado falla de inmediato en vez de dejar la API bloqueada por CORS.
    port: 5173,
    strictPort: true,
  },
})
