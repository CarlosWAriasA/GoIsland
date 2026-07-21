import type { Experience, ExperienceSchedule, ExperienceSearchParams } from '../types';
import { api } from './api';

export const experienceService = {
  getExperiences: async (signal?: AbortSignal): Promise<Experience[]> => {
    const response = await api.get<Experience[]>('/experiences', { signal });
    return response.data;
  },

  searchExperiences: async (
    params: ExperienceSearchParams,
    signal?: AbortSignal,
  ): Promise<Experience[]> => {
    const response = await api.get<Experience[]>('/experiences/search', { params, signal });
    return response.data;
  },

  getExperience: async (id: number, signal?: AbortSignal): Promise<Experience> => {
    const response = await api.get<Experience>(`/experiences/${id}`, { signal });
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
