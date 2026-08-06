import { api } from './api';
import type {
  NotificationItem,
  NotificationPreferences,
  PagedResponse,
  RegisterWebPushSubscription,
  WebPushDevice,
  WebPushPublicKeyResponse,
} from '../types';

export const NOTIFICATIONS_CHANGED_EVENT = 'goisland:notifications-changed';

const announceNotificationsChanged = () => {
  if (typeof window !== 'undefined') {
    window.dispatchEvent(new Event(NOTIFICATIONS_CHANGED_EVENT));
  }
};

export const notificationService = {
  getAll: async (signal?: AbortSignal): Promise<PagedResponse<NotificationItem>> =>
    (await api.get<PagedResponse<NotificationItem>>('/notifications', {
      params: { pageSize: 100 },
      signal,
    })).data,
  getUnreadCount: async (signal?: AbortSignal): Promise<number> =>
    (await api.get<PagedResponse<NotificationItem>>('/notifications', {
      params: { unreadOnly: true, pageSize: 1 },
      signal,
    })).data.totalItems,
  markRead: async (id: number): Promise<NotificationItem> => {
    const item = (await api.patch<NotificationItem>(`/notifications/${id}/read`)).data;
    announceNotificationsChanged();
    return item;
  },
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
