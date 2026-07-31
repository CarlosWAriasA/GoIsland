import axios from 'axios';

// URL base de la API, tomada de las variables de entorno de Vite
const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5057/api';
const API_ORIGIN = API_URL.replace(/\/api\/?$/, '');

export const resolveApiAssetUrl = (url: string) => {
  if (/^https?:\/\//i.test(url)) return url;
  return `${API_ORIGIN}${url.startsWith('/') ? '' : '/'}${url}`;
};

export const api = axios.create({
  baseURL: API_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

let authToken: string | null = null;
let unauthorizedHandler: (() => void) | null = null;

// Actualiza el token guardado en memoria
export const setAuthToken = (token: string | null) => {
  authToken = token;
};

export const setUnauthorizedHandler = (handler: (() => void) | null) => {
  unauthorizedHandler = handler;
};

// Interceptor que adjunta el token JWT a cada petición si existe en memoria
api.interceptors.request.use(
  (config) => {
    if (authToken) {
      config.headers.Authorization = `Bearer ${authToken}`;
    }
    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401 && authToken) {
      unauthorizedHandler?.();
    }
    return Promise.reject(error);
  },
);
