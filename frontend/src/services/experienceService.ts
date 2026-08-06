import type {
  Experience,
  ExperienceSchedule,
  ExperienceSearchParams,
  PagedResponse,
} from '../types';
import { api } from './api';

export const experienceService = {
  getExperiences: async (
    signal?: AbortSignal,
    params: ExperienceSearchParams = {},
  ): Promise<PagedResponse<Experience>> => {
    const response = await api.get<PagedResponse<Experience>>('/experiences', { params, signal });
    return response.data;
  },

  searchExperiences: async (
    params: ExperienceSearchParams,
    signal?: AbortSignal,
  ): Promise<PagedResponse<Experience>> => {
    const response = await api.get<PagedResponse<Experience>>('/experiences/search', { params, signal });
    return response.data;
  },

  getExperience: async (identifier: number | string, signal?: AbortSignal): Promise<Experience> => {
    const numericId = typeof identifier === 'number'
      || /^\d+$/.test(identifier);
    const path = numericId
      ? `/experiences/${identifier}`
      : `/experiences/by-slug/${encodeURIComponent(identifier)}`;
    const response = await api.get<Experience>(path, { signal });
    return response.data;
  },

  getNearby: async (
    latitude: number,
    longitude: number,
    radiusKm: number,
    signal?: AbortSignal,
  ): Promise<PagedResponse<Experience>> => {
    const response = await api.get<PagedResponse<Experience>>('/experiences/nearby', {
      params: { latitude, longitude, radiusKm, pageSize: 100 },
      signal,
    });
    return response.data;
  },

  getAvailability: async (
    id: number,
    params?: { from?: string; to?: string; quantity?: number },
    signal?: AbortSignal,
  ): Promise<ExperienceSchedule[]> => {
    const response = await api.get<ExperienceSchedule[]>(`/experiences/${id}/availability`, {
      params,
      signal,
    });
    return response.data;
  },
};
export type { Experience };
