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
| Build de `backend/Dockerfile` | Correcto |
| Imagen local | `goisland-api:catalog-deployment` |
| `GET /api/health` desde el contenedor | `Healthy` |
| `GET /api/health/ready` contra Neon | `Ready` |
