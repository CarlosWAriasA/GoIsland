import { api } from './api';
import type { AdminReviewListParams, PagedResponse, Review, ReviewInput } from '../types';

export const reviewService = {
  forAdmin: async (
    params: AdminReviewListParams,
    signal?: AbortSignal,
  ): Promise<PagedResponse<Review>> =>
    (await api.get<PagedResponse<Review>>('/admin/reviews', { params, signal })).data,
  hide: async (id: number, reason: string): Promise<Review> =>
    (await api.post<Review>(`/admin/reviews/${id}/hide`, { reason })).data,
  forExperience: async (
    experienceId: number,
    signal?: AbortSignal,
    reservationId?: number,
  ): Promise<PagedResponse<Review>> =>
    (await api.get<PagedResponse<Review>>(`/experiences/${experienceId}/reviews`, {
      params: { pageSize: reservationId ? 1 : 100, reservationId },
      signal,
    })).data,
  create: async (reservationId: number, input: ReviewInput): Promise<Review> =>
    (await api.post<Review>(`/reservations/${reservationId}/review`, input)).data,
  update: async (id: number, input: ReviewInput): Promise<Review> =>
    (await api.put<Review>(`/reviews/${id}`, input)).data,
  remove: async (id: number): Promise<void> => { await api.delete(`/reviews/${id}`); },
};
