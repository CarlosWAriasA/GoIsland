import type { HostDashboard } from '../types';
import { api } from './api';

export const hostDashboardService = {
  get: async (signal?: AbortSignal): Promise<HostDashboard> => {
    const response = await api.get<HostDashboard>('/host/dashboard', { signal });
    return response.data;
  },
};
