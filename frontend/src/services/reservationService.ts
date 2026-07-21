import { api } from './api';
import type { CreateReservationRequest, Reservation } from '../types';

export const reservationService = {
  create: async (data: CreateReservationRequest): Promise<Reservation> => {
    const response = await api.post<Reservation>('/reservations', data, {
      headers: { 'Idempotency-Key': crypto.randomUUID() },
    });
    return response.data;
  },

  getMy: async (signal?: AbortSignal): Promise<Reservation[]> => {
    const response = await api.get<Reservation[]>('/reservations/my', { signal });
    return response.data;
  },

  getById: async (id: number, signal?: AbortSignal): Promise<Reservation> => {
    const response = await api.get<Reservation>(`/reservations/${id}`, { signal });
    return response.data;
  },

  cancel: async (id: number, reason?: string): Promise<Reservation> => {
    const response = await api.post<Reservation>(`/reservations/${id}/cancel`, { reason }, {
      headers: { 'Idempotency-Key': crypto.randomUUID() },
    });
    return response.data;
  },

  reschedule: async (id: number, scheduleId: number): Promise<Reservation> => {
    const response = await api.post<Reservation>(`/reservations/${id}/reschedule`, { scheduleId }, {
      headers: { 'Idempotency-Key': crypto.randomUUID() },
    });
    return response.data;
  },

  getForHost: async (signal?: AbortSignal): Promise<Reservation[]> => {
    const response = await api.get<Reservation[]>('/host/reservations', { signal });
    return response.data;
  },

  cancelByHost: async (id: number, reason: string): Promise<Reservation> => {
    const response = await api.post<Reservation>(`/host/reservations/${id}/cancel`, { reason }, {
      headers: { 'Idempotency-Key': crypto.randomUUID() },
    });
    return response.data;
  },
};
