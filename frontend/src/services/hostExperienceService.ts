import type {
  CreateScheduleRequest,
  ExperienceSchedule,
  ManagedExperience,
  ManagedExperienceRequest,
  UpdateScheduleRequest,
} from '../types';
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

  getSchedules: async (experienceId: number, signal?: AbortSignal): Promise<ExperienceSchedule[]> => {
    const response = await api.get<ExperienceSchedule[]>(
      `/host/experiences/${experienceId}/schedules`,
      { signal },
    );
    return response.data;
  },

  createSchedule: async (
    experienceId: number,
    data: CreateScheduleRequest,
  ): Promise<ExperienceSchedule> => {
    const response = await api.post<ExperienceSchedule>(
      `/host/experiences/${experienceId}/schedules`,
      data,
    );
    return response.data;
  },

  updateSchedule: async (
    id: number,
    data: UpdateScheduleRequest,
  ): Promise<ExperienceSchedule> => {
    const response = await api.put<ExperienceSchedule>(`/host/schedules/${id}`, data);
    return response.data;
  },

  removeSchedule: async (id: number): Promise<void> => {
    await api.delete(`/host/schedules/${id}`);
  },
};
