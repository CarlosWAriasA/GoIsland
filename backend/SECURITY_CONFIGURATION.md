# Configuracion de seguridad de GoIsland

## Secretos

Los secretos no deben guardarse en `appsettings*.json`, `.env`, scripts, ejemplos ni commits.

En desarrollo se administran con `dotnet user-secrets`:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<conexion>" --project GoIsland.Api
dotnet user-secrets set "Jwt:Key" "<secreto-aleatorio-de-al-menos-32-bytes>" --project GoIsland.Api
dotnet user-secrets set "Smtp:Username" "<usuario>" --project GoIsland.Api
dotnet user-secrets set "Smtp:Password" "<contrasena>" --project GoIsland.Api
dotnet user-secrets set "WebPush:PrivateKey" "<clave-vapid-privada>" --project GoIsland.Api
```

En QA y produccion deben configurarse mediante el gestor de secretos del proveedor con nombres
de variables de ambiente como:

- `ConnectionStrings__DefaultConnection`
- `Jwt__Key`
- `Smtp__Username` y `Smtp__Password`, o `Resend__ApiKey`
- `WebPush__PrivateKey`
- `GoogleAuth__ClientId`
- `GoogleMaps__ApiKey`
- `Cloudinary__CloudName`, `Cloudinary__ApiKey` y `Cloudinary__ApiSecret`
- `Stripe__SecretKey` y `Stripe__WebhookSecret` exclusivamente de Sandbox

La conexion PostgreSQL y la clave JWT que existieron en el historial deben considerarse
comprometidas. Se deben rotar en el proveedor y en todos los ambientes antes de desplegar.

La rotacion se realiza primero en el proveedor, despues en el gestor de secretos del ambiente y
por ultimo se revoca la credencial anterior. `Jwt__Key` invalida las sesiones existentes. El
inventario y la validacion posterior estan en `../docs/deployment/operations-runbook.md`.

## Google

- La clave de Maps debe aceptar solo el dominio publico y las previews autorizadas mediante
  restricciones HTTP referrer, y limitarse a Maps JavaScript API.
- El cliente OAuth web debe declarar solamente origenes JavaScript HTTPS controlados. `localhost`
  se conserva unicamente en el cliente o ambiente de desarrollo.
- El client ID es publico; no se debe tratar como sustituto de autenticacion del backend. La API
  valida firma, emisor, audiencia, expiracion y correo verificado.

## Proxy, HTTPS y hosts

- Fuera de `Development`, la API habilita HSTS y redireccion HTTPS.
- `Cors__FrontendUrl` es obligatorio fuera de desarrollo y debe ser un origen HTTPS sin ruta.
- `AllowedHosts` debe contener solamente los hosts publicos reales de la API.
- `ForwardedHeaders__KnownProxies__0`, `__1`, etc. deben identificar las IP de los proxies
  confiables que terminan TLS. No se deben aceptar encabezados reenviados desde proxies
  desconocidos.

## Proteccion de autenticacion

- Registro, login y Google admiten hasta 10 solicitudes por IP por minuto.
- Recuperacion y restablecimiento admiten hasta 5 solicitudes por IP cada 15 minutos.
- Cinco contrasenas incorrectas bloquean temporalmente la cuenta; fallos posteriores aumentan
  progresivamente el bloqueo hasta una hora.
- Las respuestas de login permanecen genericas para no revelar si una cuenta existe o esta
  bloqueada.
- Registro, cambio y restablecimiento exigen 12 a 128 caracteres, mayuscula, minuscula y numero.

## Base de datos

Antes de ejecutar la nueva version se debe aplicar:

```powershell
.\ApplyDatabaseScript.ps1 -ScriptPath .\GoIsland.Api\Database\Scripts\010_add_login_protection.sql
```

El script toma la conexion de `ConnectionStrings__DefaultConnection` o de `user-secrets`, nunca
de un archivo versionado.

Los scripts `011` a `016` y su orden de despliegue se documentan en `../DEPLOYMENT.md`.
