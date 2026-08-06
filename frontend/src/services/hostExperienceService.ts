import type {
  CreateScheduleRequest,
  CopyScheduleWeekRequest,
  ExperienceSchedule,
  ExperienceImage,
  ManagedExperienceListParams,
  ManagedExperience,
  ManagedExperienceRequest,
  UpdateScheduleRequest,
  PagedResponse,
  RecurringScheduleGeneration,
  RecurringSchedulePreview,
  RecurringScheduleRequest,
  ScheduleBatchResponse,
} from '../types';
import { api } from './api';

export const hostExperienceService = {
  getMine: async (
    params: ManagedExperienceListParams = {},
    signal?: AbortSignal,
  ): Promise<PagedResponse<ManagedExperience>> => {
    const response = await api.get<PagedResponse<ManagedExperience>>('/host/experiences', {
      params,
      signal,
    });
    return response.data;
  },

  getOne: async (id: number, signal?: AbortSignal): Promise<ManagedExperience> => {
    const response = await api.get<ManagedExperience>(`/host/experiences/${id}`, { signal });
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

  uploadImages: async (
    id: number,
    images: Array<{ file: File; altText: string; isCover: boolean }>,
  ): Promise<ExperienceImage[]> => {
    const data = new FormData();
    images.forEach(({ file, altText }) => {
      data.append('files', file);
      data.append('altTexts', altText);
    });
    const coverIndex = images.findIndex((image) => image.isCover);
    if (coverIndex >= 0) data.append('coverIndex', String(coverIndex));
    const response = await api.post<ExperienceImage[]>(`/host/experiences/${id}/images`, data, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
    return response.data;
  },

  updateImage: async (
    experienceId: number,
    imageId: number,
    data: { altText: string; isCover: boolean },
  ): Promise<ExperienceImage[]> => {
    const response = await api.patch<ExperienceImage[]>(
      `/host/experiences/${experienceId}/images/${imageId}`,
      data,
    );
    return response.data;
  },

  deleteImage: async (experienceId: number, imageId: number): Promise<ExperienceImage[]> => {
    const response = await api.delete<ExperienceImage[]>(
      `/host/experiences/${experienceId}/images/${imageId}`,
    );
    return response.data;
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

  previewRecurringSchedules: async (
    experienceId: number,
    data: RecurringScheduleRequest,
  ): Promise<RecurringSchedulePreview> => {
    const response = await api.post<RecurringSchedulePreview>(
      `/host/experiences/${experienceId}/schedules/recurring/preview`,
      data,
    );
    return response.data;
  },

  generateRecurringSchedules: async (
    experienceId: number,
    data: RecurringScheduleRequest,
  ): Promise<RecurringScheduleGeneration> => {
    const response = await api.post<RecurringScheduleGeneration>(
      `/host/experiences/${experienceId}/schedules/recurring`,
      data,
    );
    return response.data;
  },

  previewCopyWeek: async (
    experienceId: number,
    data: CopyScheduleWeekRequest,
  ): Promise<RecurringSchedulePreview> => {
    const response = await api.post<RecurringSchedulePreview>(
      `/host/experiences/${experienceId}/schedules/copy-week/preview`,
      data,
    );
    return response.data;
  },

  copyScheduleWeek: async (
    experienceId: number,
    data: CopyScheduleWeekRequest,
  ): Promise<RecurringScheduleGeneration> => {
    const response = await api.post<RecurringScheduleGeneration>(
      `/host/experiences/${experienceId}/schedules/copy-week`,
      data,
    );
    return response.data;
  },

  closeSchedules: async (
    experienceId: number,
    scheduleIds: number[],
  ): Promise<ScheduleBatchResponse> => {
    const response = await api.patch<ScheduleBatchResponse>(
      `/host/experiences/${experienceId}/schedules/batch/close`,
      { scheduleIds },
    );
    return response.data;
  },

  updateSchedulesCapacity: async (
    experienceId: number,
    scheduleIds: number[],
    capacity: number,
  ): Promise<ScheduleBatchResponse> => {
    const response = await api.patch<ScheduleBatchResponse>(
      `/host/experiences/${experienceId}/schedules/batch/capacity`,
      { scheduleIds, capacity },
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
