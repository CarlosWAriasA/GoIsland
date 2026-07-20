import type { ManagedExperience, ManagedExperienceRequest } from '../types';
import { api } from './api';

export const hostExperienceService = {
  getMine: async (signal?: AbortSignal): Promise<ManagedExperience[]> => {
    const response = await api.get<ManagedExperience[]>('/host/experiences', { signal });
    return response.data;
  },

  create: async (data: ManagedExperienceRequest): Promise<ManagedExperience> => {
    const response = await api.post<ManagedExperience>('/host/experiences', data);
    return response.data;
  },

  update: async (id: number, data: ManagedExperienceRequest): Promise<ManagedExperience> => {
    const response = await api.put<ManagedExperience>(`/host/experiences/${id}`, data);
    return response.data;
  },

  submit: async (id: number): Promise<ManagedExperience> => {
    const response = await api.post<ManagedExperience>(`/host/experiences/${id}/submit`);
    return response.data;
  },

  remove: async (id: number): Promise<void> => {
    await api.delete(`/host/experiences/${id}`);
  },
};
