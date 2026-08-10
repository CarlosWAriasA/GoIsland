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
El procedimiento completo de copia, restauración, rotación y validación está en
`docs/deployment/operations-runbook.md`.

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

El backend escribe logs JSON por consola. Todas las respuestas incluyen `X-Correlation-ID`; se
debe registrar ese valor al investigar un error sin exponer contenido de la solicitud ni secretos.

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
| `Reservations__Expiration__HoldMinutes` | Sí | Minutos disponibles para pagar; inicialmente `15` |
| `Reservations__Expiration__PollIntervalSeconds` | Sí | Frecuencia de reconciliación; inicialmente `30` |
| `Reservations__Expiration__BatchSize` | Sí | Reservas procesadas por lote; inicialmente `50` |
| `Cloudinary__CloudName` | Fase 2 | Cuenta de imágenes |
| `Cloudinary__ApiKey` | Fase 2 | Identificador de carga |
| `Cloudinary__ApiSecret` | Fase 2 | Secreto de carga |
| `Stripe__SecretKey` | Sí | Clave secreta de prueba `sk_test_`; las claves live se rechazan |
| `Stripe__WebhookSecret` | Sí | Firma `whsec_` del endpoint |

## Render

1. Crear un **Web Service** desde el repositorio.
2. Elegir runtime **Docker**, `backend/Dockerfile` como ruta y `backend` como contexto.
3. Configurar las variables anteriores en el panel.
4. Definir `ForwardedHeaders__TrustManagedProxy=true` y evitar exponer el contenedor por otra ruta.
5. Usar `/api/health` como health check.
6. Confirmar que las migraciones `014_add_catalog_search_indexes.sql`,
   `015_add_unique_schedule_start.sql` y `016_add_reservation_expiration.sql` están aplicadas. Se
   aplicaron a la base actual de Neon el 4 de agosto de 2026.
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
- `VITE_SITE_URL=https://<frontend-publico>` sin barra final
- `VITE_GOOGLE_CLIENT_ID=<cliente-web>`
- `VITE_STRIPE_PUBLISHABLE_KEY=pk_test_<...>`; las claves live no cargan el formulario

El build consulta el catálogo público para generar `sitemap.xml` y el HTML social de cada
experiencia aprobada. Por eso la API debe estar disponible durante el despliegue y la carga manual
del catálogo debe completarse antes del build definitivo. `frontend/vercel.json` conserva la
reescritura de la SPA para rutas que todavía no existían durante el último build.

## Despliegue continuo

Cada commit a `main` publica automáticamente. El frontend lo despliega Vercel con su propia
integración de Git; el backend lo despliega `.github/workflows/deploy-backend.yml`.

### Backend (GitHub Actions → Azure App Service)

El workflow corre las pruebas contra un PostgreSQL efímero del runner y solo despliega si pasan.
Al terminar comprueba `/api/health/ready` y falla si la API no responde.

En **Settings → Secrets and variables → Actions** del repositorio:

| Tipo | Nombre | Valor |
|---|---|---|
| Variable | `AZURE_WEBAPP_NAME` | Nombre del Web App, por ejemplo `goisland-api-carlos` |
| Secreto | `AZURE_WEBAPP_PUBLISH_PROFILE` | Contenido del publish profile |

El publish profile se obtiene en el portal de Azure, en el Web App → **Descargar perfil de
publicación**, y se pega completo como valor del secreto. Contiene credenciales de publicación:
no debe versionarse ni compartirse fuera de los secretos del repositorio. Si se filtra, se
regenera desde el mismo menú, lo que invalida el anterior.

Las variables de aplicación del backend no las gestiona el workflow. Se suben una sola vez con
`backend/deploy-appsettings.ps1`, que las lee de `appsettings.Development.json` local:

```text
./backend/deploy-appsettings.ps1 -AppName <web-app> -ResourceGroup <grupo> -FrontendUrl https://<frontend-publico>
```

### Frontend (Vercel)

En el panel de Vercel, **Add New → Project**, se importa el repositorio y se configura con los
valores de la sección anterior. Con eso Vercel despliega cada commit a `main` y genera vistas
previas por cada pull request. Las variables `VITE_*` se cargan en **Settings → Environment
Variables**; ninguna es secreta, pero `VITE_API_URL` debe apuntar a la API ya desplegada.

### Orden al desplegar cambios de esquema

Los scripts SQL no se ejecutan solos, y el orden importa:

1. Aplicar los scripts pendientes de `backend/GoIsland.Api/Database/Scripts/` con
   `ApplyDatabaseScript.ps1`.
2. Confirmar `/api/health/ready` sobre la revisión activa.
3. Empujar a `main` para que el workflow despliegue el código que depende del esquema nuevo.

Invertir el orden deja la API desplegada consultando objetos que aún no existen. Por ejemplo, la
búsqueda sin tildes depende de la función `goisland_normalize` que crea el script `020`.

## Stripe Sandbox

En el ambiente público, configurar `Payments__Provider=Stripe`, `Payments__Mode=Sandbox` y las
credenciales de prueba indicadas arriba. Registrar uno de estos endpoints, según el proveedor del
backend:

```text
https://<api-azure>/api/payments/webhook
https://<api-render>/api/payments/webhook
```

Suscribir exclusivamente los eventos utilizados por la aplicación:

- `payment_intent.succeeded`
- `payment_intent.payment_failed`
- `payment_intent.canceled`
- `charge.refunded`

El webhook verifica `Stripe-Signature` antes de procesar el evento. Los eventos repetidos se
registran una sola vez. Una confirmación tardía de una reserva vencida inicia un reembolso de
prueba automático y nunca recupera los cupos ya liberados.

Para desarrollo sin conexión se mantiene `Payments__Provider=Mock`; sus acciones simuladas no
están disponibles cuando el proveedor activo es Stripe.

## Recuperación

1. Detener temporalmente escrituras o poner fuera de tráfico la revisión afectada.
2. Restaurar la copia de Neon en una rama o base separada; nunca sobre la base activa.
3. Validar esquema, conteos principales y `/api/health/ready`.
4. Actualizar `ConnectionStrings__DefaultConnection` para apuntar a la base validada.
5. Desplegar o reiniciar sin reconstruir la imagen.
6. Probar acceso, catálogo, una reserva controlada y notificaciones antes de reabrir tráfico.

Las imágenes se restauran desde Cloudinary y sus identificadores persistidos; nunca dependen del
disco del contenedor.

Los comandos, comprobaciones y procedimiento de reversión están detallados en
`docs/deployment/operations-runbook.md`.
