# GoIsland Frontend

Cliente React + TypeScript para la API de GoIsland.

## Requisitos

- Node.js compatible con Vite 8.
- API de GoIsland disponible en `http://localhost:5057`.

## Configuracion local

1. Copia `.env.example` como `.env` si necesitas cambiar la URL de la API.
2. Instala dependencias con `npm install`.
3. Inicia el cliente con `npm run dev`.

Vite usa `http://localhost:5173` y el valor predeterminado de `VITE_API_URL` es
`http://localhost:5057/api`.

## Verificacion

```text
npm run lint
npm run build
```

## Integracion disponible

- Autenticacion: registro, inicio de sesion, identidad y actualizacion de perfil.
- Catalogo real: listado y busqueda por ubicacion, una categoria y precio maximo.
- Manejo comun de errores API con `message` y errores de validacion por campo.

Favoritos, calificaciones, imagenes especificas y reservas no se simulan mientras sus flujos
persistentes no esten integrados.
