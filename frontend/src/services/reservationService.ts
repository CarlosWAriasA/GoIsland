import { api } from './api';
import type {
  CreateReservationRequest,
  CreateSelfScheduledReservationRequest,
  PagedResponse,
  Reservation,
  ReservationChangeRequest,
  ReservationListParams,
} from '../types';

export interface ChangeRequestListParams {
  status?: string;
  page?: number;
  pageSize?: number;
}

export const reservationService = {
  create: async (data: CreateReservationRequest): Promise<Reservation> => {
    const response = await api.post<Reservation>('/reservations', data, {
      headers: { 'Idempotency-Key': crypto.randomUUID() },
    });
    return response.data;
  },

  createSelfScheduled: async (data: CreateSelfScheduledReservationRequest): Promise<Reservation> => {
    const response = await api.post<Reservation>('/reservations/self-scheduled', data, {
      headers: { 'Idempotency-Key': crypto.randomUUID() },
    });
    return response.data;
  },

  getMy: async (
    params: ReservationListParams = {},
    signal?: AbortSignal,
  ): Promise<PagedResponse<Reservation>> => {
    const response = await api.get<PagedResponse<Reservation>>('/reservations/my', {
      params,
      signal,
    });
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

  rescheduleSelfScheduled: async (id: number, startsAtLocal: string, quantity: number): Promise<Reservation> => {
    const response = await api.post<Reservation>(`/reservations/${id}/reschedule-self-scheduled`, { startsAtLocal, quantity }, {
      headers: { 'Idempotency-Key': crypto.randomUUID() },
    });
    return response.data;
  },

  complete: async (id: number): Promise<Reservation> => {
    const response = await api.post<Reservation>(`/reservations/${id}/complete`, null, {
      headers: { 'Idempotency-Key': crypto.randomUUID() },
    });
    return response.data;
  },

  getForHost: async (
    params: ReservationListParams = {},
    signal?: AbortSignal,
  ): Promise<PagedResponse<Reservation>> => {
    const response = await api.get<PagedResponse<Reservation>>('/host/reservations', {
      params,
      signal,
    });
    return response.data;
  },

  cancelByHost: async (id: number, reason: string): Promise<Reservation> => {
    const response = await api.post<Reservation>(`/host/reservations/${id}/cancel`, { reason }, {
      headers: { 'Idempotency-Key': crypto.randomUUID() },
    });
    return response.data;
  },

  completeByHost: async (id: number): Promise<Reservation> => {
    const response = await api.post<Reservation>(`/host/reservations/${id}/complete`, null, {
      headers: { 'Idempotency-Key': crypto.randomUUID() },
    });
    return response.data;
  },

  requestCancellation: async (id: number, reason: string): Promise<ReservationChangeRequest> => {
    const response = await api.post<ReservationChangeRequest>(`/reservations/${id}/cancellation-requests`, { reason });
    return response.data;
  },

  requestReschedule: async (id: number, scheduleId: number, reason: string): Promise<ReservationChangeRequest> => {
    const response = await api.post<ReservationChangeRequest>(`/reservations/${id}/reschedule-requests`, { scheduleId, reason });
    return response.data;
  },

  getChangeRequestsForHost: async (
    params: ChangeRequestListParams = {},
    signal?: AbortSignal,
  ): Promise<PagedResponse<ReservationChangeRequest>> => {
    const response = await api.get<PagedResponse<ReservationChangeRequest>>('/host/reservations/change-requests', {
      params,
      signal,
    });
    return response.data;
  },

  reviewChangeRequest: async (id: number, approve: boolean, decisionReason?: string): Promise<void> => {
    await api.post(`/host/reservations/change-requests/${id}/review`, { approve, decisionReason }, {
      headers: { 'Idempotency-Key': crypto.randomUUID() },
    });
  },
};
