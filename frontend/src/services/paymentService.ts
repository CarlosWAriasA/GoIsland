import { api } from './api';
import type { Payment } from '../types';

export const paymentService = {
  create: async (reservationId: number): Promise<Payment> => {
    const response = await api.post<Payment>(`/reservations/${reservationId}/payments`, null, {
      headers: { 'Idempotency-Key': crypto.randomUUID() },
    });
    return response.data;
  },

  getById: async (id: number, signal?: AbortSignal): Promise<Payment> => {
    const response = await api.get<Payment>(`/payments/${id}`, { signal });
    return response.data;
  },

  getForReservation: async (reservationId: number, signal?: AbortSignal): Promise<Payment[]> => {
    const response = await api.get<Payment[]>(`/reservations/${reservationId}/payments`, { signal });
    return response.data;
  },

  mockConfirm: async (id: number): Promise<Payment> => {
    const response = await api.post<Payment>(`/payments/${id}/mock-confirm`);
    return response.data;
  },

  mockReject: async (id: number, failureCode?: string): Promise<Payment> => {
    const response = await api.post<Payment>(`/payments/${id}/mock-reject`, { failureCode });
    return response.data;
  },

  refund: async (id: number, reason: string): Promise<Payment> => {
    const response = await api.post<Payment>(`/admin/payments/${id}/refund`, { reason });
    return response.data;
  },
};
