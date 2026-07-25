import { api } from './api';
import type { NotificationItem, NotificationPreferences } from '../types';

export const notificationService = {
  getAll: async (signal?: AbortSignal): Promise<NotificationItem[]> =>
    (await api.get<NotificationItem[]>('/notifications', { signal })).data,
  markRead: async (id: number): Promise<NotificationItem> =>
    (await api.patch<NotificationItem>(`/notifications/${id}/read`)).data,
  getPreferences: async (signal?: AbortSignal): Promise<NotificationPreferences> =>
    (await api.get<NotificationPreferences>('/notifications/preferences', { signal })).data,
  updatePreferences: async (preferences: NotificationPreferences): Promise<NotificationPreferences> =>
    (await api.put<NotificationPreferences>('/notifications/preferences', preferences)).data,
};
