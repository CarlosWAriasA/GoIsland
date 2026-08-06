import { api } from './api';
import type { Payment, PaymentCheckout } from '../types';

export const paymentService = {
  getById: async (id: number, signal?: AbortSignal): Promise<Payment> => {
    const response = await api.get<Payment>(`/payments/${id}`, { signal });
    return response.data;
  },

  getForReservation: async (reservationId: number, signal?: AbortSignal): Promise<Payment[]> => {
    const response = await api.get<Payment[]>(`/reservations/${reservationId}/payments`, { signal });
    return response.data;
  },

  createCheckout: async (reservationId: number): Promise<PaymentCheckout> => {
    const response = await api.post<PaymentCheckout>(`/reservations/${reservationId}/payments`, null, {
      headers: { 'Idempotency-Key': crypto.randomUUID() },
    });
    return response.data;
  },

  resumeCheckout: async (paymentId: number): Promise<PaymentCheckout> => {
    const response = await api.get<PaymentCheckout>(`/payments/${paymentId}/checkout`);
    return response.data;
  },

  confirmMock: async (paymentId: number): Promise<Payment> => {
    const response = await api.post<Payment>(`/payments/${paymentId}/mock-confirm`);
    return response.data;
  },

  refund: async (id: number, reason: string): Promise<Payment> => {
    const response = await api.post<Payment>(`/admin/payments/${id}/refund`, { reason });
    return response.data;
  },
};
