# Despliegue de GoIsland

GoIsland usa el mismo contenedor del backend en Render o Azure. El frontend se publica desde
`frontend` en Vercel y PostgreSQL permanece en Neon.

## Comprobaciones previas

Antes del primer despliegue con datos reales:

1. Exportar una copia de seguridad recuperable de Neon. La migración inicial del 30 de julio de
   2026 fue autorizada sin copia porque la base aún no contenía datos importantes.
2. Confirmar que la cadena de conexión usa SSL y, si corresponde, el endpoint con pooling.
3. Crear los secretos de cada ambiente en el proveedor; no usar archivos `.env` en producción.
4. Rotar cualquier clave que haya estado expuesta y restringir las claves de Google por dominio.
5. Mantener `Payments__Mode=Sandbox` y usar solamente claves `pk_test_`/`sk_test_`.

Las decisiones que requieren acceso a cuentas se registran en
`docs/deployment/external-readiness-checklist.md`.

## Backend local en contenedor

El contexto de construcción es `backend`:

```text
docker build -t goisland-api:local -f backend/Dockerfile backend
docker run --rm -p 8080:8080 --env-file backend/.env.container goisland-api:local
```

La imagen escucha en `8080` por defecto. Si la plataforma define `PORT`, la aplicación escucha en
ese puerto sin recompilarse.

Comprobaciones:

```text
GET http://localhost:8080/api/health
GET http://localhost:8080/api/health/ready
```

El primer endpoint comprueba que el proceso vive. El segundo devuelve `503` cuando PostgreSQL no
está disponible.

## Variables del backend

| Variable | Obligatoria | Descripción |
|---|---:|---|
| `ASPNETCORE_ENVIRONMENT` | Sí | `Production` para la demostración |
| `ConnectionStrings__DefaultConnection` | Sí | Conexión PostgreSQL de Neon con SSL |
| `Jwt__Key` | Sí | Secreto aleatorio de al menos 32 bytes |
| `Jwt__Issuer` | Sí | Emisor de sesiones |
| `Jwt__Audience` | Sí | Audiencia de sesiones |
| `Cors__FrontendUrl` | Sí | Origen HTTPS de Vercel, sin ruta final |
| `AllowedHosts` | Sí | Host público de la API |
| `ForwardedHeaders__TrustManagedProxy` | Sí | `true` solo si la API está aislada detrás del proxy administrado |
| `Email__Provider` | Sí | `Resend` en el ambiente público |
| `Email__FromEmail` | Sí | Remitente verificado |
| `Email__FromName` | Sí | Nombre visible del remitente |
| `Email__ResetPasswordUrl` | Sí | URL pública de recuperación |
| `Resend__ApiKey` | Sí | Secreto de Resend |
| `GoogleAuth__ClientId` | Sí | Cliente web de Google |
| `GoogleMaps__ApiKey` | Según uso | Clave restringida de mapas |
| `WebPush__Subject` | Según uso | Contacto VAPID |
| `WebPush__PublicKey` | Según uso | Clave pública VAPID |
| `WebPush__PrivateKey` | Según uso | Clave privada VAPID |
| `Payments__Provider` | Sí | `Stripe` en la demostración |
| `Payments__Mode` | Sí | `Sandbox` |
| `Cloudinary__CloudName` | Fase 2 | Cuenta de imágenes |
| `Cloudinary__ApiKey` | Fase 2 | Identificador de carga |
| `Cloudinary__ApiSecret` | Fase 2 | Secreto de carga |
| `Stripe__SecretKey` | Fase 8 | Clave `sk_test_` |
| `Stripe__WebhookSecret` | Fase 8 | Firma `whsec_` del endpoint |

## Render

1. Crear un **Web Service** desde el repositorio.
2. Elegir runtime **Docker**, `backend/Dockerfile` como ruta y `backend` como contexto.
3. Configurar las variables anteriores en el panel.
4. Definir `ForwardedHeaders__TrustManagedProxy=true` y evitar exponer el contenedor por otra ruta.
5. Usar `/api/health` como health check.
6. Ejecutar los scripts SQL pendientes de forma controlada antes de dirigir tráfico al servicio.
7. Confirmar `/api/health/ready` y probar registro, acceso, catálogo y reserva desde Vercel.

Render proporciona `PORT`; no se debe fijar un puerto distinto en el código ni en el panel.

## Azure

La misma imagen puede publicarse en Azure Container Registry y ejecutarse en Container Apps o App
Service:

1. Construir y publicar la imagen desde `backend/Dockerfile`.
2. Configurar el puerto de destino `8080`.
3. Añadir las mismas variables del backend y confiar en cabeceras reenviadas solo si el ingreso
   público pasa exclusivamente por el proxy administrado.
4. Configurar las sondas HTTP de vida y disponibilidad con `/api/health` y
   `/api/health/ready`.
5. Ejecutar los scripts SQL pendientes antes de cambiar la revisión que recibe tráfico.

No se requieren cambios de código al alternar entre Render y Azure.

## Vercel

Crear el proyecto con:

- Root Directory: `frontend`
- Build Command: `npm run build`
- Output Directory: `dist`
- `VITE_API_URL=https://<api-publica>/api`
- `VITE_GOOGLE_CLIENT_ID=<cliente-web>`
- `VITE_STRIPE_PUBLISHABLE_KEY=pk_test_<...>` cuando se complete Stripe

`frontend/vercel.json` envía las rutas de la SPA a `index.html`, de modo que una URL de detalle se
puede abrir o recargar directamente.

## Recuperación

1. Detener temporalmente escrituras o poner fuera de tráfico la revisión afectada.
2. Restaurar la copia de Neon en una rama o base separada.
3. Validar esquema, conteos principales y `/api/health/ready`.
4. Actualizar `ConnectionStrings__DefaultConnection` para apuntar a la base validada.
5. Desplegar o reiniciar sin reconstruir la imagen.
6. Probar acceso, catálogo, una reserva controlada y notificaciones antes de reabrir tráfico.

Las imágenes se restauran desde Cloudinary y sus identificadores persistidos; nunca dependen del
disco del contenedor.
