# Importador del catálogo inicial

Este proyecto carga un catálogo editorial de destinos reales en la base configurada de GoIsland.
La importación es idempotente: identifica cada publicación por su `slug` y solo actualiza registros
que pertenecen a la cuenta interna `catalogo@goisland.invalid`.

El catálogo incluido contiene 30 publicaciones informativas y autoguiadas. Se publican aprobadas,
con precio cero, capacidad informativa ilimitada y sin horarios. Al no crear horarios, la interfaz
no ofrece la acción de reservar.

Cada publicación contiene al menos tres imágenes reutilizables seleccionadas de Wikimedia Commons.
Al importar con `--apply`, el proceso descarga cada archivo y lo sube a la misma cuenta de Cloudinary
que utiliza la creación normal de experiencias. La aplicación sirve la copia propia; el enlace de la
fuente original se conserva únicamente como atribución junto con el autor y la licencia.

## Preparación

Compila la solución y aplica primero la migración de atribuciones:

```powershell
cd backend
dotnet build GoIsland.sln
.\ApplyDatabaseScript.ps1 -ScriptPath .\GoIsland.Api\Database\Scripts\017_add_image_attribution.sql
```

El comando utiliza `ConnectionStrings__DefaultConnection` o el mismo user-secret
`ConnectionStrings:DefaultConnection` del API. No escribas la conexión directamente en el
repositorio. Para `--apply` también deben estar configurados `Cloudinary:CloudName`,
`Cloudinary:ApiKey` y `Cloudinary:ApiSecret`.

## Uso

Validar el archivo sin conectarse a la base:

```powershell
dotnet run --project .\GoIsland.CatalogImporter -- --validate
```

Simular toda la importación dentro de una transacción que finalmente se revierte:

```powershell
dotnet run --project .\GoIsland.CatalogImporter -- --dry-run
```

La simulación calcula cuántas imágenes faltan, pero no descarga ni sube archivos.

Guardar el catálogo:

```powershell
dotnet run --project .\GoIsland.CatalogImporter -- --apply
```

Para validar o importar otro archivo:

```powershell
dotnet run --project .\GoIsland.CatalogImporter -- --validate --catalog C:\ruta\catalog.json
```

## Ambiente desplegado

Antes de apuntar el importador a una base compartida o de producción:

1. Crea una copia de seguridad.
2. Aplica las migraciones, incluida `017_add_image_attribution.sql`.
3. Ejecuta `--dry-run` y revisa los conteos.
4. Ejecuta `--apply`.
5. Comprueba el catálogo público y las atribuciones de las fotos.
6. Realiza después el build definitivo del frontend para que el sitemap incluya los destinos.

El importador no crea, elimina ni modifica horarios. Las reimportaciones conservan las imágenes ya
subidas cuya fuente coincide, por lo que no generan duplicados en Cloudinary. Tampoco modifica
publicaciones con el mismo `slug` si pertenecen a otro anfitrión; en ese caso revierte toda la
operación.
