import axios from 'axios';
import { CheckCheck, Inbox } from 'lucide-react';
import { useEffect, useState } from 'react';
import { toApiError } from '../services/apiError';
import { notificationService } from '../services/notificationService';
import type { NotificationItem } from '../types';

interface AccountNotificationsProps {
  onNavigate: (path: string) => void;
}

const formatDate = (date: string) => new Intl.DateTimeFormat('es-DO', {
  day: 'numeric',
  month: 'short',
  hour: 'numeric',
  minute: '2-digit',
}).format(new Date(date));

export const AccountNotifications = ({ onNavigate }: AccountNotificationsProps) => {
  const [items, setItems] = useState<NotificationItem[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [markingAll, setMarkingAll] = useState(false);
  const [reloadCount, setReloadCount] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    notificationService.getAll(controller.signal)
      .then((notifications) => {
        setItems(notifications.items);
        setError(null);
      })
      .catch((requestError: unknown) => {
        if (!axios.isCancel(requestError)) {
          setError(toApiError(
            requestError,
            'No fue posible cargar las notificaciones.',
          ).message);
        }
      });
    return () => controller.abort();
  }, [reloadCount]);

  const unread = items?.filter((item) => !item.readAt) ?? [];

  const openNotification = async (item: NotificationItem) => {
    if (!item.readAt) {
      try {
        const updated = await notificationService.markRead(item.id);
        setItems((current) => current?.map(
          (entry) => entry.id === updated.id ? updated : entry,
        ) ?? current);
      } catch {
        // El detalle sigue disponible aunque no se pueda actualizar el estado.
      }
    }

    if (item.actionUrl?.startsWith('/')) onNavigate(item.actionUrl);
  };

  const markAllRead = async () => {
    if (unread.length === 0) return;
    setMarkingAll(true);
    try {
      const updated = await Promise.all(
        unread.map((item) => notificationService.markRead(item.id)),
      );
      setItems((current) => current?.map(
        (entry) => updated.find((item) => item.id === entry.id) ?? entry,
      ) ?? current);
      setError(null);
    } catch (requestError: unknown) {
      setError(toApiError(
        requestError,
        'No fue posible marcar todas las notificaciones.',
      ).message);
    } finally {
      setMarkingAll(false);
    }
  };

  return (
    <section className="account-notifications" aria-label="Notificaciones recientes">
      <div className="account-notifications__summary">
        <div>
          <strong>{unread.length > 0 ? `${unread.length} sin leer` : 'Todo al día'}</strong>
          <span>Reservas, pagos y actividad de tu cuenta</span>
        </div>
        {unread.length > 0 && (
          <button
            type="button"
            className="account-notifications__mark-all"
            onClick={() => void markAllRead()}
            disabled={markingAll}
          >
            <CheckCheck size={17} aria-hidden="true" />
            {markingAll ? 'Marcando…' : 'Marcar leídas'}
          </button>
        )}
      </div>

      {error && (
        <div className="account-notifications__error" role="alert">
          <span>{error}</span>
          <button type="button" onClick={() => setReloadCount((current) => current + 1)}>
            Reintentar
          </button>
        </div>
      )}

      {items === null ? (
        <div className="account-notifications__loading" role="status" aria-label="Cargando notificaciones">
          <span />
          <span />
          <span />
        </div>
      ) : items.length === 0 ? (
        <div className="account-notifications__empty">
          <span><Inbox size={25} aria-hidden="true" /></span>
          <strong>Aún no hay notificaciones</strong>
          <p>Cuando haya novedades, aparecerán aquí.</p>
        </div>
      ) : (
        <ul className="account-notifications__list">
          {items.map((item) => (
            <li key={item.id}>
              <button
                type="button"
                className={`account-notifications__item${item.readAt ? '' : ' account-notifications__item--unread'}`}
                onClick={() => void openNotification(item)}
              >
                <span className="account-notifications__item-heading">
                  <strong>{item.title}</strong>
                  {!item.readAt && <span aria-label="Sin leer" />}
                </span>
                <span className="account-notifications__message">{item.message}</span>
                <time dateTime={item.createdAt}>{formatDate(item.createdAt)}</time>
              </button>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
};

export default AccountNotifications;
