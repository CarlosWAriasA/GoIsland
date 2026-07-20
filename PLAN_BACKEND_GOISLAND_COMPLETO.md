# Plan de Implementacion Backend Completo - GoIsland

## Proposito

Este documento continua los bloques 0-6 del MVP y organiza el trabajo necesario para que el
backend cumpla el alcance funcional completo de GoIsland.

El plan cubre exclusivamente el backend. No se modificara el frontend durante estos bloques.

## Reglas obligatorias para todos los bloques

- Toda informacion funcional debe persistirse en PostgreSQL.
- No se permiten repositorios en memoria, datos mock, respuestas simuladas ni servicios falsos,
  con una unica excepcion temporal: el proveedor de pagos Stripe podra sustituirse por un
  `MockPaymentGateway` controlado en desarrollo y QA.
- Incluso con el gateway mock, reservas, pagos, importes, estados e idempotencia deben persistirse
  y validarse en PostgreSQL. No se permite simular esos datos en memoria.
- Las integraciones externas se validaran contra los entornos reales de prueba de cada proveedor.
- Las credenciales se cargaran desde variables de entorno o un gestor de secretos; nunca se
  guardaran en Git.
- Cada cambio de esquema debe incluir un script SQL versionado, idempotente cuando sea posible,
  y su configuracion correspondiente en `GoIslandDbContext`.
- Cada bloque debe incluir pruebas de integracion contra PostgreSQL real.
- Las pruebas deben ejecutarse dentro de transacciones y hacer `ROLLBACK` para no dejar datos.
- Las pruebas de Resend, Google Maps o Firebase deben ser pruebas externas opt-in contra sus APIs
  reales de prueba. Stripe queda aplazado y su contrato se comprobara temporalmente contra el
  gateway mock autorizado.
- Un componente no se marcara como terminado si solamente escribe mensajes en logs.
- Los endpoints privados deben validar autenticacion, rol, propiedad del recurso y estado de la
  operacion.
- Las operaciones de inventario, reserva, pago y reembolso deben ser idempotentes y transaccionales
  en los limites que controle la aplicacion.
- Cada bloque debe actualizar Swagger/OpenAPI, `GoIsland.Api.http` y este documento.

## Estado inicial confirmado

Ya existe una base funcional con:

- ASP.NET Core 8, Entity Framework Core y PostgreSQL.
- Registro, inicio de sesion, JWT y roles `Tourist`, `Host` y `Admin`.
- Consulta del usuario autenticado y actualizacion basica del nombre.
- Cambio de contrasena y recuperacion mediante token persistido en PostgreSQL.
- Consulta, busqueda y creacion basica de experiencias.
- Creacion y consulta de reservas con descuento transaccional de cupos.
- Repository, Unit of Work y estructura del patron Observer.
- Entidades y repositorio basico de pagos, pero sin flujo de pago real.
- Pruebas de integracion contra PostgreSQL real.

Todavia no se consideran funcionalidades completas:

- La recuperacion por correo hasta configurar y comprobar Resend.
- Los observadores de email, push, capacidad y dashboard, porque actualmente solo escriben logs.
- La aprobacion de anfitriones y experiencias.
- Los horarios, calendarios y disponibilidad por fecha.
- Las cancelaciones, reprogramaciones y reembolsos.
- Los pagos, resenas, reputacion, mapas y dashboards.

## Orden de implementacion

| Orden | Bloque | Dependencia principal |
|---|---|---|
| 7 | Recuperacion de contrasena operativa con Resend | Base actual |
| 8 | Perfiles y validacion de anfitriones | Usuarios y roles |
| 9 | Propiedad y moderacion de experiencias | Bloque 8 |
| 10 | Horarios, calendario y disponibilidad real | Bloque 9 |
| 11 | Ciclo completo de reservas y cancelaciones | Bloque 10 |
| 12 | Pagos persistentes con gateway mock reemplazable | Bloque 11 |
| 13 | Notificaciones reales y Outbox | Bloques 11-12 |
| 14 | Resenas y reputacion verificadas | Bloques 11-12 |
| 15 | Geolocalizacion y Google Maps | Bloques 9-10 |
| 16 | Dashboard y analitica | Bloques 11-14 |
| 17 | Seguridad, operacion y despliegue | Todos los anteriores |
| 18 | Validacion integral del backend | Todos los anteriores |

---

## Bloque 7 - Recuperacion de contrasena operativa con Resend

### Objetivo

Convertir el flujo ya implementado en una funcionalidad operativa que envie correos reales.

### Proveedor recomendado

Usar **Resend mediante SMTP**, porque el backend ya implementa `SmtpEmailSender` y no necesita
agregar un SDK especifico.

### Configuracion requerida

- Crear cuenta de Resend.
- Verificar un dominio o utilizar temporalmente el remitente de pruebas permitido por Resend.
- Crear una API key con permisos de envio.
- Configurar fuera del repositorio:
  - `Smtp__Host=smtp.resend.com`
  - `Smtp__Port=587`
  - `Smtp__EnableSsl=true`
  - `Smtp__Username=resend`
  - `Smtp__Password=<RESEND_API_KEY>`
  - `Smtp__FromEmail=<REMITENTE_VERIFICADO>`
  - `Smtp__FromName=GoIsland`
  - `Smtp__ResetPasswordUrl=<URL_DEL_CLIENTE>`
- Mantener `PasswordReset__TokenLifetimeMinutes` configurable.

### Trabajo backend

- Validar la configuracion SMTP al iniciar la aplicacion en QA/produccion.
- Registrar de forma segura los fallos de entrega, sin exponer correo, token o credenciales.
- Evitar enumeracion de usuarios manteniendo una respuesta publica uniforme.
- Invalidar tokens anteriores al emitir uno nuevo.
- Mantener tokens hasheados, con expiracion y de un solo uso.
- Preparar una prueba externa opt-in que envie un correo real a una direccion controlada.

### Criterios de terminado

- `POST /api/auth/forgot-password` acepta la solicitud sin devolver `503`.
- El correo llega realmente al destinatario permitido.
- El enlace contiene un token utilizable una sola vez.
- `POST /api/auth/reset-password` cambia el hash en PostgreSQL.
- El token usado o expirado es rechazado.
- Ningun secreto aparece en Git, logs o respuestas HTTP.

---

## Bloque 8 - Perfiles y validacion de anfitriones

### Objetivo

Impedir que una persona obtenga permisos de anfitrion sin aprobacion administrativa.

### Modelo propuesto

Crear `HostProfile` con, como minimo:

- `Id`
- `UserId`
- `DisplayName`
- `Description`
- `PhoneNumber`
- `VerificationStatus`: `Pending`, `Approved`, `Rejected`, `Suspended`
- `RejectionReason`
- `SubmittedAt`
- `ReviewedAt`
- `ReviewedByAdminId`

Los documentos de identidad no deben guardarse como archivos dentro de la base. Si se incorporan,
se almacenaran en un proveedor privado de objetos y PostgreSQL conservara solamente referencias y
metadatos protegidos.

### Endpoints propuestos

```text
POST /api/hosts/apply
GET  /api/hosts/me
PUT  /api/hosts/me
GET  /api/admin/hosts?status=Pending
GET  /api/admin/hosts/{id}
POST /api/admin/hosts/{id}/approve
POST /api/admin/hosts/{id}/reject
POST /api/admin/hosts/{id}/suspend
```

### Reglas

- El registro publico no debe conceder el rol efectivo `Host` directamente.
- Una solicitud aprobada promueve al usuario a `Host` dentro de la misma transaccion.
- Solo administradores pueden aprobar, rechazar o suspender.
- Un anfitrion suspendido no puede crear ni modificar experiencias.
- Cada decision administrativa debe quedar en una tabla de auditoria.

### Criterios de terminado

- Un turista puede solicitar convertirse en anfitrion.
- El estado se persiste en PostgreSQL.
- Un administrador puede revisar y decidir la solicitud.
- Solamente un anfitrion aprobado obtiene permisos de publicacion.
- Existen pruebas reales de autorizacion y transicion de estados.

### Resultado implementado - 20 de julio de 2026

- `HostProfile` persiste datos publicos, contacto, estado, motivo y revision administrativa.
- El registro publico siempre crea turistas y los `Host` legados sin aprobacion se degradan.
- Solicitud, consulta, edicion, listado administrativo, aprobacion, rechazo y suspension estan
  disponibles mediante los endpoints definidos.
- Aprobacion y cambio de rol se guardan atomicamente; rechazo y suspension exigen motivo.
- `AdminAuditLog` conserva cada decision y el servicio valida el estado persistido para bloquear
  tokens antiguos de perfiles suspendidos.

---

## Bloque 9 - Propiedad y moderacion de experiencias

### Objetivo

Relacionar cada experiencia con un anfitrion y completar su ciclo de publicacion.

### Cambios de dominio

- Agregar `HostId` obligatorio a `Experience`.
- Sustituir gradualmente `IsApproved` por `ApprovalStatus`:
  `Draft`, `PendingReview`, `Approved`, `Rejected`, `Suspended`.
- Agregar `RejectionReason`, `ReviewedAt`, `ReviewedByAdminId` y `UpdatedAt`.
- Definir categorias en tabla propia si deben administrarse desde el backend.
- Agregar reglas para titulo, descripcion, precio, capacidad y estado.

### Endpoints propuestos

```text
POST   /api/host/experiences
GET    /api/host/experiences
GET    /api/host/experiences/{id}
PUT    /api/host/experiences/{id}
DELETE /api/host/experiences/{id}
POST   /api/host/experiences/{id}/submit
GET    /api/admin/experiences?status=PendingReview
POST   /api/admin/experiences/{id}/approve
POST   /api/admin/experiences/{id}/reject
POST   /api/admin/experiences/{id}/suspend
```

Los endpoints publicos existentes deben seguir mostrando unicamente experiencias aprobadas.

### Reglas

- Un anfitrion solo administra experiencias de su propiedad.
- No se elimina fisicamente una experiencia que tenga reservas; se archiva o suspende.
- Una modificacion sustancial puede devolver la experiencia a revision.
- Cada cambio de moderacion queda auditado.

### Criterios de terminado

- Ninguna experiencia queda sin propietario.
- Un anfitrion puede consultar tambien sus experiencias pendientes o rechazadas.
- Existe un flujo administrativo real de aprobacion y rechazo.
- El catalogo publico nunca expone borradores o elementos pendientes.

### Resultado implementado - 20 de julio de 2026

- `Experience.HostId` es obligatorio y los datos legados fueron asignados durante la migracion.
- El ciclo de moderacion usa `Draft`, `PendingReview`, `Approved`, `Rejected` y `Suspended`.
- Los anfitriones crean borradores, consultan solo los propios, editan, eliminan si no existen
  reservas y envian a revision.
- Los administradores aprueban, rechazan con motivo y suspenden; cada cambio queda auditado.
- El catalogo, detalle, busqueda y reservas filtran exclusivamente `Approved`.
- Las pruebas PostgreSQL cubren propiedad, visibilidad publica, transiciones y auditoria.

---

## Bloque 10 - Horarios, calendario y disponibilidad real

### Objetivo

Administrar cupos por fecha y horario, no mediante un unico contador global.

### Modelo propuesto

Crear `ExperienceSchedule`:

- `Id`
- `ExperienceId`
- `StartsAt`
- `EndsAt`
- `Capacity`
- `AvailableSpots`
- `Status`: `Scheduled`, `Closed`, `Cancelled`, `Completed`
- `CreatedAt`
- `UpdatedAt`
- Token de concurrencia

### Endpoints propuestos

```text
POST   /api/host/experiences/{experienceId}/schedules
GET    /api/host/experiences/{experienceId}/schedules
PUT    /api/host/schedules/{id}
DELETE /api/host/schedules/{id}
GET    /api/experiences/{experienceId}/availability?from=&to=
GET    /api/experiences/search?location=&category=&minPrice=&maxPrice=&from=&to=&quantity=
```

### Reglas

- `EndsAt` debe ser posterior a `StartsAt`.
- No se permite reducir capacidad por debajo de cupos ya reservados.
- La disponibilidad se calcula y persiste por horario.
- Debe existir un indice por `ExperienceId` y `StartsAt`.
- Fechas se almacenan en UTC y se presentan con zona horaria explicita.
- Una reserva nueva debe apuntar a `ScheduleId`.

### Criterios de terminado

- El anfitrion puede publicar horarios futuros.
- El turista puede consultar disponibilidad real por fecha.
- Dos solicitudes concurrentes no pueden producir sobreventa.
- La base rechaza capacidades o fechas invalidas.

---

## Bloque 11 - Ciclo completo de reservas y cancelaciones

### Objetivo

Completar las transiciones de reserva, liberacion de cupos y politicas de cancelacion.

### Estados propuestos

```text
PendingPayment
Confirmed
CancelledByTourist
CancelledByHost
Completed
RefundPending
Refunded
```

Crear una tabla `ReservationStatusHistory` y, si corresponde, `CancellationPolicy`.

### Endpoints propuestos

```text
POST /api/reservations
GET  /api/reservations/my
GET  /api/reservations/{id}
POST /api/reservations/{id}/cancel
POST /api/reservations/{id}/reschedule
GET  /api/host/reservations
GET  /api/host/reservations/{id}
POST /api/host/reservations/{id}/cancel
POST /api/host/reservations/{id}/complete
```

### Reglas

- Crear una reserva genera `PendingPayment` y bloquea cupos durante un periodo definido.
- Confirmar pago cambia la reserva a `Confirmed`.
- Cancelar libera cupos exactamente una vez.
- Reprogramar mueve cupos entre horarios dentro de una transaccion.
- Un anfitrion solo consulta reservas de sus experiencias.
- Cada endpoint de escritura acepta una clave de idempotencia.
- Las transiciones invalidas deben responder `409 Conflict`.
- Las politicas de cancelacion deben producir un calculo auditable del reembolso.

### Criterios de terminado

- Se validan todas las transiciones permitidas y prohibidas.
- Cancelacion y liberacion de cupos son atomicas.
- Reintentar una solicitud no duplica reservas ni cupos.
- Turista, anfitrion y administrador tienen acceso solamente a lo autorizado.

---

## Bloque 12 - Pagos persistentes con gateway mock reemplazable

### Objetivo

Implementar todo el ciclo financiero y su persistencia usando temporalmente un gateway mock,
sin acoplar el dominio ni los endpoints a ese proveedor.

### Arquitectura requerida

- Crear `IPaymentGateway` como puerto de integracion.
- Implementar `MockPaymentGateway` unicamente para `Development` y `QA`.
- Registrar el proveedor mediante configuracion (`Payments__Provider=Mock`).
- Hacer que la aplicacion falle al iniciar si `Payments__Provider=Mock` en produccion.
- Mantener DTOs, servicios y estados independientes del gateway para agregar posteriormente
  `StripePaymentGateway` sin reescribir reservas ni controladores.
- Persistir cada intento del gateway mock con identificador externo, resultado y fecha.

### Modelos propuestos

Ampliar `Payment` y crear las entidades necesarias:

- `Provider`
- `ProviderPaymentId`
- `IdempotencyKey`
- `Currency`
- `SubtotalAmount`
- `ServiceFeeAmount`
- `PlatformCommissionAmount`
- `HostNetAmount`
- `Status`
- `FailureCode`
- `PaidAt`
- `RefundedAmount`
- `PaymentWebhookEvent` con identificador unico del proveedor
- `Refund`
- Campos de transferencia al anfitrion preparados, pero sin inventar transferencias reales

### Endpoints propuestos

```text
POST /api/reservations/{id}/payments
GET  /api/payments/{id}
POST /api/payments/{id}/mock-confirm
POST /api/payments/{id}/mock-reject
POST /api/admin/payments/{id}/refund
```

Los endpoints `mock-confirm` y `mock-reject` deben existir solamente en desarrollo/QA y no deben
mapearse en produccion.

### Reglas

- El monto se calcula exclusivamente en el servidor.
- No se reciben ni almacenan datos de tarjeta durante la etapa mock.
- Cada solicitud y confirmacion se procesa una sola vez usando idempotencia persistida.
- La reserva solo se confirma mediante un resultado exitoso del gateway configurado.
- Comision, cargo de servicio, monto del anfitrion y reembolso quedan persistidos.
- El reembolso mock actualiza un registro financiero auditable, sin afirmar que existio una
  transferencia bancaria real.
- Las respuestas identifican claramente `provider: "Mock"` durante esta fase.

### Criterios de terminado

- El gateway mock puede producir resultados aprobados y rechazados de forma controlada.
- Repetir una confirmacion no duplica pagos, reservas ni efectos.
- Un pago rechazado no confirma la reserva.
- Un reembolso mock queda identificado como tal y se persiste en PostgreSQL.
- Produccion rechaza la configuracion mock al iniciar.
- Existe una lista documentada de tareas para sustituir el gateway por Stripe PaymentIntents,
  webhooks firmados y, si se necesita, Stripe Connect.

---

## Bloque 13 - Notificaciones reales y Transactional Outbox

### Objetivo

Reemplazar los observadores que solo registran logs por efectos reales, persistentes y reintentables.

### Arquitectura

Crear una tabla `OutboxMessages` dentro de PostgreSQL. La operacion de negocio y su evento se
guardan en la misma transaccion. Un `BackgroundService` procesa los mensajes pendientes.

Crear tambien:

- `Notification`
- `UserNotificationPreference`
- `DeviceToken`
- `OutboxMessage`
- `OutboxAttempt`
- `CapacityAudit`

### Integraciones

- Email: Resend mediante SMTP o API oficial.
- Push: Firebase Cloud Messaging con credenciales protegidas.
- Dashboard: notificacion persistida consultable por API.
- Capacidad: registro persistido de cambios y deteccion de inconsistencias; el descuento critico
  de cupos sigue ocurriendo dentro de la transaccion de reserva.

### Eventos minimos

- Reserva creada y pendiente de pago.
- Pago confirmado.
- Reserva confirmada.
- Recordatorio previo.
- Cambio de horario.
- Cancelacion por turista.
- Cancelacion por anfitrion.
- Reembolso iniciado y completado.

### Endpoints propuestos

```text
GET    /api/notifications
PATCH  /api/notifications/{id}/read
PUT    /api/notifications/preferences
POST   /api/devices
DELETE /api/devices/{id}
```

### Criterios de terminado

- Los cuatro observadores producen efectos reales verificables.
- Un fallo temporal no pierde la notificacion y genera reintentos controlados.
- Los mensajes procesados no vuelven a enviarse por accidente.
- El usuario puede consultar notificaciones persistidas.
- Las pruebas externas comprueban entrega real de email y push.

---

## Bloque 14 - Resenas y reputacion verificadas

### Objetivo

Permitir opiniones solamente de turistas que completaron la experiencia.

### Modelo propuesto

Crear `Review`:

- `Id`
- `ReservationId` unico
- `UserId`
- `ExperienceId`
- `HostId`
- `Rating` de 1 a 5
- `Comment`
- `ModerationStatus`
- `CreatedAt`
- `UpdatedAt`

### Endpoints propuestos

```text
POST   /api/reservations/{id}/review
PUT    /api/reviews/{id}
DELETE /api/reviews/{id}
GET    /api/experiences/{id}/reviews
GET    /api/hosts/{id}/reviews
GET    /api/admin/reviews?status=Reported
POST   /api/admin/reviews/{id}/hide
```

### Reglas

- Solo una resena por reserva completada.
- El autor solo puede modificar su propia resena dentro del periodo permitido.
- La puntuacion agregada se calcula desde PostgreSQL.
- Los comentarios se validan y se devuelven como texto; nunca como HTML confiable.
- La moderacion no elimina silenciosamente el historial administrativo.

### Criterios de terminado

- Una reserva no completada no permite resena.
- No se pueden publicar dos resenas para la misma reserva.
- La reputacion cambia al crear, editar, ocultar o eliminar una resena.
- Las consultas publicas solo muestran resenas visibles.

---

## Bloque 15 - Geolocalizacion y Google Maps

### Objetivo

Guardar ubicaciones precisas y ofrecer busquedas reales por cercania.

### Tecnologia

- Activar PostGIS en PostgreSQL.
- Agregar `Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite`.
- Usar Google Geocoding/Places para validar direcciones.
- Usar Google Routes solamente si el backend debe calcular rutas o tiempos.

### Cambios de dominio

- Agregar direccion estructurada y punto geografico a la experiencia.
- Agregar `MeetingPointInstructions`.
- Guardar `GooglePlaceId` para evitar geocodificaciones repetidas.
- Crear indice espacial.
- Persistir cache de respuestas permitidas por los terminos del proveedor.

### Endpoints propuestos

```text
GET /api/locations/autocomplete?query=
GET /api/locations/geocode?placeId=
GET /api/experiences/nearby?latitude=&longitude=&radiusKm=
GET /api/experiences/{id}/route?originLatitude=&originLongitude=
```

### Reglas

- Las API keys nunca se devuelven al cliente desde el backend.
- Latitud, longitud y radio tienen limites validos.
- Las consultas por cercania se ejecutan en PostgreSQL/PostGIS.
- Se aplican cache y rate limiting para controlar costo y abuso.

### Criterios de terminado

- Una experiencia aprobada tiene una ubicacion geografica valida.
- La busqueda por radio devuelve resultados ordenados por distancia.
- La integracion se comprueba contra Google Maps real.
- Fallos y cuotas del proveedor se gestionan sin inventar resultados.

---

## Bloque 16 - Dashboard y analitica

### Objetivo

Proporcionar metricas reales para anfitriones y administradores usando datos persistidos.

### Endpoints propuestos

```text
GET /api/host/dashboard/summary?from=&to=
GET /api/host/dashboard/reservations?from=&to=
GET /api/host/dashboard/revenue?from=&to=&groupBy=
GET /api/host/dashboard/experiences?from=&to=
GET /api/admin/dashboard/summary?from=&to=
```

### Metricas minimas

- Reservas confirmadas, canceladas y completadas.
- Cupos vendidos y porcentaje de ocupacion.
- Ingreso bruto, comision, reembolsos e ingreso neto.
- Experiencias con mayor demanda.
- Distribucion por fecha y temporada.
- Calificacion promedio y cantidad de resenas.

### Reglas

- El anfitrion solo ve datos de sus experiencias.
- Los montos provienen de pagos confirmados y reembolsos conciliados.
- Las consultas deben agregarse en PostgreSQL, no cargando tablas completas en memoria.
- Agregar indices o tablas de proyeccion si las consultas exceden los objetivos de rendimiento.

### Criterios de terminado

- Todas las cifras pueden rastrearse hasta reservas y pagos reales.
- Los rangos de fechas y zona horaria son consistentes.
- Las pruebas comparan los agregados contra registros reales creados en PostgreSQL.

---

## Bloque 17 - Seguridad, operacion y despliegue

### Objetivo

Preparar el backend para QA compartido y produccion.

### Seguridad de aplicacion

- Forzar HTTPS y configurar correctamente reverse proxy/forwarded headers.
- Agregar rate limiting a login, registro, recuperacion, busqueda y webhooks.
- Fortalecer la politica de contrasenas.
- Agregar bloqueo progresivo o proteccion contra fuerza bruta.
- Evaluar refresh tokens hasheados y revocables en PostgreSQL.
- Validar CORS por ambiente.
- Centralizar manejo de excepciones con respuestas `ProblemDetails`.
- Incorporar auditoria para acciones administrativas y financieras.
- No registrar tokens, contrasenas, API keys ni datos de tarjeta.

### Salud y observabilidad

- Cambiar `/api/health` para comprobar al menos aplicacion y PostgreSQL.
- Crear una comprobacion separada de readiness.
- Agregar logs estructurados con correlation ID.
- Medir errores 5xx, latencia, uso de conexiones y backlog de Outbox.
- Configurar alertas de disponibilidad y fallos de integraciones.

### Base de datos y despliegue

- Mantener usuarios y bases separados para desarrollo, QA y produccion.
- Exigir SSL hacia PostgreSQL en QA/produccion.
- Ejecutar scripts de esquema mediante un proceso controlado y auditable.
- Configurar backups diarios y comprobar restauracion.
- Publicar artefactos Release reproducibles.
- Configurar secretos en el proveedor de despliegue.

### Criterios de terminado

- La API opera por HTTPS en QA.
- PostgreSQL no esta expuesto publicamente.
- Health/readiness detectan una base de datos no disponible.
- Rate limiting y autorizacion tienen pruebas negativas.
- Existe evidencia de backup y restauracion.
- No hay secretos reales dentro del repositorio.

---

## Bloque 18 - Validacion integral del backend

### Objetivo

Demostrar que los flujos completos funcionan con PostgreSQL real y con las integraciones externas
definidas para la fase. Stripe permanece como la unica integracion mock autorizada.

### Escenarios obligatorios

1. Registrar turista y autenticarlo.
2. Solicitar perfil de anfitrion y aprobarlo como administrador.
3. Crear experiencia, horarios y enviarla a revision.
4. Aprobar la experiencia y encontrarla en el catalogo.
5. Reservar un horario con cupos disponibles.
6. Crear y completar un pago mediante el gateway mock persistente.
7. Confirmar la reserva desde el resultado idempotente del gateway.
8. Recibir email y push reales.
9. Consultar la reserva como turista y anfitrion.
10. Completar la actividad y publicar una resena verificada.
11. Cancelar otra reserva, liberar cupos y ejecutar un reembolso real de prueba.
12. Consultar dashboard y comprobar sus cifras contra PostgreSQL.
13. Buscar experiencias por cercania usando PostGIS y Google Maps.

### Evidencias requeridas

- Resultado de `dotnet build` en Release.
- Resultado de todas las pruebas PostgreSQL.
- Resultado de las pruebas externas opt-in.
- Requests y respuestas principales documentados en Swagger/OpenAPI.
- Registros correspondientes en PostgreSQL.
- Eventos visibles en Resend, Firebase y Google Cloud, y registros mock claramente identificados
  en PostgreSQL para pagos.
- Confirmacion de que frontend no fue modificado durante estos bloques.

### Criterios de terminado

- Ningun escenario depende de datos en memoria. La unica respuesta simulada permitida es la del
  gateway mock de pagos, siempre identificada, persistida y bloqueada en produccion.
- Todos los estados se pueden rastrear en PostgreSQL y, cuando corresponda, en el proveedor real.
- Todos los controles de rol, propiedad e idempotencia tienen pruebas positivas y negativas.
- El backend cumple los flujos funcionales descritos por los documentos del proyecto.

---

## Decisiones que deben confirmarse antes de sus bloques

Estas decisiones no impiden comenzar los primeros bloques, pero deben resolverse antes de llegar
a la integracion correspondiente:

1. **Migracion futura a Stripe:** confirmar si la version real usara solamente PaymentIntents o
   tambien Stripe Connect para transferencias automaticas a anfitriones.
2. **Politica de cancelacion:** definir porcentajes y ventanas de reembolso para turista y
   anfitrion.
3. **Notificaciones push:** confirmar Firebase Cloud Messaging como proveedor.
4. **Dominio de correo:** indicar el dominio/remitente que se verificara en Resend.
5. **Google Maps:** confirmar si se necesita solamente geocodificacion/cercania o tambien calculo
   de rutas.

## Siguiente bloque recomendado

Comenzar por el **Bloque 7: recuperacion de contrasena con Resend**. Es pequeno, aprovecha el
codigo existente y elimina una funcionalidad parcialmente configurada. Despues debe continuarse
con los bloques 8, 9 y 10, porque propiedad, aprobacion y horarios son dependencias de reservas,
pagos, resenas y dashboards.
