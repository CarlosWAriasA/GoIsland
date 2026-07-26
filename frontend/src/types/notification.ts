export interface NotificationItem {
  id: number;
  type: string;
  title: string;
  message: string;
  actionUrl: string | null;
  readAt: string | null;
  createdAt: string;
}

export interface NotificationPreferences {
  dashboardEnabled: boolean;
  emailEnabled: boolean;
  pushEnabled: boolean;
}

export interface WebPushPublicKeyResponse {
  publicKey: string;
}

export interface RegisterWebPushSubscription {
  endpoint: string;
  p256dh: string;
  auth: string;
  expirationTime: string | null;
}

export interface WebPushDevice {
  id: number;
  expirationTime: string | null;
  lastSeenAt: string;
}
