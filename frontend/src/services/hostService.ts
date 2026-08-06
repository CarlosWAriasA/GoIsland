import type {
  HostProfile,
  HostApplicationListParams,
  HostProfileRequest,
  ManagedExperience,
  ManagedExperienceListParams,
  PagedResponse,
} from '../types';
import { api } from './api';

export const hostService = {
  apply: async (data: HostProfileRequest): Promise<HostProfile> => {
    const response = await api.post<HostProfile>('/hosts/apply', data);
    return response.data;
  },

  getMine: async (signal?: AbortSignal): Promise<HostProfile> => {
    const response = await api.get<HostProfile>('/hosts/me', { signal });
    return response.data;
  },

  updateMine: async (data: HostProfileRequest): Promise<HostProfile> => {
    const response = await api.put<HostProfile>('/hosts/me', data);
    return response.data;
  },

  getApplications: async (
    params: HostApplicationListParams = {},
    signal?: AbortSignal,
  ): Promise<PagedResponse<HostProfile>> => {
    const response = await api.get<PagedResponse<HostProfile>>('/admin/hosts', {
      params,
      signal,
    });
    return response.data;
  },

  decideApplication: async (
    id: number,
    action: 'approve' | 'reject' | 'suspend',
    reason?: string,
  ): Promise<HostProfile> => {
    const response = await api.post<HostProfile>(`/admin/hosts/${id}/${action}`, { reason });
    return response.data;
  },

  getExperiencesForAdmin: async (
    params: ManagedExperienceListParams = {},
    signal?: AbortSignal,
  ): Promise<PagedResponse<ManagedExperience>> => {
    const response = await api.get<PagedResponse<ManagedExperience>>('/admin/experiences', {
      params,
      signal,
    });
    return response.data;
  },

  decideExperience: async (
    id: number,
    action: 'approve' | 'reject' | 'suspend',
    reason?: string,
  ): Promise<ManagedExperience> => {
    const response = await api.post<ManagedExperience>(
      `/admin/experiences/${id}/${action}`,
      { reason },
    );
    return response.data;
  },
};
