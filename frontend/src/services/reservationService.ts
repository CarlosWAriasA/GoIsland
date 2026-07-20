import { api } from './api';
import type { CreateReservationRequest, Reservation } from '../types';

export const reservationService = {
  create: async (data: CreateReservationRequest): Promise<Reservation> => {
    const response = await api.post<Reservation>('/reservations', data);
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
};
