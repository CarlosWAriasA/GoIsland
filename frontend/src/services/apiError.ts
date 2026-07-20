import axios from 'axios';

export interface ApiError {
  message: string;
  errors?: Record<string, string[]>;
  status?: number;
}

const fallbackMessage = 'No fue posible completar la solicitud. Inténtalo de nuevo.';

export const toApiError = (error: unknown, fallback = fallbackMessage): ApiError => {
  if (!axios.isAxiosError(error)) {
    return { message: fallback };
  }

  const data = error.response?.data;
  if (!data || typeof data !== 'object') {
    return {
      message: error.code === 'ERR_NETWORK'
        ? 'No fue posible conectar con GoIsland. Verifica que la API esté disponible.'
        : fallback,
      status: error.response?.status,
    };
  }

  const payload = data as { message?: unknown; errors?: unknown };
  const errors = isValidationErrors(payload.errors) ? payload.errors : undefined;
  const firstValidationError = errors ? Object.values(errors).flat()[0] : undefined;

  return {
    message: typeof payload.message === 'string'
      ? payload.message
      : firstValidationError ?? fallback,
    errors,
    status: error.response?.status,
  };
};

const isValidationErrors = (value: unknown): value is Record<string, string[]> => {
  if (!value || typeof value !== 'object') {
    return false;
  }

  return Object.values(value).every(
    (messages) => Array.isArray(messages) && messages.every((message) => typeof message === 'string'),
  );
};
