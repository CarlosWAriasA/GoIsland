# Plan de Integracion Frontend-Backend y Mejora UX/UI - GoIsland

> **Documento histórico.** Conserva las entregas de integración y UX. El plan activo de
> preparación y despliegue es `PLAN_PREPARACION_CATALOGO_Y_DESPLIEGUE.md`.

## Estado de ejecucion

Inicio: 19 de julio de 2026.

### Extension completada - Google Identity y correo configurable

- Login y registro aceptan la credencial de Google Identity Services mediante `POST /api/auth/google`.
- El backend valida audiencia, firma, emisor, expiracion y correo verificado; persiste el `sub` de
  Google como identidad estable separada del correo.
- Las cuentas nuevas se crean como turista y las cuentas locales solo se vinculan automaticamente
  cuando Google conserva autoridad sobre el correo (Gmail o Google Workspace).
- `Email:Provider` selecciona `Smtp` o `Resend`; ambos reutilizan remitente, URL y plantilla de
  recuperacion, sin credenciales dentro del repositorio.
- El script `004_create_external_logins.sql` fue aplicado al PostgreSQL compartido.
- Verificacion aprobada: 27 pruebas .NET/PostgreSQL, `npm run lint` y `npm run build`.

### Primera entrega completada - base del Bloque 0

- El catalogo consume `GET /api/experiences` y `GET /api/experiences/search`.
- Se eliminaron `experiencesMock.json`, la latencia artificial y los campos no respaldados
  `featured`, `rating` y `reviewCount`.
- Se retiraron del flujo Favoritos y la confirmacion de reserva simulada.
- Los filtros del cliente coinciden con el contrato real: `location`, una `category` y `maxPrice`.
- Se agregaron tipos TypeScript exactos, cancelacion de solicitudes y traduccion central de errores
  con `message` y `errors`.
- El registro publico del cliente crea turistas y ya no presenta seleccion directa de anfitrion.
- Vite, CORS y la URL local de recuperacion quedaron alineados en `http://localhost:5173`.
- `GET /api/auth/me` ahora devuelve el mismo `UserResponse` que login, registro y perfil.
- Verificacion aprobada: `npm run lint`, `npm run build`, compilacion backend Release, salud,
  catalogo, busqueda y preflight CORS.

### Bloque 0 cerrado

- La instancia anterior fue sustituida por un binario actual recompilado y saludable.
- Swagger confirma 14 rutas, autenticacion Bearer y los contratos de identidad y busqueda.
- `GoIsland.Api.http` contiene ejemplos positivos y negativos para `400`, `401` y `404`.
- Las excepciones de conectividad PostgreSQL se traducen a `503` con un objeto JSON `message`, sin
  exponer una pagina de excepcion ni detalles internos al frontend.
- Las 18 pruebas de integracion PostgreSQL pasan al ejecutarse con acceso de red al servidor
  administrado.

### Bloque 1 completado

- Se aplicaron los tokens oceano, turquesa, arena, coral y colores semanticos.
- La navegacion principal ahora tiene menu movil accesible, estados activos y controles de 44 px.
- Se agrego `Saltar al contenido principal` y foco visible global.
- El footer se redujo a rutas funcionales y contenido centrado en Republica Dominicana.
- Los estilos internos de `Button`, `Navbar`, `Footer` y `Logo` se trasladaron al sistema global.
- Se adopto Manrope como tipografia principal.
- Se incorporaron `Input`, `SelectField`, `PriceField`, `StatusBadge`, `Alert`, `Skeleton`,
  `EmptyState`, `ErrorState` y `Dialog` con asociaciones y estados accesibles.
- Login, registro, perfil y ruta protegida usan el shell responsive y no contienen hojas `<style>`
  embebidas.
- Las clases genericas `glass-*` se sustituyeron por `surface-panel` y `surface-card`.
- La validacion real en `360x800`, `390x844`, `768x1024`, `1280x720` y `1440x900` confirmo
  cero desbordamiento horizontal y cero controles visibles menores de `44x44`.
- El menu movil expone `aria-expanded`, abre y cierra correctamente y conserva nombres accesibles.
- Los errores de formulario usan `aria-invalid` y `aria-describedby`; el enlace de salto lleva el
  foco a `main`.
- `npm run lint` y `npm run build` finalizan sin errores ni advertencias de aplicacion.

### Bloque 2 completado

- Login y registro conservan temporalmente `token`, expiracion y usuario en `sessionStorage`; la
  sesion sobrevive una recarga dentro de la pestana y se elimina al cerrar sesion o expirar.
- El cliente adjunta Bearer de forma central, restaura la identidad con `GET /api/auth/me` y limpia
  la sesion mediante un interceptor global ante respuestas `401` autenticadas.
- Las rutas privadas conservan la ruta solicitada y regresan a ella despues de iniciar sesion.
- Perfil actualiza el nombre real mediante `PUT /api/users/profile` y mantiene sincronizada la
  identidad almacenada.
- Se agregaron pantallas separadas para solicitar recuperacion, restablecer mediante token y cambiar
  la contrasena desde una ruta privada.
- Todos los campos de contrasena incluyen mostrar/ocultar con nombre accesible, `aria-pressed` y
  area interactiva de `44x44`.
- Los errores `400` se presentan por campo desde `errors`; `401`, `409`, token invalido y correo no
  configurado conservan el mensaje del contrato backend.
- La prueba HTTP real confirmo registro `201`, identidad y perfil `200`, cambio de contrasena `204`,
  rechazo de la contrasena anterior `401`, login con la nueva `200`, duplicado `409`, privado sin
  token `401`, correo no configurado `503` y restablecimiento invalido `400`.
- La interfaz confirmo restauracion despues de recargar, redireccion post-login, cierre deliberado,
  perfil actualizado y cero desbordamiento o controles pequenos en las nuevas pantallas moviles.
- `npm run lint`, `npm run build` y las 18 pruebas de integracion PostgreSQL finalizan correctamente.

### Bloque 3 completado

- El catalogo y la busqueda consumen exclusivamente `GET /api/experiences` y
  `GET /api/experiences/search`; no existen arreglos locales como fuente de datos.
- Los filtros `location`, `category` y `maxPrice` se reflejan en el query string, se restauran al
  recargar y se aplican automaticamente con debounce de 450 ms.
- Cada cambio de consulta cancela la solicitud anterior mediante `AbortController` y evita que una
  respuesta obsoleta reemplace resultados mas recientes.
- Las tarjetas muestran solamente titulo, descripcion, ubicacion, categoria, precio, capacidad y
  cupos reales; el espacio visual indica honestamente que la imagen no esta disponible.
- Se agrego `/experiences/:id` con carga, error recuperable, detalle real y estado visible cuando el
  backend responde `404` para una experiencia inexistente o no aprobada.
- El regreso desde el detalle conserva la busqueda original y sus filtros compartibles.
- PostgreSQL de QA contiene tres experiencias dominicanas aprobadas para validar catalogo y detalle:
  Samana, San Francisco de Macoris y Santo Domingo.
- La validacion HTTP confirmo catalogo `200`, detalle `200`, filtros combinados `200`, filtro invalido
  `400` y detalle inexistente `404`.
- La interfaz confirmo tres resultados reales, debounce, recarga de URL, un resultado filtrado,
  detalle positivo, `404`, estado vacio y cero desbordamiento o controles pequenos en movil.
- `npm run lint`, `npm run build`, `git diff --check` y las 18 pruebas PostgreSQL finalizan
  correctamente.

### Bloque 4 completado

- El detalle de experiencia permite crear una reserva real mediante `POST /api/reservations` solo
  para usuarios autenticados; el login conserva y recupera la ruta de origen.
- El dialogo resume experiencia, precio unitario, cantidad y total antes de enviar, valida la
  disponibilidad y bloquea cierres o envios duplicados mientras la solicitud esta en curso.
- La interfaz comunica de forma explicita el estado real `Pending`; no presenta pago confirmado,
  correo enviado ni confirmacion final inexistente.
- Ante un conflicto `409`, el detalle vuelve a consultar la experiencia y actualiza los cupos
  visibles; tambien conserva los mensajes reales para `400`, `401` y errores recuperables.
- Se agregaron las rutas privadas `/reservations` y `/reservations/:id`, enlazadas desde la
  navegacion, con carga, error, vacio, estado, cantidad, total y fecha obtenidos del backend.
- El detalle no revela reservas ajenas: el backend responde `404` tanto para una reserva inexistente
  como para una que pertenece a otro usuario, y la interfaz usa un unico estado seguro.
- La prueba HTTP real creo una reserva de dos personas por USD 116 con estado `Pending`, redujo los
  cupos de 10 a 8 y confirmo su presencia en `GET /api/reservations/my` y su detalle propio.
- Una solicitud superior a los cupos respondio `409` sin modificar la disponibilidad; otro usuario
  obtuvo `404` y una solicitud anonima obtuvo `401`.
- La interfaz confirmo retorno post-login, total dinamico, validacion de cupos, listado, detalle,
  estado no disponible, menu movil y cero desbordamiento horizontal; la consola no registro errores.
- `npm run lint`, `npm run build` y las 18 pruebas de integracion PostgreSQL finalizan correctamente.

### Bloque 5 completado

- Se eliminaron los anuncios repetidos de cada skeleton y se sustituyeron las regiones vivas
  extensas por mensajes breves para carga, error y cantidad de resultados o reservas.
- Los errores de formulario conservan `aria-invalid` y referencias validas mediante
  `aria-describedby`; `Input` y `SelectField` combinan mensajes propios y descripciones externas.
- El dialogo mantiene el foco dentro de sus controles, cierra con `Escape` y devuelve el foco al
  boton que lo abrio sin reiniciar el ciclo cuando cambia el estado de envio.
- El enlace de salto enfoca explicitamente `main`, todos los controles auditados tienen nombre
  accesible y no existen indices de tabulacion positivos.
- El estilo del toast se traslado de React al CSS global y no quedan `any`, hojas `<style>`,
  excepciones de TypeScript ni desactivaciones de ESLint en `frontend/src`.
- El coral de marca se ajusto a `#E65A4B`: alcanza 3.54:1 sobre blanco y 3.8:1 sobre el footer;
  texto, primario, errores y advertencias superan 4.5:1 en sus fondos funcionales.
- Los cinco viewports `360x800`, `390x844`, `768x1024`, `1280x720` y `1440x900` tienen cero
  desbordamiento horizontal y cero controles visibles menores de `44x44` en el flujo critico.
- El reflujo equivalente a 200 % sobre 1280 px se valido a 640 px CSS sin desbordamiento; la regla
  `prefers-reduced-motion` elimina desplazamiento suave y reduce animaciones y transiciones.
- La matriz HTTP real cubrio registro, login, identidad restaurable, perfil, cambio y recuperacion
  de contrasena, catalogo, busqueda, detalle, reserva, sobrecupo, propiedad y token rechazado.
- La prueba integral creo la reserva `#40` con estado `Pending`, redujo los cupos de 14 a 13 y
  confirmo `409` sin descuento adicional, `404` para otro propietario y `401` para token rechazado.
- El bundle de produccion no contiene imagenes empaquetadas: JavaScript gzip es aproximadamente
  110.6 kB y CSS gzip 6.1 kB; no se detectaron errores de consola en el navegador.

Checklist final de la guia UX/UI:

- Tarea critica: catalogo, detalle y reserva usan exclusivamente API y PostgreSQL reales.
- Jerarquia y consistencia: shell, tokens, componentes y estados compartidos se usan en todos los
  flujos implementados.
- Retroalimentacion y prevencion: carga, error, vacio, validacion, doble envio y conflicto de cupos
  tienen respuestas visibles y accesibles.
- Accesibilidad: foco visible, salto a contenido, controles de 44 px, contraste, etiquetas,
  descripciones, regiones vivas breves y movimiento reducido fueron verificados.
- Honestidad funcional: no se muestran imagenes, reputacion, pago, correo o confirmacion que el
  backend no haya entregado.
- Adaptacion: los cinco viewports definidos y el reflujo ampliado finalizan sin desbordamiento.

### Entrega A completada - Anfitriones y moderacion

- El registro publico concede exclusivamente `Tourist`; los roles `Host` legados sin perfil aprobado
  se degradan de forma segura y un administrador no puede solicitar convertirse en anfitrion.
- PostgreSQL incorpora `host_profiles`, estados `Pending`, `Approved`, `Rejected` y `Suspended`, y
  auditoria persistente para cada decision administrativa.
- Aprobar una solicitud promueve al usuario a `Host` en el mismo `SaveChanges`; rechazar exige un
  motivo y permite reenviar la solicitud; suspender retira el rol efectivo.
- Cada experiencia tiene `HostId` obligatorio y ciclo `Draft`, `PendingReview`, `Approved`,
  `Rejected` y `Suspended`, conservando `IsApproved` solamente como compatibilidad temporal.
- Los endpoints publicos siguen mostrando unicamente experiencias aprobadas; los anfitriones solo
  consultan y modifican las propias, y un perfil suspendido queda bloqueado aunque conserve un JWT
  emitido antes de la suspension.
- El frontend agrega `/host-profile`, `/host/experiences` y `/admin/moderation`, con navegacion por
  rol, solicitud/reenvio, CRUD de borradores, envio a revision y decisiones administrativas.
- El script idempotente `003_create_host_moderation.sql` fue aplicado al PostgreSQL compartido.
- Swagger expone 28 rutas, incluidas 14 rutas nuevas de Entrega A; salud y CORS local quedaron
  verificados despues de restaurar la configuracion normal.
- Verificacion aprobada: compilacion Release sin advertencias, 22 pruebas PostgreSQL, `npm run lint`,
  `npm run build`, flujo visual turista-administrador-anfitrion y vista `390x844` sin desbordamiento
  ni controles visibles menores de 44 px.

### Entrega B completada - Calendario y reservas completas

- PostgreSQL incorpora horarios por experiencia con inicio y final UTC, capacidad, cupos disponibles,
  estados `Scheduled`, `Closed`, `Cancelled` y `Completed`, indice por experiencia/fecha y control de
  concurrencia sobre la disponibilidad.
- Los anfitriones aprobados publican, consultan, editan y eliminan horarios propios; no pueden reducir
  capacidad por debajo de los cupos reservados ni eliminar un horario con reservas.
- La disponibilidad publica filtra horarios futuros abiertos por rango y cantidad, y la busqueda admite
  `minPrice`, `maxPrice`, `from`, `to` y `quantity` sin exponer experiencias no aprobadas.
- Las reservas nuevas requieren `ScheduleId`, nacen como `PendingPayment` y mantienen `ExperienceId`
  como dato compatible; precio y experiencia se calculan en el servidor.
- Cancelar libera cupos exactamente una vez y reprogramar mueve cupos entre horarios de la misma
  experiencia dentro de una transaccion; cada cambio queda en `reservation_status_history`.
- Las escrituras de reservas exigen `Idempotency-Key`, se persisten por usuario y operacion, y rechazan
  reutilizar una clave con un cuerpo diferente.
- El anfitrion consulta solamente reservas de sus experiencias, puede cancelarlas con motivo y solo
  puede completar reservas confirmadas cuyo horario ya termino.
- El frontend agrega selector de fecha/hora en la reserva, cancelacion, reprogramacion, historial,
  calendario del anfitrion y bandeja de reservas recibidas.
- El script idempotente `005_create_schedules_and_reservation_lifecycle.sql` fue aplicado al PostgreSQL
  compartido y migro las reservas legadas a horarios historicos cerrados.
- Verificacion aprobada: compilacion Release sin advertencias, 31 pruebas PostgreSQL, `npm run lint`,
  `npm run build` y `git diff --check`.

### Entrega C completada - Pagos mock persistentes

- PostgreSQL amplia `payments` con proveedor, referencia externa, idempotencia por usuario, moneda,
  desglose (subtotal, cargo de servicio, comision de plataforma y neto del anfitrion), codigo de
  fallo, fecha de pago y monto reembolsado; un indice parcial unico garantiza un solo pago vigente
  por reserva sin bloquear los reintentos tras un rechazo.
- `payment_gateway_attempts`, `payment_webhook_events` y `refunds` persisten cada intento del
  gateway, cada evento procesado exactamente una vez y cada reembolso con motivo y actor.
- `IPaymentGateway` es el puerto neutral del dominio; `MockPaymentGateway` se registra con
  `Payments:Provider=Mock` y la aplicacion rechaza arrancar en Production con ese proveedor o con
  cualquier proveedor desconocido, por lo que `mock-confirm` y `mock-reject` jamas quedan mapeados
  fuera de Development/QA.
- `POST /api/reservations/{id}/payments` exige `Idempotency-Key`, calcula el desglose en el servidor
  y nace `Pending`; bloqueos transaccionales por clave y reserva evitan llamadas concurrentes
  duplicadas al gateway, la misma clave devuelve el mismo pago y su reutilizacion con otra reserva
  responde `409`.
- `mock-confirm` convierte el pago en `Paid` y la reserva en `Confirmed` una sola vez gracias al
  evento unico por proveedor; `mock-reject` marca `Failed` con codigo de fallo sin confirmar la
  reserva y habilita un nuevo intento; solo el dueno del pago o un administrador operan el simulador.
- `POST /api/admin/payments/{id}/refund` registra el reembolso mock con motivo, mueve la reserva a
  `Refunded` y libera los cupos exactamente una vez; un bloqueo por pago y una clave estable del
  gateway hacen que repetirlo, incluso concurrentemente, no duplique efectos.
- El detalle de reserva muestra el desglose real devuelto por el servidor y una unica accion
  `Pagar`; en el ambiente actual el pago se confirma automaticamente sin exponer al usuario el
  proveedor temporal ni controles de simulacion. El administrador conserva el formulario de
  reembolso y el cliente no solicita ni representa datos de tarjeta.
- El script idempotente `006_create_payments.sql` fue aplicado al PostgreSQL compartido.
- Verificacion aprobada: compilacion Release sin advertencias, 51 pruebas (43 PostgreSQL y 8 de
  gateway, contrato HTTP y validacion de arranque), `npm run lint`, `npm run build`,
  `git diff --check` y 30 pasos E2E HTTP reales, incluida la comprobacion de que la aplicacion no
  arranca fuera de Development/QA con el proveedor mock.

### Tareas para sustituir el gateway mock por Stripe

- Crear `StripePaymentGateway : IPaymentGateway` con PaymentIntents y registrarlo mediante
  `Payments:Provider=Stripe`; las credenciales viven en user-secrets o variables de ambiente,
  nunca en el repositorio.
- Sustituir `mock-confirm` y `mock-reject` por `POST /api/payments/webhook` con verificacion de
  firma; `payment_intent.succeeded` y `payment_intent.payment_failed` alimentan
  `payment_webhook_events` para conservar el procesamiento exactamente una vez.
- Mapear `PaymentIntent.Id` a `ProviderPaymentId` y los reembolsos de Stripe a `refunds`
  (`Refund.Id` a `ProviderRefundId`).
- Entregar al frontend el `clientSecret` del PaymentIntent y reemplazar los botones simulados por
  Stripe Payment Element; el cliente sigue sin tocar numeros de tarjeta.
- Evaluar Stripe Connect para transferir `HostNetAmount` a los anfitriones cuando exista onboarding.
- Verificar en modo prueba con `stripe listen` antes de retirar el gateway mock de QA.

### Entrega D completada - Notificaciones y resenas

- PostgreSQL incorpora `outbox_messages`, intentos por canal, notificaciones persistentes,
  preferencias, dispositivos, auditoria de capacidad y resenas verificadas; el script idempotente
  `007_create_notifications_and_reviews.sql` fue aplicado al ambiente compartido.
- Crear, reprogramar, cancelar y completar reservas, confirmar pagos y registrar reembolsos agregan
  sus eventos al outbox dentro del mismo `SaveChanges` que modifica el negocio.
- Un servicio en segundo plano reclama mensajes de forma atomica, recupera leases vencidos y aplica
  hasta ocho intentos con espera exponencial. Dashboard, correo y push registran sus intentos por
  separado para no repetir un canal configurado ya completado durante un reintento.
- Correo transaccional reutiliza el proveedor SMTP/Resend configurado. Push usa el protocolo Web
  Push estandar con VAPID, sin SDK ni proyecto de Firebase. Las suscripciones guardan `endpoint`,
  `p256dh` y `auth`, y el service worker muestra la notificacion y valida el destino antes de abrirlo.
- Desarrollo local tiene `WebPush:Subject`, `WebPush:PublicKey` y `WebPush:PrivateKey` en
  `dotnet user-secrets`; QA y produccion deben configurar su propio par VAPID. El script idempotente
  `008_create_web_push_subscriptions.sql` fue aplicado al PostgreSQL compartido y retiro la tabla
  legada de tokens Firebase, cuyos valores no eran convertibles al protocolo Web Push.
- Las claves VAPID se generan una sola vez con `VapidHelper.GenerateVapidKeys`; la publica puede
  entregarse al navegador y la privada vive exclusivamente en user-secrets o variables de ambiente.
  Rotarlas exige que los navegadores se suscriban nuevamente.
- El cliente agrega `/notifications`, lectura por usuario y preferencias de bandeja, correo y push;
  la ruta es privada y conserva los estados accesibles de carga, vacio, error y exito.
- Una resena solo nace desde una reserva `Completed`, es unica por reserva y editable por su autor
  durante 30 dias. Eliminar conserva el registro como `Deleted`; ocultar exige motivo y crea
  auditoria administrativa. Las consultas publicas y los agregados incluyen solo `Visible`.
- El detalle de experiencia muestra promedio, cantidad y comentarios verificados; el detalle de una
  reserva completada permite crear, editar y eliminar la resena propia.
- Verificacion aprobada: compilacion sin advertencias, 65 pruebas .NET/PostgreSQL, contrato VAPID
  y alta/baja de suscripcion contra PostgreSQL, `npm run lint`, `npm run build`, `git diff --check`
  y navegacion local sin errores de consola. La activacion y el estado activo de los avisos fueron
  confirmados visualmente en un navegador con permisos habilitados.

### Entrega E completada - Mapas y panel del anfitrion

- PostgreSQL incorpora coordenadas opcionales y validadas para cada experiencia mediante
  `009_add_experience_locations.sql`, aplicado al ambiente compartido. Las experiencias existentes
  permanecen validas sin inventar una ubicacion; el anfitrion puede senalarla al editar.
- `GET /api/experiences/nearby` recibe un punto y un radio de 1 a 300 km, limita candidatos en
  PostgreSQL y calcula la distancia Haversine antes de devolver solo experiencias aprobadas,
  ordenadas de menor a mayor distancia.
- `GET /api/host/dashboard` exige un anfitrion aprobado y agrega exclusivamente sus experiencias,
  proximos horarios, reservas confirmadas, personas reservadas, experiencias completadas,
  ingresos netos, calificacion y resenas visibles.
- El frontend agrega `/experiences/map`, busqueda a menos de 50 km con permiso explicito del
  dispositivo, mapa en el detalle cuando existe ubicacion y selector de punto para el anfitrion.
- `/host/dashboard` presenta seis metricas reales y las proximas cinco fechas, con enlaces directos
  a su calendario. Ninguna cifra se calcula con datos locales ni se comparte entre anfitriones.
- El mapa usa Leaflet y teselas de OpenStreetMap con atribucion visible, sin cuentas, claves ni
  servicios de pago. Su codigo se carga solamente al abrir una pantalla que lo necesita.
- Verificacion aprobada: migracion aplicada, compilacion .NET sin advertencias, 68 pruebas
  .NET/PostgreSQL, `npm run lint`, `npm run build` y paquete principal por debajo de 500 kB.

## Fuente de diseno analizada

Este plan aplica la guia `Guia_Diseno_UX_UI_Prototipos_IA_Julissa_Mateo_Abad.pdf`, revisada en
sus 13 paginas, al producto GoIsland y a los contratos que el backend ofrece actualmente.

Principios tomados de la guia:

- Disenar alrededor de la tarea critica y no de una plantilla generica.
- Definir usuario, contexto, dispositivo, personalidad, arquitectura y estados antes de decorar.
- Usar jerarquia, consistencia, retroalimentacion, prevencion de errores y control del usuario.
- Asignar una funcion a cada color y mantener contraste suficiente.
- Para turismo: usar turquesa, arena, coral y azul oceano con fotografia autentica del destino.
- Para marketplace: destacar busqueda, disponibilidad, confianza, comparacion y reputacion.
- Para reservas: hacer central la disponibilidad/calendario cuando el backend lo permita.
- Disenar mobile-first, con acciones al alcance del pulgar y navegacion simple.
- Incluir estados vacio, carga, error, exito, sin conexion y permisos insuficientes.
- Validar teclado, foco visible, tamanos tactiles, contraste e independencia del color.

## Restricciones del plan

- La ejecucion comenzo por el Bloque 0 y avanzara en entregas verticales verificables.
- Los datos funcionales deben proceder del backend y PostgreSQL.
- No se permitiran experiencias, calificaciones, resenas, favoritos ni confirmaciones inventadas.
- El proveedor temporal de pagos permanece como detalle interno; la interfaz presenta el flujo
  final con una unica accion `Pagar`.
- Una pantalla no se considerara integrada si solamente se ve bien con datos locales.
- Las mejoras futuras que requieran endpoints inexistentes se mostraran como bloqueadas, no como
  funcionalidades aparentes.

## Auditoria del frontend actual

### Hallazgos funcionales

| Hallazgo | Estado actual | Accion obligatoria |
|---|---|---|
| Catalogo | Usa `experiencesMock.json` y demora artificial | Consumir `GET /api/experiences` y eliminar el JSON |
| Busqueda | Filtra localmente titulo, descripcion y multiples categorias | Ajustarse a `location`, una `category` y `maxPrice`, o ampliar primero el backend |
| Reserva | Muestra "Reserva confirmada" sin llamar la API | Usar `POST /api/reservations` y mostrar el estado real `Pending` |
| Destacadas | Depende de un campo `featured` inexistente en backend | Eliminar la seccion o renombrarla con un criterio real verificable |
| Reputacion | Muestra `rating` y `reviewCount` inventados | Ocultarlos hasta implementar el modulo de resenas |
| Favoritos | Solo vive en `localStorage` | Retirar del flujo principal hasta disponer de endpoints persistentes |
| Fotografias | Usa imagenes genericas de destinos fuera de RD | Usar placeholder honesto hasta que backend entregue imagenes; luego fotografia dominicana autentica |
| Registro Host | Concede la apariencia de anfitrion directo | Registrar turista por defecto y esperar el flujo de validacion de anfitrion |
| Sesion | JWT solamente en memoria; se pierde al recargar | Definir persistencia temporal segura y restaurar identidad con `/auth/me` |
| Recuperacion | Backend existe, pantallas no | Crear solicitar, restablecer y cambiar contrasena |
| Reservas del usuario | Backend existe, pantalla no | Crear listado y detalle de reservas reales |

### Hallazgos UX/UI y accesibilidad

- La pagina inicial todavia sigue el patron generico de hero, buscador y tarjetas repetidas.
- El mensaje habla de islas de todo el mundo, mientras el proyecto esta enfocado en Republica
  Dominicana.
- En un viewport de 390 px existe desbordamiento horizontal y el `h1` conserva 48 px.
- El navbar no cambia a menu movil y comprime logo, enlaces y botones en una sola fila.
- Existen controles interactivos menores de 44 x 44 px.
- Varias hojas `<style>` se renderizan dentro de botones o enlaces; su contenido termina formando
  parte del nombre accesible del control.
- `Input` genera identificadores con `Math.random()` durante el render, provocando IDs inestables.
- Existen enlaces `href="#"` que aparentan navegar pero no llevan a contenido real.
- Hay variables CSS usadas pero no definidas, como `--text-secondary`, `--text-primary` y
  `--text-muted`.
- Hay demasiados estilos inline, lo que dificulta consistencia, responsive y estados de foco.
- El pie de pagina ocupa demasiado espacio para los pocos flujos reales disponibles.
- El diseño usa imagenes remotas de Unsplash sin control de disponibilidad ni coherencia local.

### Estado tecnico medido

- `npm run build`: aprobado.
- `npm run lint`: falla actualmente con 8 errores y 2 advertencias.
- Los errores incluyen pureza de React, tipos `any`, efectos con actualizaciones encadenadas y
  separacion incorrecta de exports para Fast Refresh.

## Ficha UX de GoIsland

| Pregunta | Decision para GoIsland |
|---|---|
| Usuario principal | Turista nacional o extranjero que quiere descubrir y reservar experiencias locales confiables |
| Contexto | Telefono movil, frecuentemente durante un viaje, con luz exterior y conectividad variable |
| Tarea critica | Encontrar una experiencia disponible y crear una reserva sin confundir precio, cupos o estado |
| Error grave | Mostrar disponibilidad falsa, duplicar una reserva o afirmar que esta confirmada sin estarlo |
| Personalidad | Dominicana, cercana, confiable, viva y organizada; no generica ni excesivamente premium |
| Dispositivo prioritario | Mobile-first desde 360 px, seguido de tablet y escritorio |
| Confianza | Precio claro, cupos reales, estado visible, anfitrion y reputacion cuando existan en backend |

## Direccion visual propuesta

### Estilo

Combinar **editorial turistico + marketplace mobile-first**:

- Fotografia y contexto dominicano como entrada visual.
- Busqueda y disponibilidad como acciones principales.
- Tarjetas mas simples, informativas y comparables.
- Superficies limpias; evitar llamar `glass` a paneles que realmente son tarjetas blancas.
- Menos hero decorativo en movil y mas espacio para descubrir rapidamente.
- Dashboard operativo reservado para anfitrion y administrador en fases futuras.

### Sistema de color propuesto

| Token | Color inicial | Funcion |
|---|---|---|
| `--color-ocean-700` | `#075985` | Marca, navegacion y texto de accion |
| `--color-turquoise-600` | `#0F766E` | Accion primaria y seleccion |
| `--color-sand-50` | `#FFF8ED` | Fondo calido y secciones editoriales |
| `--color-coral-500` | `#F26B5B` | Acentos puntuales, no errores |
| `--color-ink-900` | `#16323F` | Texto principal |
| `--color-slate-600` | `#5D6E75` | Texto secundario con contraste |
| `--color-border` | `#D9E4E5` | Bordes y divisores |
| `--color-success` | `#15803D` | Exito y disponibilidad confirmada |
| `--color-warning` | `#B45309` | Pocos cupos o accion sensible |
| `--color-error` | `#B42318` | Error y cancelacion destructiva |

La composicion seguira aproximadamente 60% neutros/arena, 30% oceano/turquesa y 10% coral u
otros acentos. Ningun estado dependera solamente del color.

### Tipografia, iconografia e imagenes

- Usar Manrope como familia principal para interfaz y contenido.
- Si se desea una voz editorial, usar una serif solamente en titulares promocionales, nunca en
  formularios o datos operativos.
- Mantener una sola familia de iconos: Lucide.
- Sustituir SVGs manuales duplicados por iconos de la familia seleccionada.
- Tamano minimo de 16 px para contenido esencial y 14 px para ayuda secundaria.
- Usar fotografias reales de Republica Dominicana con tratamiento consistente.
- Mientras el backend no tenga imagenes, mostrar un placeholder de categoria claramente neutral;
  no asignar una fotografia externa como si perteneciera a la experiencia.

## Arquitectura de informacion inmediata

### Publica

```text
/experiences                 Explorar y buscar
/experiences/:id             Detalle real de una experiencia
/login                       Iniciar sesion
/register                    Registro de turista
/forgot-password             Solicitar recuperacion
/reset-password              Restablecer contrasena
```

### Turista autenticado

```text
/reservations                Mis reservas
/reservations/:id            Detalle y estado de reserva
/profile                     Perfil
/profile/security            Cambio de contrasena
```

### Diferidas hasta que exista backend

- Favoritos persistentes.
- Resenas y calificaciones.
- Mapa y cercania.
- Calendario por horario.
- Panel de anfitrion.
- Moderacion administrativa.
- Notificaciones persistentes.

## Matriz del backend disponible actualmente

| Flujo | Endpoint real | Pantalla frontend |
|---|---|---|
| Salud | `GET /api/health` | Diagnostico, no navegacion principal |
| Registro | `POST /api/auth/register` | Registro de turista |
| Login | `POST /api/auth/login` | Inicio de sesion |
| Identidad | `GET /api/auth/me` | Restauracion de sesion y perfil |
| Perfil | `PUT /api/users/profile` | Editar nombre |
| Cambiar clave | `PUT /api/auth/change-password` | Seguridad de cuenta |
| Solicitar recuperacion | `POST /api/auth/forgot-password` | Olvide mi contrasena |
| Restablecer clave | `POST /api/auth/reset-password` | Nueva contrasena por token |
| Catalogo | `GET /api/experiences` | Explorar experiencias aprobadas |
| Detalle | `GET /api/experiences/{id}` | Detalle de experiencia |
| Busqueda | `GET /api/experiences/search` | Filtros por ubicacion, categoria y precio maximo |
| Crear experiencia | `POST /api/experiences` | Diferido hasta completar propiedad/aprobacion |
| Crear reserva | `POST /api/reservations` | Formulario de cantidad y confirmacion del estado real |
| Mis reservas | `GET /api/reservations/my` | Listado privado |
| Detalle reserva | `GET /api/reservations/{id}` | Detalle privado |

## Decision recomendada

No esperar a terminar todo el backend. La estrategia recomendada es integrar ahora los flujos que
ya existen y continuar por entregas verticales pequenas.

Esto permite comprobar temprano autenticacion, formatos JSON, validaciones, CORS, estados HTTP y
necesidades reales del cliente sin bloquear el desarrollo de los modulos pendientes.

Este documento es solamente un plan de coordinacion. No autoriza modificaciones del frontend por
parte del responsable de backend.

## Principios de integracion

- Swagger/OpenAPI sera el contrato principal entre los equipos.
- El frontend nunca accedera directamente a PostgreSQL.
- La URL de la API se configurara por ambiente.
- Los errores conservaran una estructura coherente: `message` y, cuando aplique, `errors`.
- Los contratos ya integrados no se cambiaran sin versionarlos o coordinar la migracion.
- Cada entrega debe tener criterios de aceptacion verificables desde Swagger y desde el frontend.
- Los datos funcionales siempre procederan del backend y PostgreSQL.
- El gateway temporal de pagos solo se usa como detalle interno del ambiente; la interfaz presenta
  el flujo final de pago y nunca expone controles de simulacion.

## Bloque 0 - Congelar contrato y eliminar comportamientos falsos

### Trabajo backend

- Confirmar que la solucion compila y que las pruebas PostgreSQL pasan.
- Reiniciar la API con el binario actual.
- Revisar que Swagger muestre autenticacion Bearer y todos los DTOs.
- Mantener CORS configurable para la URL local del frontend.
- Documentar ejemplos positivos y negativos en `GoIsland.Api.http`.
- Definir una lista de endpoints estables para la primera integracion.

### Trabajo frontend

- Eliminar `experiencesMock.json` y la demora artificial de `experienceService`.
- Eliminar el toast que afirma una reserva confirmada sin crearla.
- Retirar `featured`, `rating` y `reviewCount` del contrato actual.
- Ocultar Favoritos de la navegacion hasta que exista persistencia backend.
- Retirar enlaces con `href="#"` o reemplazarlos por contenido real.
- Crear tipos TypeScript que reflejen exactamente los DTOs del backend.
- Definir `ApiError` y una funcion unica para traducir `message` y `errors`.

### Entregable al equipo frontend

- URL local o de QA de la API.
- URL de Swagger.
- Lista de variables de entorno requeridas por el cliente.
- Credenciales de usuarios de QA creados en PostgreSQL de QA, nunca en el repositorio.
- Tabla de endpoints, roles, request, response y errores esperados.

### Criterios de terminado

- Frontend puede alcanzar `/api/health`.
- CORS permite solamente el origen configurado.
- Swagger representa el comportamiento real de la API.
- Ninguna pantalla depende de JSON local o estados inventados.
- No existe una accion que anuncie exito sin respuesta positiva del backend.

---

## Bloque 1 - Sistema visual, shell responsive y componentes base

### Objetivo

Aplicar una identidad propia antes de extender mas pantallas.

### Trabajo frontend

- Reemplazar los tokens actuales por el sistema oceano, turquesa, arena, coral y semanticos.
- Mover estilos inline repetidos y todos los `<style>` internos a hojas o modulos de estilo.
- Renombrar `glass-card` y `glass-panel` segun su funcion real.
- Unificar tipografia, escala, espaciado, radio, sombras y anchos de contenido.
- Corregir `Input` usando `useId()` y asociaciones label/control estables.
- Definir componentes base:
  - `Button`
  - `TextField`
  - `SelectField`
  - `PriceField`
  - `StatusBadge`
  - `Alert`
  - `Skeleton`
  - `EmptyState`
  - `ErrorState`
  - `Dialog`
  - `Pagination` cuando el backend la soporte
- Crear un navbar mobile-first con menu accesible o navegacion inferior para las tareas principales.
- Reducir el hero en movil y priorizar busqueda/resultados dentro del primer viewport.
- Simplificar el footer y mostrar solamente enlaces funcionales.
- Agregar `Skip to content`, foco visible y soporte de `prefers-reduced-motion`.

### Breakpoints de verificacion

```text
360 x 800   telefono pequeno
390 x 844   telefono comun
768 x 1024  tablet
1280 x 720  escritorio
1440 x 900  escritorio amplio
```

### Criterios de terminado

- No existe desplazamiento horizontal en ningun breakpoint.
- Todo control tactil principal mide al menos 44 x 44 px.
- Los nombres accesibles de botones y enlaces contienen solamente su etiqueta util.
- Paleta, tipografia, iconos y estados son consistentes.
- La tarea primaria se identifica en menos de cinco segundos.

---

## Bloque 2 - Autenticacion, sesion y cuenta

### Endpoints a integrar

```text
POST /api/auth/register
POST /api/auth/login
GET  /api/auth/me
PUT  /api/users/profile
PUT  /api/auth/change-password
POST /api/auth/forgot-password
POST /api/auth/reset-password
```

### Contrato que debe entregar backend

- Registro e inicio de sesion devuelven JWT, expiracion y usuario.
- Rutas privadas responden `401` sin token valido.
- Validaciones responden `400` con errores por campo.
- Email duplicado responde `409`.
- Recuperacion mantiene una respuesta que no revela si el correo existe.
- `forgot-password` queda listo cuando termine el Bloque 7 de Resend.
- El frontend registra inicialmente turistas; la solicitud de anfitrion se incorporara con el
  bloque backend correspondiente.
- Se acuerda una estrategia temporal de sesion compatible con el JWT actual.

### Trabajo esperado del frontend

- Formularios de registro, login, perfil y contrasena.
- Almacenamiento y envio controlado del Bearer token.
- Restauracion de identidad mediante `GET /api/auth/me` al recargar.
- Manejo global de `401` y expiracion de sesion.
- Presentacion de errores de validacion sin inventar mensajes distintos al contrato.
- Pantallas separadas para solicitar recuperacion, restablecer por token y cambiar contrasena.
- Redireccion a la ruta originalmente solicitada despues del login.
- Indicadores de mostrar/ocultar contrasena con nombre accesible.

### Criterios de aceptacion compartidos

- Un usuario se registra, inicia sesion y consulta su identidad real.
- Actualizar perfil modifica PostgreSQL y se refleja al volver a consultar.
- Cambiar o restablecer contrasena invalida la anterior.
- Recargar el navegador conserva o cierra la sesion de forma deliberada, nunca accidental.
- Se verifican carga, error, exito, token expirado y servicio de correo no configurado.

---

## Bloque 3 - Catalogo, busqueda y detalle reales

### Endpoints a integrar

```text
GET /api/experiences
GET /api/experiences/{id}
GET /api/experiences/search?location=&category=&maxPrice=
```

### Contrato que debe entregar backend

- Solo se devuelven experiencias aprobadas.
- Una experiencia inexistente o no aprobada responde `404`.
- Los filtros son opcionales y combinables.
- La respuesta contiene precio y cupos disponibles provenientes de PostgreSQL.
- El backend no entrega actualmente imagen, calificacion, resenas ni condicion destacada.

### Trabajo esperado del frontend

- Listado de experiencias.
- Vista de detalle.
- Formulario de busqueda ajustado al contrato: ubicacion, una categoria y precio maximo.
- Estados de carga, vacio y error.
- Debounce y cancelacion de la solicitud anterior al cambiar filtros.
- URL con query string para que una busqueda pueda recargarse o compartirse.
- Placeholder visual honesto cuando no exista imagen.
- Precio, ubicacion, categoria y cupos con jerarquia clara.
- Ocultar calificacion, reputacion y fotografias especificas mientras el backend no las provea.
- Redactar contenido y ejemplos para Republica Dominicana.

### Criterios de aceptacion compartidos

- Crear o actualizar datos en PostgreSQL cambia los resultados del catalogo.
- Los filtros mostrados coinciden con la respuesta real del backend.
- No existen arreglos locales de experiencias como fuente de datos.
- No existen campos presentados que el backend no pueda respaldar.
- El detalle devuelve `404` visible y recuperable cuando la experiencia no existe.

---

## Bloque 4 - Creacion y consulta de reservas reales

### Endpoints a integrar

```text
POST /api/reservations
GET  /api/reservations/my
GET  /api/reservations/{id}
```

### Contrato que debe entregar backend

- Crear una reserva requiere JWT.
- Cupos insuficientes responden `409`.
- La reserva y el descuento de cupos son atomicos.
- Un usuario no puede consultar reservas ajenas.
- La respuesta identifica claramente el estado `Pending` actual.

### Trabajo esperado del frontend

- Vista o dialogo de reserva con seleccion de cantidad.
- Resumen de experiencia, precio por persona, cantidad y total estimado.
- Confirmacion previa que explique que la reserva quedara `Pending`, no pagada ni confirmada.
- Listado de reservas del usuario.
- Detalle y estado de cada reserva.
- Navegacion directa a `/reservations/:id` despues de una creacion exitosa.
- Tratamiento especifico de `400`, `401`, `404` y `409`.
- Deshabilitar doble envio y conservar idempotencia visual mientras el backend completa su soporte.
- Actualizar cupos visibles despues de reservar.

### Criterios de aceptacion compartidos

- La reserva aparece en PostgreSQL y en `GET /api/reservations/my`.
- Los cupos disminuyen y una sobreventa concurrente es rechazada.
- La interfaz muestra el estado devuelto, no un estado calculado localmente.
- Nunca se muestra correo enviado o pago confirmado si esos efectos no ocurrieron.
- Reintentar despues de un `409` vuelve a consultar disponibilidad.

---

## Bloque 5 - Accesibilidad, calidad y validacion integral

### Accesibilidad

- Navegar todos los flujos usando solamente teclado.
- Verificar orden de foco y retorno del foco al cerrar dialogos.
- Asociar errores con campos mediante `aria-describedby`.
- Usar `aria-live` para resultados asincronos importantes sin duplicar mensajes.
- Asegurar contraste WCAG AA para texto y controles.
- Acompanar estados con texto o icono, no solamente color.
- Verificar zoom al 200% y texto aumentado.
- Probar con movimiento reducido.

### Calidad tecnica

- Hacer que `npm run lint` termine sin errores ni advertencias.
- Mantener `npm run build` aprobado.
- Eliminar `any` en manejo de errores y DTOs.
- Separar providers, hooks y constantes para Fast Refresh.
- Evitar efectos que disparen cadenas de actualizaciones innecesarias.
- Centralizar estilos y eliminar CSS duplicado dentro de componentes.
- Revisar peso del bundle e imagenes.

### Pruebas de integracion

- Ejecutar pruebas end-to-end contra la API real y PostgreSQL de QA.
- No usar MSW, JSON local ni una API falsa para los criterios de aceptacion.
- Probar al menos:
  1. Registro y login.
  2. Restauracion de sesion.
  3. Actualizacion de perfil.
  4. Cambio y recuperacion de contrasena.
  5. Catalogo y busqueda.
  6. Detalle de experiencia.
  7. Reserva exitosa.
  8. Cupos insuficientes.
  9. Mis reservas y reserva ajena.
  10. Sesion expirada.

### Criterios de terminado

- Build y lint aprobados.
- Cero desbordamiento horizontal en los cinco viewports definidos.
- Flujos principales completables con teclado.
- Estados reales verificables en PostgreSQL.
- Checklist final de la guia UX/UI respondido con evidencia.

---

## Bloque 6 - Entregas verticales futuras

A partir de aqui, cada bloque backend debe integrarse antes de comenzar demasiados bloques nuevos.

### Entrega A - Anfitriones y moderacion

- Backend: bloques 8 y 9.
- Integrar solicitud de anfitrion, experiencias propias y aprobacion administrativa.
- No avanzar al calendario hasta validar permisos y propiedad desde los tres roles.

### Entrega B - Calendario y reservas completas

- Backend: bloques 10 y 11.
- Integrar horarios, disponibilidad, cancelacion y reprogramacion.
- Sustituir la reserva por experiencia por una reserva asociada a un horario.

### Entrega C - Pagos mock persistentes

- Backend: bloque 12.
- Integrar creacion de pago y consulta de estado usando el contrato neutral del gateway.
- Presentar una unica accion `Pagar` y completar automaticamente el pago en el ambiente actual.
- No recolectar ni representar numeros reales de tarjeta.
- Preparar el cliente para consultar estados sin asumir exito inmediato.

### Entrega D - Notificaciones y resenas

- Backend: bloques 13 y 14.
- Integrar bandeja de notificaciones, preferencias y resena posterior a una reserva completada.

### Entrega E - Mapas y dashboard

- Backend: bloques 15 y 16.
- Integrar cercania, ubicacion, rutas acordadas y metricas del anfitrion.
- Estado: completada mediante ubicaciones persistentes, cercania publica y panel privado real.

## Compatibilidad de contratos

Antes de cambiar un endpoint ya integrado:

1. Documentar el cambio propuesto.
2. Identificar pantallas consumidoras.
3. Mantener temporalmente campos compatibles cuando sea seguro.
4. Agregar pruebas del nuevo contrato.
5. Coordinar una misma entrega entre backend y frontend.
6. Retirar el contrato anterior solo cuando el cliente ya no lo consuma.

Los cambios grandes, como pasar de reserva por `ExperienceId` a reserva por `ScheduleId`, deben
planificarse como una version coordinada y no introducirse silenciosamente.

## Lista minima para cada entrega al frontend

- Endpoints y metodos HTTP.
- Autenticacion y roles requeridos.
- DTO de entrada con validaciones.
- DTO de respuesta.
- Estados HTTP positivos y negativos.
- Ejemplos reales de Swagger.
- Script SQL aplicado en el ambiente compartido.
- Pruebas PostgreSQL aprobadas.
- Limitaciones conocidas.
- Fecha a partir de la cual el contrato se considera estable.

## Orden inmediato recomendado

1. Configurar las credenciales de Google y del proveedor de correo en cada ambiente.
2. Configurar en QA y produccion las credenciales propias para los avisos a dispositivos.
3. Definir la siguiente entrega funcional despues de mapas y panel del anfitrion.
4. Mantener cada entrega vertical verificada antes de acumular la siguiente.

Este enfoque evita tanto el extremo de detener el backend por completo como el de terminar todos
los modulos sin haber comprobado que el frontend puede consumir correctamente sus contratos.
