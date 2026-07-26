self.addEventListener('push', (event) => {
  let payload = {};
  try {
    payload = event.data ? event.data.json() : {};
  } catch {
    payload = { body: event.data?.text() };
  }

  const title = payload.title || 'GoIsland';
  const options = {
    body: payload.body || 'Tienes una nueva notificación.',
    icon: '/favicon.svg?v=2',
    badge: '/favicon.svg?v=2',
    data: { actionUrl: payload.actionUrl || '/notifications' },
  };
  event.waitUntil(self.registration.showNotification(title, options));
});

self.addEventListener('notificationclick', (event) => {
  event.notification.close();
  let destination = new URL('/notifications', self.location.origin);
  try {
    const requested = new URL(event.notification.data?.actionUrl || '/notifications', self.location.origin);
    if (requested.origin === self.location.origin) destination = requested;
  } catch {
    // Usa la bandeja de notificaciones como destino seguro.
  }

  event.waitUntil((async () => {
    const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
    const existing = windows.find((client) => new URL(client.url).origin === self.location.origin);
    if (existing) {
      await existing.navigate(destination.href);
      return existing.focus();
    }
    return self.clients.openWindow(destination.href);
  })());
});
