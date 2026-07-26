import { notificationService } from './notificationService';

const DEVICE_ID_KEY = 'goisland.webPushSubscriptionId';

export type WebPushStatus = 'active' | 'denied' | 'inactive' | 'unsupported';

const isSupported = () =>
  'serviceWorker' in navigator
  && 'PushManager' in window
  && 'Notification' in window;

const urlBase64ToUint8Array = (value: string) => {
  const padding = '='.repeat((4 - value.length % 4) % 4);
  const base64 = (value + padding).replace(/-/g, '+').replace(/_/g, '/');
  const raw = window.atob(base64);
  const output = new Uint8Array(raw.length);
  for (let index = 0; index < raw.length; index += 1) output[index] = raw.charCodeAt(index);
  return output;
};

export const getWebPushStatus = async (): Promise<WebPushStatus> => {
  if (!isSupported()) return 'unsupported';
  if (Notification.permission === 'denied') return 'denied';
  const registration = await navigator.serviceWorker.getRegistration();
  const subscription = await registration?.pushManager.getSubscription();
  return subscription ? 'active' : 'inactive';
};

export const activateWebPush = async (): Promise<WebPushStatus> => {
  if (!isSupported()) return 'unsupported';
  if (Notification.permission === 'denied') return 'denied';

  const publicKey = await notificationService.getWebPushPublicKey();
  const registration = await navigator.serviceWorker.register('/push-sw.js');
  const permission = await Notification.requestPermission();
  if (permission !== 'granted') return permission === 'denied' ? 'denied' : 'inactive';

  const subscription = await registration.pushManager.getSubscription()
    ?? await registration.pushManager.subscribe({
      userVisibleOnly: true,
      applicationServerKey: urlBase64ToUint8Array(publicKey),
    });
  const serialized = subscription.toJSON();
  if (!serialized.endpoint || !serialized.keys?.p256dh || !serialized.keys.auth) {
    throw new Error('No fue posible preparar los avisos para este dispositivo.');
  }

  const device = await notificationService.registerWebPushSubscription({
    endpoint: serialized.endpoint,
    p256dh: serialized.keys.p256dh,
    auth: serialized.keys.auth,
    expirationTime: subscription.expirationTime
      ? new Date(subscription.expirationTime).toISOString()
      : null,
  });
  window.localStorage.setItem(DEVICE_ID_KEY, String(device.id));
  return 'active';
};

export const deactivateWebPush = async (): Promise<void> => {
  if (!isSupported()) return;
  const registration = await navigator.serviceWorker.getRegistration();
  const subscription = await registration?.pushManager.getSubscription();
  await subscription?.unsubscribe();

  const storedId = window.localStorage.getItem(DEVICE_ID_KEY);
  window.localStorage.removeItem(DEVICE_ID_KEY);
  const deviceId = storedId ? Number(storedId) : Number.NaN;
  if (Number.isInteger(deviceId) && deviceId > 0) {
    await notificationService.deleteWebPushSubscription(deviceId);
  }
};
