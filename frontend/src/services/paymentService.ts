import { api } from './api';
import type { Payment } from '../types';

export const paymentService = {
  getById: async (id: number, signal?: AbortSignal): Promise<Payment> => {
    const response = await api.get<Payment>(`/payments/${id}`, { signal });
    return response.data;
  },

  getForReservation: async (reservationId: number, signal?: AbortSignal): Promise<Payment[]> => {
    const response = await api.get<Payment[]>(`/reservations/${reservationId}/payments`, { signal });
    return response.data;
  },

  pay: async (reservationId: number, pendingPaymentId?: number): Promise<Payment> => {
    let paymentId = pendingPaymentId;
    if (!paymentId) {
      const created = await api.post<Payment>(`/reservations/${reservationId}/payments`, null, {
        headers: { 'Idempotency-Key': crypto.randomUUID() },
      });
      paymentId = created.data.id;
    }

    const completed = await api.post<Payment>(`/payments/${paymentId}/mock-confirm`);
    return completed.data;
  },

  refund: async (id: number, reason: string): Promise<Payment> => {
    const response = await api.post<Payment>(`/admin/payments/${id}/refund`, { reason });
    return response.data;
  },
};
