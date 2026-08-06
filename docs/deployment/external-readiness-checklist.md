# Preparación externa

Este registro contiene únicamente estados y referencias no secretas. Las claves y cadenas de
conexión pertenecen al gestor de secretos de cada proveedor.

## Fase 0

- [ ] Se confirmó el límite y estado de la cuenta actual de Neon.
- [ ] La conexión de Neon usa SSL.
- [ ] Se comprobó el endpoint con pooling.
- [x] Se confirmó que la base no contiene datos importantes y se autorizó continuar sin copia
  inicial.
- [ ] La cuenta y dominio de Vercel están disponibles.
- [ ] La cuenta de Cloudinary está disponible.
- [ ] El dominio remitente de Resend está verificado.
- [ ] El Sandbox de Stripe está disponible y no contiene datos financieros reales.
- [ ] La cuenta de Render está disponible.
- [ ] Se comprobó la elegibilidad de Azure for Students.
- [x] Se eligió Azure como primer destino.
- [ ] Se definió la URL final del frontend.
- [ ] Se definió la URL final de la API.
- [ ] Existe una matriz privada de secretos separada por ambiente.

## Registro de la decisión

| Dato | Valor |
|---|---|
| Primer destino del backend | Azure |
| URL de Vercel | Pendiente |
| URL de la API | Pendiente |
| Fecha de la última copia de Neon | No aplica para la migración inicial; base sin datos importantes |
| Responsable de recuperación | Pendiente |

## Validación técnica

| Comprobación | Resultado |
|---|---|
| Migración de imágenes `012` en Neon | Aplicada el 30 de julio de 2026 |
| Migración de catálogo `013` en Neon | Aplicada el 30 de julio de 2026 |
| Migración de búsqueda `014` en Neon | Aplicada el 4 de agosto de 2026 |
| Migración de horarios `015` en Neon | Aplicada el 4 de agosto de 2026 |
| Migración de vencimiento `016` en Neon | Aplicada el 4 de agosto de 2026 |
| Integración Stripe Sandbox | Implementada; pendiente de credenciales y prueba externa |
| Observabilidad y errores uniformes | Implementados; logs JSON y `X-Correlation-ID` |
| Suite PostgreSQL conjunta | 104/104 en Release el 4 de agosto de 2026 |
| Pruebas focalizadas | 12/12: imágenes, búsqueda, horarios y expiración |
| Build Release del backend | Correcto, sin advertencias |
| Lint y build del frontend | Correctos |
| Build de `backend/Dockerfile` | Correcto en validación anterior |
| Imagen local | `goisland-api:catalog-deployment` |
| `GET /api/health` desde el contenedor | `Healthy` |
| `GET /api/health/ready` contra Neon | `Ready` |

Las migraciones `014`–`016` se aplicaron sin copia previa por autorización del propietario: la
base seguía siendo de demostración y no contenía información importante.

## Activación de Stripe Sandbox

- [ ] `VITE_STRIPE_PUBLISHABLE_KEY` usa una clave `pk_test_`.
- [ ] `Stripe__SecretKey` usa una clave `sk_test_`.
- [ ] `Stripe__WebhookSecret` usa el secreto `whsec_` del endpoint público.
- [ ] El webhook apunta a `/api/payments/webhook` en la URL HTTPS elegida.
- [ ] Están suscritos `payment_intent.succeeded`, `payment_intent.payment_failed`,
  `payment_intent.canceled` y `charge.refunded`.
- [ ] Una tarjeta de prueba confirma una reserva una sola vez.
- [ ] Una tarjeta de prueba rechazada conserva la reserva pendiente y permite corregir el pago.
- [ ] Un reembolso de prueba actualiza pago, reserva, cupos e historial.

## Respaldo y recuperación

- [ ] Se creó una copia lógica con conexión Neon directa, no pooled.
- [ ] `pg_restore --list` pudo leer la copia.
- [ ] La copia se guardó cifrada fuera del repositorio.
- [ ] Se restauró en una rama o base separada.
- [ ] Se compararon los conteos principales y se verificaron imágenes de Cloudinary.
- [ ] La API recuperada respondió `Ready` y superó acceso, catálogo y reserva controlada.
- [ ] Se registraron fecha, versión y responsable del simulacro.

## Secretos y proveedores

- [ ] Se rotaron la conexión Neon y `Jwt__Key` previamente expuestas.
- [ ] Se revisaron y rotaron, si correspondía, Resend/SMTP, Cloudinary, VAPID y Stripe Sandbox.
- [ ] Google Maps acepta solo dominios autorizados y Maps JavaScript API.
- [ ] Google OAuth acepta solo los orígenes JavaScript HTTPS controlados.
- [ ] Ningún secreto aparece en el repositorio, logs, capturas o documentación compartida.

## Prueba pública

| Superficie | URL/dispositivo | Resultado | Fecha | Responsable |
|---|---|---|---|---|
| Escritorio | Pendiente | Pendiente | Pendiente | Pendiente |
| Móvil | Pendiente | Pendiente | Pendiente | Pendiente |

- [ ] Catálogo, búsqueda, slug y recarga directa funcionan desde Vercel.
- [ ] Acceso por correo y Google funciona desde el dominio público.
- [ ] Reserva, pago Sandbox, vencimiento y reembolso se verificaron de extremo a extremo.
- [ ] Imágenes, correo, páginas legales, `robots.txt` y `sitemap.xml` están disponibles.
