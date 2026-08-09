# Operación y recuperación de GoIsland

Esta guía cubre la demostración universitaria. No contiene credenciales ni reemplaza los controles
de acceso de Neon, Azure, Vercel, Cloudinary, Resend, Google o Stripe.

## Responsables y registro

Antes de publicar, completar en `external-readiness-checklist.md`:

- responsable de despliegue y recuperación;
- URL activa del frontend y de la API;
- fecha, ubicación protegida y responsable de la última copia;
- versión o commit desplegado;
- resultado del último simulacro de restauración.

No registrar secretos, cadenas de conexión, tokens ni contenido de archivos `.env`.

## Respaldo de PostgreSQL

Para cada cambio de esquema o carga importante:

1. Suspender escrituras o realizar la copia antes de abrir tráfico.
2. Tomar una instantánea desde **Backup & Restore** de Neon si el plan disponible lo permite.
3. Crear además una copia lógica externa con una cadena **directa, no pooled**. Neon recomienda
   evitar conexiones con PgBouncer al usar `pg_dump`.
4. Guardar el archivo cifrado en una ubicación privada fuera del repositorio.
5. Comprobar que el archivo puede listarse y registrar fecha, tamaño y responsable.

Ejemplo en PowerShell, ejecutado desde una carpeta privada de respaldos:

```powershell
$env:GOISLAND_SOURCE_DB = '<conexion-directa-de-Neon>'
pg_dump --format=custom --no-owner --no-acl --file goisland-predeploy.dump --dbname $env:GOISLAND_SOURCE_DB
pg_restore --list goisland-predeploy.dump
Remove-Item Env:GOISLAND_SOURCE_DB
```

El cliente `pg_dump` debe ser de la misma versión mayor que PostgreSQL o una posterior compatible.
Una lista legible no sustituye el simulacro de restauración.

Referencias: [migración con pg_dump en Neon](https://neon.com/docs/import/migrate-from-neon) y
[pg_restore](https://www.postgresql.org/docs/current/app-pgrestore.html).

## Restauración segura

Nunca probar una restauración sobre la base activa.

1. Crear una rama o base separada en Neon y obtener su cadena directa.
2. Restaurar el archivo con salida inmediata ante errores:

   ```powershell
   $env:GOISLAND_TARGET_DB = '<conexion-directa-de-la-base-de-recuperacion>'
   pg_restore --exit-on-error --no-owner --no-acl --clean --if-exists --dbname $env:GOISLAND_TARGET_DB goisland-predeploy.dump
   Remove-Item Env:GOISLAND_TARGET_DB
   ```

3. Comparar conteos de `users`, `experiences`, `experience_schedules`, `reservations`, `payments`,
   `experience_images` y `notification_outbox` con el origen registrado.
4. Ejecutar las migraciones posteriores a la copia, si existen.
5. Iniciar una revisión del backend apuntando a la base recuperada y comprobar
   `/api/health/ready`, acceso, catálogo, una reserva controlada y notificaciones.
6. Verificar que las imágenes referenciadas siguen disponibles en Cloudinary.
7. Solo entonces actualizar `ConnectionStrings__DefaultConnection` y dirigir tráfico a la
   revisión validada.
8. Conservar la base anterior sin escrituras durante la ventana de reversión acordada.

Si la restauración falla, conservar el archivo y la base de prueba, retirarles acceso público y
registrar el error usando el correlation ID de la solicitud afectada.

## Migraciones de la demostración

La base actual de Neon recibió estas migraciones el 4 de agosto de 2026, en este orden:

1. `014_add_catalog_search_indexes.sql`
2. `015_add_unique_schedule_start.sql`
3. `016_add_reservation_expiration.sql`

En una base nueva deben aplicarse en el mismo orden. Cada aplicación debe comprobar
`/api/health/ready` y ejecutar la suite focalizada correspondiente.

Las migraciones `017` a `020` ya estaban presentes en Neon y las migraciones `021` y `022` se
aplicaron el 9 de agosto de 2026, en ese orden. Antes de aplicarlas se creó una copia lógica con
PostgreSQL 18, se validó su tabla de contenido con `pg_restore` y se comprobaron los conteos de
usuarios, experiencias, reservas y pagos antes y después.

Para migraciones futuras:

1. crear y verificar una copia recuperable;
2. ejecutar `./scripts/verify-release.ps1 -IncludeIntegration` contra una base local o efímera;
3. aplicar únicamente los scripts pendientes, respetando su orden numérico;
4. comprobar readiness y los flujos afectados;
5. registrar fecha, versión y responsable en `external-readiness-checklist.md`.

## Rotación de credenciales

Toda clave que haya aparecido en un commit, captura, chat, log o archivo compartido se considera
expuesta. Rotar primero en el proveedor, actualizar el gestor de secretos, desplegar y revocar la
anterior. No pegar valores en tickets ni en este registro.

| Credencial | Acción posterior |
|---|---|
| Neon | Probar conexión y readiness; revocar la contraseña anterior |
| `Jwt__Key` | Volver a iniciar sesión; la rotación invalida sesiones existentes |
| Resend o SMTP | Enviar recuperación controlada y revocar la clave anterior |
| Cloudinary | Probar carga y eliminación controlada |
| VAPID | Volver a suscribir el navegador si cambia el par de claves |
| Stripe Sandbox | Probar pago y recrear la firma del webhook si corresponde |
| Google Maps | Restringir por sitio y solo a las API utilizadas |
| Google OAuth | Autorizar únicamente los orígenes HTTPS controlados |

Para Google Maps, usar restricciones de **Websites/HTTP referrers** para el dominio final y las
previews que se decida admitir, y una restricción de API para Maps JavaScript API. Google documenta
estas restricciones en [Manage API keys](https://docs.cloud.google.com/docs/authentication/api-keys).
El cliente OAuth web debe contener únicamente los orígenes JavaScript propios; producción usa
HTTPS y las páginas públicas de inicio, privacidad y términos. Véanse las
[políticas OAuth 2.0 de Google](https://developers.google.com/identity/protocols/oauth2/policies).

## Observabilidad y diagnóstico

- Los logs del backend se emiten como JSON por consola para que Azure o Render los recojan.
- Cada respuesta incluye `X-Correlation-ID`. El backend conserva un valor entrante seguro o crea
  uno y lo añade al scope de logs.
- Los errores públicos incluyen `correlationId`, pero no excepciones, SQL ni configuración.
- `/api/health` comprueba vida del proceso.
- `/api/health/ready` comprueba PostgreSQL y devuelve `503` cuando no está disponible.

Ante un incidente, registrar hora UTC, URL, estado HTTP, correlation ID y versión desplegada antes
de reiniciar o cambiar configuración.

## Verificación pública

Ejecutar desde móvil y escritorio, sin herramientas internas:

- abrir inicio, catálogo, búsqueda y una experiencia mediante su `slug`;
- registrar o acceder con correo y con Google;
- crear una reserva, completar un pago Sandbox y comprobar el estado final;
- validar vencimiento y reembolso con una reserva controlada;
- revisar carga de imágenes, correo, navegación directa y recarga de rutas;
- comprobar privacidad, términos, cancelaciones, `robots.txt` y `sitemap.xml`.

Registrar dispositivo, navegador, URL y resultado en `external-readiness-checklist.md`.
