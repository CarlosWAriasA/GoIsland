# GoIsland Frontend

Cliente React + TypeScript para la API de GoIsland.

## Requisitos

- Node.js compatible con Vite 8.
- API de GoIsland disponible en `http://localhost:5057`.

## Configuracion local

1. Copia `.env.example` como `.env` y configura `VITE_GOOGLE_CLIENT_ID` con el mismo cliente web
   usado por el backend.
2. Instala dependencias con `npm install`.
3. Inicia el cliente con `npm run dev`.

Vite usa `http://localhost:5173` y el valor predeterminado de `VITE_API_URL` es
`http://localhost:5057/api`.

## Despliegue en Vercel

- Directorio raíz: `frontend`.
- Build: `npm run build`.
- Salida: `dist`.
- Variables: `VITE_API_URL`, `VITE_GOOGLE_CLIENT_ID` y, al habilitar pagos,
  `VITE_STRIPE_PUBLISHABLE_KEY`.

`vercel.json` conserva las rutas de la aplicación al abrirlas o recargarlas directamente. La guía
completa de ambientes y recuperación está en `../DEPLOYMENT.md`.

## Verificacion

```text
npm run lint
npm run build
```

## Integracion disponible

- Autenticacion: registro e inicio de sesion por correo o Google, identidad y actualizacion de
  perfil.
- Catalogo real: listado y busqueda por ubicacion, una categoria y precio maximo.
- Reservas reales: creacion, listado privado y detalle propio.
- Anfitriones: solicitud, estado y edicion en `/host-profile`.
- Experiencias propias: borradores, edicion y envio a revision en `/host/experiences`.
- Administracion: aprobacion, rechazo y suspension en `/admin/moderation`.
- Galerías persistentes en Cloudinary con portada, texto alternativo y tamaños optimizados.
- Ficha completa de experiencias con duración, encuentro, inclusiones, requisitos, accesibilidad,
  idiomas, cancelación e itinerario ordenado.
- Manejo comun de errores API con `message` y errores de validacion por campo.

Favoritos y recomendaciones no se simulan mientras sus flujos persistentes no estén integrados.
