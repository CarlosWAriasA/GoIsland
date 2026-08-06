# Plan de preparación del catálogo y despliegue de GoIsland

## Estado

Plan aprobado y en ejecución.

Avance al 1 de agosto de 2026:

- Fase 0: Azure elegido como primer destino y base autorizada para migración inicial sin respaldo
  por no contener datos importantes; cuentas y URLs públicas todavía pendientes.
- Fase 1: implementada; la imagen Docker fue construida y sus endpoints de vida y disponibilidad
  respondieron correctamente contra Neon.
- Fase 2: núcleo implementado y migración `012` aplicada en Neon; prueba real de carga pendiente
  de credenciales de Cloudinary. No hay imágenes existentes que trasladar.
- Fase 3: implementada y migración `013` aplicada en Neon; contrato completo, itinerario,
  validación de revisión y presentación pública disponibles.
- Fase 4: núcleo implementado; catálogo, cercanía, publicaciones del anfitrión, reservas,
  moderación, reseñas y notificaciones usan el contrato paginado común. La búsqueda y los filtros
  se ejecutan en servidor, las pantallas principales conservan filtros y página en la URL y
  cancelan solicitudes anteriores. La migración de índices `014` fue aplicada a Neon el 4 de
  agosto de 2026; queda pendiente la prueba con un catálogo amplio.
- Fase 5: implementada; el anfitrión puede generar horarios recurrentes con vista previa y
  exclusiones, copiar semanas y cerrar o ajustar la capacidad de varias fechas. Las operaciones
  son idempotentes, respetan los cupos reservados y convierten la hora local a UTC. La migración
  de unicidad `015` fue aplicada a Neon el 4 de agosto de 2026.
- Fase 6: implementada; las reservas de pago vencen después de 15 minutos, liberan los cupos una
  sola vez y rechazan pagos tardíos. La reconciliación se ejecuta al arrancar, periódicamente y
  antes de consultar disponibilidad o pagar. La migración `016` fue aplicada a Neon el 4 de
  agosto de 2026.
- Fase 7: implementada; las experiencias aprobadas usan URLs públicas con `slug`, conservan
  compatibilidad con IDs y publican metadatos, imagen social y datos estructurados basados en el
  catálogo real. El build genera `robots.txt`, páginas sociales y un sitemap filtrado desde la API
  pública. También están disponibles contacto, privacidad, términos y cancelaciones con alcance
  de prototipo universitario.
- Fase 8: implementada en código; PaymentIntent, Payment Element, webhooks firmados, reembolsos e
  idempotencia están integrados y las claves live se rechazan. Queda pendiente activar las
  credenciales del Sandbox y ejecutar la prueba externa de extremo a extremo.
- Fase 9: implementada en código y documentación; logs JSON, correlation ID, errores uniformes,
  readiness PostgreSQL, CI repetible y runbook de respaldo/restauración están listos. Quedan como
  validaciones externas la rotación en proveedores, el simulacro con Neon y las pruebas sobre las
  URLs públicas en móvil y escritorio.

Decisiones confirmadas:

- Carga manual de experiencias; no se construirá importador masivo.
- Frontend en Vercel.
- Backend portable entre Render y Azure mediante Docker.
- PostgreSQL actual en Neon, reutilizado como base administrada.
- Imágenes en Cloudinary.
- Pagos públicos de demostración mediante Stripe Sandbox.
- Gateway Mock conservado para pruebas automatizadas y desarrollo.
- Stripe Connect y pagos live fuera del alcance universitario.

## Propósito

Preparar GoIsland para cargar manualmente un catálogo amplio de experiencias y presentar el
proyecto universitario desde internet sin depender del disco local del servidor ni de servicios
comerciales innecesarios.

Este plan no incluye un importador masivo. Las experiencias se crearán manualmente mediante el
flujo de anfitrión y moderación existente.

## Decisiones de alcance

- GoIsland se desplegará inicialmente como demostración universitaria, no como marketplace
  comercial en producción.
- El frontend se publicará en Vercel.
- Cloudinary reemplazará el almacenamiento local de imágenes antes de cargar el catálogo
  definitivo.
- Los pagos reales y las transferencias a anfitriones quedan fuera del alcance inicial.
- Stripe Sandbox se integrará con PaymentIntents, Payment Element, webhooks firmados y tarjetas de
  prueba, sin mover dinero real.
- El backend se empaquetará una sola vez como contenedor portable para poder desplegar exactamente
  el mismo artefacto en Render o Azure.
- Se conservarán PostgreSQL, el flujo de aprobación, las reservas, las notificaciones y las
  reseñas ya implementadas.
- No se añadirán todavía favoritos, recomendaciones, cupones, chat ni programas de fidelidad.

## Arquitectura recomendada para la demostración

```text
Navegador
   |
   +-- Frontend React/Vite -------- Vercel
   |
   +-- API ASP.NET Core ----------- Contenedor Docker
           |                         +-- Render Web Service, o
           |                         +-- Azure Container Apps/App Service
           |
           +-- PostgreSQL --------- Neon
           +-- Imágenes ----------- Cloudinary
           +-- Correos ------------ Resend
           +-- Mapas -------------- Google Maps
           +-- Pagos de prueba ---- Stripe Sandbox
```

### Frontend: Vercel

Vercel soporta Vite directamente. Se configurará:

- Directorio raíz: `frontend`.
- Comando de build: `npm run build`.
- Directorio de salida: `dist`.
- `VITE_API_URL` con la URL pública del backend.
- `VITE_GOOGLE_CLIENT_ID` y las variables públicas que correspondan.
- Reescritura de rutas hacia `index.html` para que funcionen enlaces como
  `/experiences/123` al recargar.

### Backend portable: Render o Azure

El código funcional no dependerá de ninguno de los dos proveedores. Se construirá una sola imagen
Docker que:

- escuche un puerto configurable;
- reciba toda la configuración mediante variables de ambiente;
- no escriba datos funcionales en el disco local;
- exponga health y readiness;
- pueda ejecutarse localmente, en Render y en Azure;
- use los mismos servicios externos de PostgreSQL, Cloudinary, Resend y Stripe.

Se mantendrán dos perfiles de despliegue documentados. Elegir uno no obligará a modificar pagos,
imágenes, reservas ni contratos HTTP.

### Perfil A: Render

Render permite ejecutar ASP.NET Core mediante Docker y es la opción más sencilla para una
demostración.

Limitaciones aceptadas:

- El servicio gratuito se duerme después de 15 minutos sin tráfico.
- La primera solicitud puede tardar aproximadamente un minuto.
- El sistema de archivos es efímero y no puede conservar imágenes.
- No se usará Render Postgres gratuito porque sus bases gratuitas expiran después de 30 días.
- Los procesos en segundo plano solo trabajan mientras la instancia está activa.

Render recibirá la misma imagen Docker y definirá el puerto, las variables y el health check desde
su panel.

### Perfil B: Azure for Students

Si se dispone de correo universitario elegible, Azure for Students ofrece crédito para doce meses
sin exigir tarjeta. Puede alojarse el backend en Azure App Service o Azure Container Apps.

Ventajas:

- Entorno más cercano a una implementación empresarial de .NET.
- Crédito estudiantil para evitar depender exclusivamente de una instancia gratuita pequeña.
- Mejor opción si el tiempo de arranque de Render afecta la presentación.

Desventajas:

- Configuración y control de costos más complejos.
- El crédito tiene duración limitada.

La selección final se hará después de probar el contenedor. Render será la ruta más sencilla;
Azure será preferible si se quiere evitar una espera larga durante la presentación o demostrar el
uso del ecosistema Microsoft.

### PostgreSQL: Neon

La base actual ya está alojada en Neon y se conservará:

- PostgreSQL administrado.
- Plan gratuito sin vencimiento fijo.
- Conexiones SSL y pooling.
- Capacidad suficiente para una demostración universitaria.

No se creará otra base ni se migrarán los datos existentes. Se comprobarán el límite del plan, la
conexión SSL, el pooling y la ejecución controlada de los scripts pendientes. Antes de cambios de
esquema o de la carga definitiva se realizará una copia de seguridad exportable.

### Imágenes: Cloudinary

Cloudinary reemplazará `wwwroot/uploads`:

- El backend subirá cada imagen usando credenciales privadas.
- PostgreSQL guardará `PublicId`, URL segura, ancho, alto, formato, orden y texto alternativo.
- El frontend recibirá URLs optimizadas para tarjeta y detalle.
- Al eliminar una imagen se eliminarán tanto el registro como el recurso remoto.
- La clave secreta nunca llegará al navegador.

El plan gratuito incluye almacenamiento, transformaciones y CDN suficientes para el catálogo de
una demostración, siempre que las imágenes se optimicen.

## Matriz de configuración

| Destino | Variable | Uso |
|---|---|---|
| Vercel | `VITE_API_URL` | URL pública de la API |
| Vercel | `VITE_SITE_URL` | Origen público para canonical, sitemap y enlaces sociales |
| Vercel | `VITE_GOOGLE_CLIENT_ID` | Inicio de sesión con Google |
| Vercel | `VITE_STRIPE_PUBLISHABLE_KEY` | Stripe Sandbox |
| Backend | `ConnectionStrings__DefaultConnection` | PostgreSQL |
| Backend | `Jwt__Key` | Firma de sesiones |
| Backend | `Cors__FrontendUrl` | Origen final de Vercel |
| Backend | `Payments__Provider` | `Stripe` en la demostración desplegada |
| Backend | `Payments__Mode` | `Sandbox` obligatorio en el proyecto |
| Backend | `GoogleAuth__ClientId` | Validación de acceso con Google |
| Backend | `GoogleMaps__ApiKey` | Servicios de mapas configurados |
| Backend | `Resend__ApiKey` | Correos |
| Backend | `WebPush__PublicKey` | Avisos web |
| Backend | `WebPush__PrivateKey` | Firma privada de avisos |
| Backend | `Cloudinary__CloudName` | Cuenta de imágenes |
| Backend | `Cloudinary__ApiKey` | Carga de imágenes |
| Backend | `Cloudinary__ApiSecret` | Secreto de imágenes |
| Backend | `Stripe__SecretKey` | Stripe Sandbox |
| Backend | `Stripe__WebhookSecret` | Firma de eventos de Stripe Sandbox |

### Ambientes

| Ambiente | Backend | Pagos | Datos |
|---|---|---|---|
| Pruebas automatizadas | Local | Mock | PostgreSQL de pruebas con rollback |
| Desarrollo local | Máquina del equipo | Mock o Stripe Sandbox | PostgreSQL de desarrollo |
| Demostración | Render o Azure | Stripe Sandbox obligatorio | PostgreSQL administrado |

El ambiente de demostración aplicará las validaciones de seguridad de un despliegue público, pero
bloqueará expresamente cualquier clave o operación live de Stripe.

## Fase 0 — Confirmar servicios y cuentas

### Trabajo

1. Validar la base actual de Neon, su pooling y la conexión SSL.
2. Crear una copia de seguridad inicial de Neon.
3. Crear o validar cuentas de Vercel, Cloudinary, Resend y Stripe Sandbox.
4. Crear una cuenta de Render y comprobar si se tiene acceso a Azure for Students.
5. Elegir el primer destino de despliegue sin acoplar el código a esa decisión.
6. Definir las URLs finales del frontend y la API.
7. Preparar una matriz privada de secretos por ambiente.

### Terminado cuando

- Cada servicio elegido tiene una cuenta accesible.
- La base actual de Neon se conecta mediante SSL y tiene una copia de seguridad recuperable.
- Ningún secreto está dentro de Git.
- Stripe Sandbox puede crear datos de prueba sin usar información financiera real.
- Está elegido el primer destino del contenedor: Render o Azure.

## Fase 1 — Hacer la aplicación desplegable

### Backend

- Crear un `Dockerfile` multietapa para ASP.NET Core, sin instrucciones exclusivas de Render o
  Azure.
- Escuchar el puerto proporcionado por el entorno y usar un valor local seguro cuando no exista.
- Ejecutar y validar localmente la misma imagen que se publicará.
- Configurar CORS para la URL final de Vercel.
- Crear:
  - `GET /api/health` para vida de la aplicación.
  - `GET /api/health/ready` para comprobar PostgreSQL.
- Ejecutar los scripts SQL mediante un proceso controlado antes del primer arranque.
- Mantener HTTPS, forwarded headers y secretos por variables de ambiente.
- Verificar que el Outbox procesa mensajes después de despertar la instancia.
- Documentar un perfil Render y otro Azure con las mismas variables.

### Frontend

- Agregar la configuración de Vercel y la reescritura de SPA.
- Separar variables locales, preview y producción.
- Traducir el primer arranque lento del backend a un estado normal de carga.

### Verificación

- Vercel puede consultar la API por HTTPS.
- Registro, login, catálogo y reserva funcionan desde las URLs públicas.
- Una base no disponible provoca un readiness negativo.
- Reiniciar o redesplegar el backend no pierde datos.
- La imagen puede iniciarse con la configuración de Render y con la de Azure sin recompilar código.

## Fase 2 — Migrar las imágenes antes de poblar

### Modelo

Ampliar `ExperienceImage` con:

- `Provider`
- `PublicId`
- `SecureUrl`
- `Width`
- `Height`
- `Format`
- `AltText`
- `IsCover`
- `SortOrder`

### Backend

- Crear `IImageStorage`.
- Implementar `CloudinaryImageStorage`.
- Validar firma real del archivo, tamaño y dimensiones.
- Subir imágenes al proveedor y persistir metadatos solamente después del éxito.
- Compensar la carga remota si falla la transacción de PostgreSQL.
- Eliminar el recurso remoto de forma segura e idempotente.
- Generar URLs transformadas para:
  - tarjeta;
  - detalle;
  - miniatura administrativa.

### Frontend

- Permitir definir portada.
- Permitir texto alternativo breve.
- Mostrar progreso y errores por imagen.
- Conservar el máximo actual de imágenes por experiencia.

### Migración

- Migrar las imágenes locales existentes, si son necesarias.
- Retirar la dependencia de `/uploads/experiences`.

### Terminado cuando

- Las imágenes sobreviven reinicios y redespliegues.
- Las tarjetas no descargan imágenes originales de varios megabytes.
- Eliminar una imagen no deja archivos huérfanos.

## Fase 3 — Completar la información de las experiencias

### Campos principales

- `Slug`
- `ShortDescription`
- `DurationMinutes`
- `TimeZoneId`, inicialmente `America/Santo_Domingo`
- `MeetingPointInstructions`
- `PickupInformation`, opcional
- `WhatIsIncluded`
- `WhatIsNotIncluded`
- `WhatToBring`
- `GuestRequirements`
- `MinimumAge`, opcional
- `Difficulty`: `Easy`, `Moderate`, `Demanding`
- `AccessibilityInformation`
- Idiomas disponibles
- Política de cancelación
- Etiquetas de búsqueda

### Itinerario

Crear una colección ordenada:

- Título de la etapa.
- Descripción.
- Duración estimada.
- Ubicación opcional.
- Orden.

### Reglas

- Una experiencia no podrá enviarse a revisión sin los campos públicos obligatorios.
- Los textos se guardarán como contenido, nunca como HTML confiable.
- Cambios sustanciales devolverán la experiencia a borrador.
- La política seleccionada será la única fuente para calcular cancelaciones y reembolsos.
- Los campos avanzados aparecerán progresivamente en el formulario.

### Terminado cuando

- El detalle responde las preguntas esenciales antes de reservar.
- El administrador puede revisar toda la información y las fotografías.
- Los registros nuevos y existentes cumplen el contrato actualizado.

## Fase 4 — Paginación, búsqueda y administración escalables

### Contrato común

Crear una respuesta paginada:

```text
items
page
pageSize
totalItems
totalPages
```

### Aplicar a

- Catálogo público.
- Búsqueda.
- Cercanía y mapa.
- Experiencias del anfitrión.
- Reservas del turista y anfitrión.
- Solicitudes y experiencias en moderación.
- Reseñas y notificaciones cuando corresponda.

### Búsqueda

- Texto en título, resumen, ubicación y etiquetas.
- Filtros por categoría, precio, fecha, cantidad, idioma, dificultad y accesibilidad.
- Orden por relevancia, fecha, precio, valoración y distancia.
- Índices PostgreSQL apropiados.
- `pageSize` predeterminado de 24 y máximo controlado.

### Frontend

- Mantener filtros y página en la URL.
- Cancelar solicitudes anteriores.
- Cargar páginas adicionales sin perder el contexto.
- Filtrar moderación en el servidor, no sobre todos los registros descargados.

### Terminado cuando

- Ningún listado principal descarga todo el catálogo.
- La búsqueda conserva filtros al recargar o compartir la URL.
- Las consultas se mantienen rápidas con un conjunto de prueba amplio.

## Fase 5 — Horarios recurrentes y operación por lotes

Estado: implementada el 1 de agosto de 2026. La validación focalizada cubre generación repetida,
exclusiones, copia de semanas y atomicidad de las operaciones por lotes.

### Alcance

No se implementará un motor universal de calendarios. Se añadirá un generador sencillo:

- Rango de fechas.
- Días de la semana.
- Hora local de inicio y final.
- Capacidad.
- Fechas excluidas.
- Vista previa antes de crear.

El generador creará horarios individuales para conservar el modelo de reservas existente.

### Funciones

- Generar varias fechas en una operación idempotente.
- Evitar duplicados por experiencia y hora de inicio.
- Cerrar varias fechas futuras.
- Ajustar capacidad en varias fechas sin bajar de lo ya reservado.
- Copiar la configuración de una semana.
- Mostrar las fechas en `America/Santo_Domingo` y persistir UTC.

### Terminado cuando

- Publicar tres meses de disponibilidad no requiere crear cada fecha manualmente.
- Repetir una solicitud no duplica horarios.
- Las excepciones y fechas bloqueadas son visibles antes de confirmar.

## Fase 6 — Vencimiento de reservas pendientes

Estado: implementada el 4 de agosto de 2026. Incluye bloqueo transaccional por reserva, historial,
auditoría de capacidad, reconciliación al arrancar y durante la operación, y cuenta regresiva en
el detalle de la reserva.

### Dominio

- Agregar `ExpiresAt` a reservas `PendingPayment`.
- Agregar un estado explícito de expiración.
- Configurar la ventana, inicialmente 15 minutos.

### Procesamiento

- Expirar reservas vencidas en un proceso periódico mientras la API está activa.
- Ejecutar también una limpieza al arrancar la aplicación.
- Verificar expiración de forma perezosa antes de consultar disponibilidad o iniciar un pago.
- Liberar cupos exactamente una vez dentro de una transacción.
- Rechazar pagos posteriores al vencimiento.
- Registrar historial y auditoría de capacidad.

Este diseño considera que Render puede dormir: al despertar, la primera operación reconciliará las
reservas vencidas antes de ofrecer los cupos.

### Frontend

- Mostrar cuánto tiempo queda para pagar.
- Actualizar el estado cuando vence.
- Ofrecer volver a reservar si todavía hay disponibilidad.

### Terminado cuando

- Abandonar un pago no bloquea cupos indefinidamente.
- Dos procesos concurrentes no liberan cupos dos veces.
- No puede confirmarse una reserva ya vencida.

## Fase 7 — SEO y presentación pública

Estado: implementada el 4 de agosto de 2026. El sitemap y los HTML usados al compartir enlaces se
regeneran durante el build de producción, después de consultar únicamente experiencias aprobadas.

### Trabajo

- Usar `Slug` en las URLs públicas y mantener compatibilidad temporal con IDs.
- Agregar título, descripción, canonical y Open Graph por experiencia.
- Crear `robots.txt`.
- Generar `sitemap.xml` después de completar la carga manual.
- Agregar datos estructurados solamente con información real.
- Incluir horarios como eventos únicamente cuando cumplan ese significado.
- Crear imágenes sociales usando la portada de cada experiencia.
- Añadir páginas públicas de contacto, privacidad, términos y cancelaciones.

### Alcance académico

Las páginas legales indicarán que se trata de un prototipo universitario y que no procesa pagos
reales. No se presentarán como asesoría ni como condiciones comerciales definitivas.

### Terminado cuando

- Cada experiencia tiene una URL legible y compartible.
- Compartir una experiencia muestra título, resumen y fotografía correctos.
- El sitemap contiene solamente experiencias aprobadas.

## Fase 8 — Integrar Stripe Sandbox

Estado: implementada en código el 4 de agosto de 2026. El gateway Mock permanece disponible en
Development y QA. La activación pública requiere las credenciales de prueba y registrar el webhook
en la cuenta de Stripe Sandbox.

### Papel del gateway Mock

El gateway Mock se conservará exclusivamente para pruebas automatizadas y desarrollo sin conexión.
Continuará verificando:

- cálculo del precio;
- idempotencia;
- confirmación;
- rechazo;
- reembolso;
- actualización de cupos y dashboard;
- contratos neutrales del dominio.

El ambiente público de demostración utilizará Stripe Sandbox.

Complejidad media. No mueve dinero real y no requiere datos bancarios reales para simular pagos y
transferencias.

#### Información necesaria

- Una cuenta de Stripe que permita acceder a un sandbox.
- Clave publicable de prueba.
- Clave secreta de prueba.
- Secreto de firma del webhook.
- URL HTTPS pública del backend.
- Moneda de la demostración.
- Porcentaje de cargo y comisión.
- Eventos que se procesarán.

#### Implementación

1. Instalar el SDK oficial de Stripe para .NET.
2. Crear `StripePaymentGateway : IPaymentGateway`.
3. Crear un PaymentIntent por pago.
4. Devolver solo el `clientSecret` necesario al navegador.
5. Integrar Stripe Payment Element en React.
6. Crear un webhook y verificar su firma.
7. Procesar de forma idempotente:
   - pago exitoso;
   - pago rechazado;
   - pago cancelado;
   - reembolso.
8. Usar exclusivamente tarjetas de prueba.
9. Conservar el gateway Mock para pruebas automatizadas.
10. Rechazar el arranque si `Payments:Mode` no es `Sandbox` en el ambiente de demostración.
11. Rechazar claves live para impedir cobros accidentales.
12. Configurar el webhook con la URL del proveedor elegido:
    - `https://<api-render>/api/payments/webhook`; o
    - `https://<api-azure>/api/payments/webhook`.

La arquitectura existente de `IPaymentGateway`, pagos persistentes y eventos únicos reduce el
trabajo necesario.

### Pagos comerciales y Stripe Connect

Quedan fuera de este proyecto. Stripe no muestra actualmente a República Dominicana entre los
países donde una empresa puede abrir directamente una cuenta de pagos.

Una versión comercial exigiría resolver antes:

- jurisdicción y entidad legal admitida;
- identidad y datos fiscales del negocio;
- cuenta bancaria;
- responsables y beneficiarios;
- onboarding de cada anfitrión;
- comisiones y calendario de transferencias;
- reembolsos, disputas y saldos negativos;
- términos, privacidad y soporte real.

No se deben registrar datos falsos de país, empresa o banco para activar Stripe.

### Terminado cuando

- Payment Element acepta únicamente tarjetas de prueba.
- Un PaymentIntent exitoso confirma la reserva una sola vez.
- Un rechazo no confirma la reserva ni duplica intentos.
- Un webhook repetido no duplica efectos.
- Un reembolso Sandbox actualiza pago, reserva, cupos e historial.
- Cambiar la URL del backend entre Render y Azure solo exige actualizar configuración y webhook.
- Ninguna clave secreta llega al frontend o al repositorio.

## Fase 9 — Operación, pruebas y documentación

**Estado: implementada el 4 de agosto de 2026; activación externa pendiente.**

### Operación

- [x] Logs JSON estructurados con correlation ID.
- [x] Readiness con PostgreSQL.
- [x] Manejo uniforme de errores de infraestructura, validación y rutas sin respuesta.
- [ ] Copia de seguridad antes de la presentación; requiere acceso a Neon.
- [x] Procedimiento documentado de restauración segura sobre una base separada.
- [ ] Revisión de claves expuestas anteriormente y rotación en proveedores.
- [ ] Restricción de las claves de Google por dominio y API en Google Cloud.

### Calidad

- [x] Build Release del backend sin advertencias.
- [x] Lint y build del frontend aprobados.
- [x] Pruebas focalizadas de imágenes, búsqueda, horarios y expiración: 12/12.
- [x] La suite PostgreSQL desactiva paralelismo, prepara un esquema vacío y tiene CI con PostgreSQL.
- [ ] Pruebas desde las URLs públicas en móvil y escritorio; requieren despliegue.

La suite conjunta finalizó con 104/104 pruebas en Release el 4 de agosto de 2026. Una desconexión
transitoria de Neon observada durante la matriz focalizada motivó reintentos breves y acotados en
la infraestructura de pruebas; la matriz y la suite completa finalizaron correctamente después
del ajuste.

### Documentación

- [x] Este documento es el plan activo.
- [x] README del frontend actualizado.
- [x] Planes anteriores marcados como históricos y reconciliados.
- [x] Variables de ambiente documentadas sin valores secretos.
- [x] Guía de despliegue y runbook de recuperación creados.

## Orden de ejecución recomendado

1. Fase 0: servicios y decisiones.
2. Fase 1: despliegue mínimo.
3. Fase 2: almacenamiento de imágenes.
4. Fase 3: modelo definitivo de experiencias.
5. Fase 4: paginación y búsqueda.
6. Fase 5: horarios recurrentes.
7. Fase 6: vencimiento de reservas.
8. Fase 7: SEO y páginas públicas.
9. Fase 8: Stripe Sandbox.
10. Fase 9: validación final y documentación.
11. Carga manual del catálogo definitivo.

No se debe comenzar la carga grande antes de cerrar las fases 2 y 3. Se pueden crear entre cinco y
diez experiencias temporales para probar formularios, búsquedas, imágenes y horarios durante el
desarrollo.

## Fuentes de las decisiones de infraestructura

- Vercel y Vite:
  <https://vercel.com/docs/frameworks/frontend/vite>
- Render gratuito:
  <https://render.com/docs/free>
- Render Web Services y Docker:
  <https://render.com/docs/web-services>
- Neon:
  <https://neon.com/pricing>
- Cloudinary:
  <https://cloudinary.com/pricing>
- Azure for Students:
  <https://azure.microsoft.com/en-us/free/students>
- Stripe Sandbox:
  <https://docs.stripe.com/testing-use-cases>
- Stripe PaymentIntents:
  <https://docs.stripe.com/payments/payment-intents>
- Stripe Connect:
  <https://docs.stripe.com/connect/marketplace/essential-tasks>
- Disponibilidad global de Stripe:
  <https://stripe.com/global>
