# Documento técnico — GoIsland

**Proyecto Integrador · Grupo 03**
Plataforma de descubrimiento y reserva de experiencias turísticas locales en República Dominicana.

| | |
|---|---|
| Repositorio | `GoIsland` (monorepo: `backend/` + `frontend/`) |
| Última actualización | 7 de agosto de 2026 |
| Estado | Prototipo funcional desplegado, con pagos en modo Sandbox |

> Este documento se mantiene actualizado dentro del repositorio y es la referencia técnica del
> proyecto. Los documentos operativos complementarios son [DEPLOYMENT.md](DEPLOYMENT.md),
> [backend/SECURITY_CONFIGURATION.md](backend/SECURITY_CONFIGURATION.md),
> [docs/deployment/operations-runbook.md](docs/deployment/operations-runbook.md) y
> [AGENTS.md](AGENTS.md) (directrices de producto y redacción de interfaz).

---

## 1. Descripción del sistema

GoIsland conecta a personas que visitan República Dominicana con anfitriones locales que ofrecen
experiencias: recorridos, actividades acuáticas, gastronomía y cultura. La plataforma cubre el
ciclo completo: publicación de la experiencia, moderación, búsqueda, reserva, pago, realización y
reseña.

### 1.1 Actores

| Rol | Capacidades |
|---|---|
| **Turista** (`Tourist`) | Explora el catálogo, busca por texto/filtros/mapa, reserva, paga, solicita cambios, reseña |
| **Anfitrión** (`Host`) | Publica experiencias, gestiona calendario y cupos, atiende reservas y solicitudes de cambio |
| **Administrador** (`Admin`) | Modera solicitudes de anfitrión y experiencias, registra reembolsos |

El registro público solo crea cuentas de turista. El rol de anfitrión se obtiene mediante una
solicitud que un administrador aprueba.

### 1.2 Alcance del prototipo

Es un prototipo académico. **No se procesan pagos reales**: la pasarela opera en modo Sandbox con
claves de prueba (`pk_test_`/`sk_test_`) o con un proveedor simulado. Las reservas, cancelaciones y
reembolsos son demostrativos y no implican movimientos de dinero.

---

## 2. Arquitectura

### 2.1 Vista general

```
┌──────────────────┐        HTTPS/JSON        ┌────────────────────┐
│   Frontend SPA   │ ───────────────────────► │   API REST         │
│  React 19 + Vite │ ◄─────────────────────── │  ASP.NET Core 8    │
│    (Vercel)      │      JWT Bearer          │  (Render / Azure)  │
└──────────────────┘                          └─────────┬──────────┘
        │                                               │ EF Core
        │ Google Maps JS                                ▼
        │ Stripe.js                            ┌────────────────────┐
        ▼                                      │    PostgreSQL      │
   Servicios externos ◄───────────────────────►│      (Neon)        │
   Cloudinary · Stripe · Resend/SMTP · Web Push└────────────────────┘
```

Arquitectura en capas dentro de la API:

```
Controllers        → HTTP, autorización, traducción de resultados a códigos de estado
  ↓
Services           → reglas de negocio, transacciones, orquestación
  ↓
Repositories       → acceso a datos (interfaces + implementaciones EF Core)
  ↓
GoIslandDbContext  → EF Core / Npgsql
```

### 2.2 Stack tecnológico

**Backend**
- .NET 8 / ASP.NET Core 8 (Web API)
- Entity Framework Core con Npgsql (PostgreSQL)
- Autenticación JWT Bearer + Google Identity
- xUnit para pruebas (unitarias e integración contra PostgreSQL real)
- Docker (imagen publicada en Render/Azure)

**Frontend**
- React 19 + TypeScript 6, empaquetado con Vite 8
- React Router 7 (enrutamiento y protección por rol)
- TanStack Query 5 (caché de datos del servidor)
- Axios (cliente HTTP con interceptores de sesión)
- Stripe.js / React Stripe.js, Google Maps JS API
- CSS propio, sin framework de UI

**Infraestructura**
- PostgreSQL gestionado en Neon (SSL obligatorio)
- Cloudinary para almacenamiento y transformación de imágenes
- Resend o SMTP para correo transaccional
- Web Push (VAPID) para avisos fuera de la aplicación
- GitHub Actions para integración continua

### 2.3 Patrones de diseño aplicados

| Patrón | Implementación | Propósito |
|---|---|---|
| **Repository** | `IUserRepository`, `IExperienceRepository`, `IReservationRepository`, `IPaymentRepository`, … con implementaciones `Ef*` | Aísla la persistencia de las reglas de negocio y permite sustituirla en pruebas |
| **Unit of Work** | `IUnitOfWork` / `UnitOfWork` | Agrupa varias escrituras en una transacción atómica |
| **Observer** | `IReservationObserver` con `EmailNotificationObserver`, `PushNotificationObserver`, `CapacityManagerObserver`, `DashboardObserver` | Desacopla los efectos secundarios de una reserva (correo, push, cupos, métricas) del servicio que la crea |
| **Strategy** | `IPaymentGateway` con `MockPaymentGateway` y `StripePaymentGateway` | Permite cambiar de proveedor de pago por configuración, sin tocar el dominio |
| **Transactional Outbox** | `OutboxMessage` + `IOutboxWriter` + `IOutboxProcessor` + `OutboxBackgroundService` | Garantiza que las notificaciones se envíen aunque el proveedor externo falle temporalmente, sin perder mensajes ni bloquear la transacción principal |
| **Dependency Injection** | Contenedor nativo de ASP.NET Core (`Program.cs`) | Inversión de dependencias en toda la aplicación |
| **Middleware / Chain of Responsibility** | `CorrelationIdMiddleware` y pipeline de ASP.NET Core | Trazabilidad transversal de solicitudes |
| **Background Service** | `OutboxBackgroundService`, `ReservationExpirationBackgroundService` | Trabajo diferido y periódico fuera del ciclo de solicitud |

---

## 3. Modelo de datos

### 3.1 Entidades principales

| Entidad | Descripción |
|---|---|
| `User` | Cuenta con rol (`Tourist`/`Host`/`Admin`), credenciales locales opcionales |
| `UserExternalLogin` | Vínculo con proveedores externos (Google) |
| `HostProfile` | Perfil del anfitrión y su estado de verificación |
| `Experience` | Publicación: título, descripción, precio, categoría, ubicación, modo de disponibilidad |
| `ExperienceImage` | Imágenes en Cloudinary, con portada y atribución |
| `ExperienceItineraryItem` | Etapas del itinerario |
| `ExperienceSchedule` | Fecha/hora concreta con cupo disponible |
| `Reservation` | Reserva de un turista sobre una experiencia (con o sin horario) |
| `ReservationStatusHistory` | Bitácora de transiciones de estado |
| `ReservationChangeRequest` | Solicitud de cancelación o reprogramación que requiere aprobación del anfitrión |
| `ReservationIdempotencyKey` | Evita reservas duplicadas ante reintentos |
| `Payment`, `PaymentGatewayAttempt`, `PaymentWebhookEvent`, `Refund` | Ciclo de pago y su auditoría |
| `Review` | Reseña asociada a una reserva completada |
| `Notification`, `UserNotificationPreference`, `WebPushSubscription` | Avisos y preferencias por canal |
| `OutboxMessage`, `OutboxAttempt` | Cola transaccional de envíos |
| `CapacityAudit` | Auditoría de movimientos de cupo |
| `AdminAuditLog` | Registro de decisiones de moderación |
| `PasswordResetToken` | Recuperación de contraseña de un solo uso |

### 3.2 Máquinas de estado

**Experiencia** (`ExperienceApprovalStatuses`)
```
Draft ──submit──► PendingReview ──approve──► Approved ──suspend──► Suspended
                        │
                        └──reject──► Rejected
```
Al editar una experiencia aprobada, vuelve a `Draft` y debe reenviarse a revisión.

**Anfitrión** (`HostVerificationStatuses`)
```
Pending ──► Approved ──► Suspended
   └─────► Rejected
```

**Reserva** (`ReservationStatuses`)
```
PendingPayment ──pago confirmado──► Confirmed ──visita realizada──► Completed
      │                                 │
      │ (vence el tiempo de pago)       ├──cancelación──► CancelledByTourist / CancelledByHost
      ▼                                 │
   Expired                              └──reembolso──► RefundPending ──► Refunded
```
Las reservas gratuitas quedan `Confirmed` directamente. Las reservas con pago mantienen los cupos
en espera durante **15 minutos** (`ReservationExpirationOptions.HoldMinutes`); al vencer, el
servicio en segundo plano las marca `Expired` y libera los cupos.

**Pago** (`PaymentStatuses`): `Pending → Paid | Failed`, y `Paid → Refunded`.

**Solicitud de cambio** (`ReservationChangeRequestStatuses`): `Pending → Approved | Rejected`,
para los tipos `Cancel` y `Reschedule`.

### 3.3 Migraciones

El esquema se versiona con scripts SQL numerados en `backend/GoIsland.Api/Database/Scripts/`
(001–020). Se aplican en orden con `ApplyDatabaseScript.ps1` y las pruebas de integración
recorren el directorio completo, de modo que todo script nuevo queda cubierto automáticamente.

---

## 4. Funcionalidades y flujos

### 4.1 Autenticación y cuentas

- Registro con correo y contraseña, o acceso con Google (One Tap / botón).
- Contraseñas con PBKDF2 (`Pbkdf2PasswordHasher`); política de 12–128 caracteres con mayúscula,
  minúscula y número.
- Sesión mediante JWT Bearer; el frontend renueva y detecta expiración, redirigiendo al inicio de
  sesión sin perder la ruta solicitada.
- Recuperación de contraseña con token de un solo uso y vencimiento.
- Las cuentas creadas con Google no muestran opciones de contraseña.

### 4.2 Catálogo y descubrimiento

- Listado paginado con filtros por categoría, precio, duración y accesibilidad.
- Búsqueda por texto **insensible a tildes y mayúsculas**: la extensión `unaccent` de PostgreSQL,
  envuelta en la función inmutable `goisland_normalize` (script 020), permite indexar el texto ya
  normalizado con índices trigram. El término del usuario se normaliza igual en el servidor
  (`SearchText`) y en los filtros locales del mapa (`utils/searchText.ts`), de modo que "samana"
  encuentra "Samaná" y viceversa. Se aplica al catálogo, la moderación, las reservas, las reseñas
  y el listado de anfitriones.
- Búsqueda por cercanía (`/api/experiences/nearby`) con radio en kilómetros y orden por distancia.
- Vista de mapa con Google Maps y agrupación de marcadores.
- Detalle con carrusel de imágenes, visor a pantalla completa, itinerario, reseñas y disponibilidad.

### 4.3 Reserva y pago

Dos modos de disponibilidad (`ExperienceSchedulingModes`):

- **`HostScheduled`** — el anfitrión publica horarios con cupo; el turista elige uno.
- **`SelfGuided`** — el turista elige libremente fecha y hora; sin límite de cupo.

Flujo con pago:

1. El turista selecciona horario y cantidad de personas.
2. La API crea la reserva en `PendingPayment` y reserva los cupos (con clave de idempotencia).
3. Se crea el intento de pago en la pasarela; el turista completa el formulario de Stripe.
4. El webhook confirma el pago → la reserva pasa a `Confirmed` y se disparan los observadores
   (correo, push, panel del anfitrión).
5. Si el pago no se completa en 15 minutos, la reserva expira y los cupos se liberan.

Después de la fecha de la visita, el turista puede marcarla como realizada y dejar una reseña
(editable durante 30 días).

### 4.4 Solicitudes de cambio

Una reserva ya pagada no se cancela ni reprograma de forma directa: el turista envía una
**solicitud de cambio** con motivo, y el anfitrión la aprueba o rechaza desde su panel. Al aprobar
una cancelación se registra el reembolso; al aprobar una reprogramación se mueven los cupos al
nuevo horario.

### 4.5 Panel del anfitrión

- Resumen de publicaciones, reservas y pagos.
- Gestión de experiencias con guardado de borradores (basta el título) y envío a revisión.
- Calendario con creación de horarios individuales, **recurrentes** (con vista previa), **copia de
  semana**, y acciones en lote para cerrar horarios o ajustar capacidad.
- Bandeja de reservas recibidas y de solicitudes de cambio pendientes.

### 4.6 Moderación

El administrador revisa solicitudes de anfitrión y experiencias enviadas, con motivo obligatorio al
rechazar o suspender. Cada decisión queda en `AdminAuditLog`. También puede ocultar reseñas
inapropiadas y registrar reembolsos.

### 4.7 Notificaciones

Tres canales configurables por el usuario: en la aplicación, por correo electrónico y en el
dispositivo (Web Push con VAPID). Todos los envíos pasan por el patrón Outbox, con reintentos y
registro de intentos.

---

## 5. API REST

Base: `/api`. Todas las respuestas de error usan `application/problem+json` e incluyen la cabecera
`X-Correlation-ID`.

| Área | Endpoints |
|---|---|
| **Salud** | `GET /health`, `GET /health/ready` |
| **Configuración** | `GET /config/public` |
| **Autenticación** | `POST /auth/register`, `POST /auth/login`, `POST /auth/google`, `PUT /auth/change-password`, `POST /auth/forgot-password`, `POST /auth/reset-password`, `GET /auth/me` |
| **Usuarios** | `PUT /users/profile` |
| **Catálogo** | `GET /experiences`, `GET /experiences/{id}`, `GET /experiences/by-slug/{slug}`, `GET /experiences/search`, `GET /experiences/nearby`, `GET /experiences/{id}/availability` |
| **Anfitrión — solicitud** | `POST /hosts/apply`, `GET /hosts/me`, `PUT /hosts/me` |
| **Anfitrión — experiencias** | `POST|GET /host/experiences`, `GET|PUT|DELETE /host/experiences/{id}`, `POST /host/experiences/{id}/submit`, gestión de imágenes (`POST`, `PATCH`, `DELETE`) |
| **Anfitrión — horarios** | `GET|POST /host/experiences/{id}/schedules`, `POST …/recurring[/preview]`, `POST …/copy-week[/preview]`, `PATCH …/batch/close`, `PATCH …/batch/capacity`, `PUT|DELETE /host/schedules/{id}` |
| **Anfitrión — reservas** | `GET /host/reservations`, `GET /host/reservations/{id}`, `POST …/{id}/cancel`, `POST …/{id}/complete`, `GET /host/reservations/change-requests`, `POST …/change-requests/{id}/review` |
| **Anfitrión — panel** | `GET /host/dashboard` |
| **Reservas** | `POST /reservations`, `POST /reservations/self-scheduled`, `GET /reservations/my`, `GET /reservations/{id}`, `POST …/{id}/cancel`, `POST …/{id}/complete`, `POST …/{id}/reschedule`, `POST …/{id}/cancellation-requests`, `POST …/{id}/reschedule-requests` |
| **Pagos** | `POST|GET /reservations/{id}/payments`, `GET /payments/{id}/checkout`, `POST /payments/webhook`, `POST /admin/payments/{id}/refund` |
| **Reseñas** | `POST /reservations/{id}/review`, `PUT|DELETE /reviews/{id}`, `GET /experiences/{id}/reviews`, `GET /hosts/{id}/reviews`, `GET /admin/reviews`, `POST /admin/reviews/{id}/hide` |
| **Notificaciones** | `GET /notifications`, `PATCH /notifications/{id}/read`, `GET|PUT /notifications/preferences` |
| **Dispositivos** | `GET /devices/web-push-public-key`, `POST /devices`, `DELETE /devices/{id}` |
| **Moderación** | `GET /admin/hosts`, `GET /admin/hosts/{id}`, `POST …/approve|reject|suspend`; `GET /admin/experiences`, `POST …/approve|reject|suspend` |

---

## 6. Seguridad

| Control | Implementación |
|---|---|
| Contraseñas | PBKDF2 con sal por usuario; política de complejidad en cliente y servidor |
| Sesiones | JWT firmado con `Jwt:Key` (mínimo 32 bytes aleatorios); emisor y audiencia validados |
| Autorización | Por rol en la API (`[Authorize(Roles=…)]`) y en el frontend (`RoleRoute`), más verificación de propiedad del recurso |
| Límite de solicitudes | `AddRateLimiter` con políticas dedicadas para autenticación (10/min) y recuperación de contraseña; respuesta `429` con `Retry-After` |
| Protección de inicio de sesión | Bloqueo progresivo tras intentos fallidos (script 010) |
| CORS | Origen único configurado en `Cors:FrontendUrl`; obligatorio HTTPS fuera de desarrollo |
| Transporte | HSTS y redirección HTTPS en producción; SSL obligatorio hacia PostgreSQL |
| Cabeceras de proxy | `ForwardedHeaders` habilitado solo si la API está aislada tras el proxy administrado |
| Idempotencia | Claves en creación de reservas y deduplicación de eventos de webhook |
| Concurrencia | Control optimista sobre cupos, con conflicto explícito (`409`) en vez de sobreventa |
| Webhooks | Verificación de firma de Stripe (`Stripe:WebhookSecret`) |
| Secretos | Nunca en el repositorio; `dotnet user-secrets` en desarrollo y gestor del proveedor en QA/producción |
| Trazabilidad | `X-Correlation-ID` en toda respuesta; logs JSON sin contenido de solicitud ni secretos |
| Auditoría | `AdminAuditLog`, `CapacityAudit`, `ReservationStatusHistory`, `PaymentGatewayAttempt` |

Validaciones de arranque: la aplicación no inicia si faltan `Jwt:Key`, `Cors:FrontendUrl`,
credenciales de Cloudinary o claves de Stripe fuera de desarrollo, ni si se intenta usar el
proveedor de pagos simulado en un ambiente público.

El detalle operativo está en [backend/SECURITY_CONFIGURATION.md](backend/SECURITY_CONFIGURATION.md).

---

## 7. Frontend

### 7.1 Organización

```
frontend/src/
├── pages/        20 pantallas (públicas, turista, anfitrión, administración)
├── components/   componentes reutilizables (Button, Input, Dialog, EmptyState, …)
├── routes/       AppRoutes, ProtectedRoute, RoleRoute
├── context/      AuthContext (sesión y rol)
├── hooks/        useAuth, usePageMetadata, useRevealOnScroll, useDismissable
├── queries/      configuración de TanStack Query y claves de caché
├── services/     un módulo por dominio de la API
├── utils/        etiquetas de estado, política de contraseñas, fechas
└── constants/    catálogos estáticos
```

### 7.2 Decisiones relevantes

- **Carga diferida** de las pantallas pesadas (mapa y panel del anfitrión) con `React.lazy`.
- **Caché de servidor** con TanStack Query, con claves centralizadas en `queryKeys.ts`.
- **Estados de carga** con esqueletos visuales y anuncios `aria-live` solo para lectores de
  pantalla, evitando texto redundante en la interfaz.
- **Accesibilidad**: roles ARIA, `aria-busy`, etiquetas asociadas a cada campo, foco visible y
  contraste verificado.
- **Modo sin conexión**: `OfflineBanner` y reintentos.
- **Redacción de interfaz** gobernada por [AGENTS.md](AGENTS.md): textos breves, sin terminología
  técnica, ocultando lo no aplicable en vez de explicarlo.

---

## 8. Calidad y pruebas

### 8.1 Integración continua

`.github/workflows/quality.yml` se ejecuta en cada *pull request* y en `main`:

- **Backend** — levanta PostgreSQL 16 como servicio y ejecuta `dotnet test` en Release.
- **Frontend** — `npm ci`, `npm run lint` y `npm run build`.

### 8.2 Suite de pruebas

112 pruebas en `backend/GoIsland.Api.Tests`, organizadas en:

- **Unitarias** — servicios de dominio con dobles de prueba.
- **Integración** (`PostgresIntegrationTestBase`) — contra PostgreSQL real, aplicando todos los
  scripts de esquema y aislando cada prueba en una transacción con reversión.

Cobertura funcional: autenticación y Google, catálogo y búsqueda, cercanía, horarios (incluidos
recurrentes y copia de semana), reservas, expiración, pagos y webhooks, reembolsos, solicitudes de
cambio, moderación, notificaciones y outbox, reseñas.

### 8.3 Comandos

```bash
dotnet test backend/GoIsland.Api.Tests/GoIsland.Api.Tests.csproj -c Release
```

```bash
npm --prefix frontend run lint && npm --prefix frontend run build
```

---

## 9. Despliegue

| Componente | Plataforma | Notas |
|---|---|---|
| Frontend | Vercel | Build desde `frontend/`; variables `VITE_*` |
| Backend | Render o Azure | Imagen Docker desde `backend/Dockerfile`; escucha en `PORT` o `8080` |
| Base de datos | Neon (PostgreSQL) | SSL obligatorio; endpoint con pooling |
| Imágenes | Cloudinary | Carga firmada desde la API |
| Correo | Resend (público) / SMTP (desarrollo) | Remitente verificado |

Sondas de disponibilidad: `GET /api/health` (proceso vivo) y `GET /api/health/ready` (`503` si
PostgreSQL no responde).

El procedimiento completo — copia de seguridad, restauración, rotación de claves y validación
posterior — está en [docs/deployment/operations-runbook.md](docs/deployment/operations-runbook.md),
y las decisiones que dependen de accesos externos en
[docs/deployment/external-readiness-checklist.md](docs/deployment/external-readiness-checklist.md).

---

## 10. Limitaciones conocidas y trabajo futuro

**Limitaciones del prototipo**
- Los pagos operan solo en Sandbox; no hay liquidación real a anfitriones.
- No hay mensajería directa entre turista y anfitrión.
- La aplicación está disponible únicamente en español.
- Las imágenes del catálogo de demostración provienen de bancos de imágenes con atribución.

**Mejoras identificadas**
- Página pública de perfil del anfitrión: el endpoint `GET /api/hosts/{id}/reviews` ya existe pero
  ninguna pantalla lo consume.
- Cancelación con reembolso parcial según la política de cada experiencia.
- Lista de espera cuando un horario queda sin cupos.
- Panel de métricas históricas para el anfitrión (ocupación, ingresos por período).
- Exportación de reservas del anfitrión.
- Internacionalización (inglés) y formato de moneda por región.
- Verificación de identidad del anfitrión con documentos, si el proyecto pasara a producción real.
