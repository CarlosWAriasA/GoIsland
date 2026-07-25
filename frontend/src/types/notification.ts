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
