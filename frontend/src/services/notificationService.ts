import { api } from './api';
import type {
  NotificationItem,
  NotificationPreferences,
  RegisterWebPushSubscription,
  WebPushDevice,
  WebPushPublicKeyResponse,
} from '../types';

export const notificationService = {
  getAll: async (signal?: AbortSignal): Promise<NotificationItem[]> =>
    (await api.get<NotificationItem[]>('/notifications', { signal })).data,
  markRead: async (id: number): Promise<NotificationItem> =>
    (await api.patch<NotificationItem>(`/notifications/${id}/read`)).data,
  getPreferences: async (signal?: AbortSignal): Promise<NotificationPreferences> =>
    (await api.get<NotificationPreferences>('/notifications/preferences', { signal })).data,
  updatePreferences: async (preferences: NotificationPreferences): Promise<NotificationPreferences> =>
    (await api.put<NotificationPreferences>('/notifications/preferences', preferences)).data,
  getWebPushPublicKey: async (): Promise<string> =>
    (await api.get<WebPushPublicKeyResponse>('/devices/web-push-public-key')).data.publicKey,
  registerWebPushSubscription: async (subscription: RegisterWebPushSubscription): Promise<WebPushDevice> =>
    (await api.post<WebPushDevice>('/devices', subscription)).data,
  deleteWebPushSubscription: async (id: number): Promise<void> => {
    await api.delete(`/devices/${id}`);
  },
};
