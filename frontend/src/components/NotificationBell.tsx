import axios from 'axios';
import { Bell, CheckCheck } from 'lucide-react';
import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useDismissable } from '../hooks/useDismissable';
import { notificationService } from '../services/notificationService';
import type { NotificationItem } from '../types';

const PREVIEW_LIMIT = 5;

const formatDate = (date: string) => new Intl.DateTimeFormat('es-DO', {
  day: 'numeric', month: 'short', hour: 'numeric', minute: '2-digit',
}).format(new Date(date));

export const NotificationBell = () => {
  const navigate = useNavigate();
  const [items, setItems] = useState<NotificationItem[]>([]);
  const [open, setOpen] = useState(false);
  const [failed, setFailed] = useState(false);
  const [markingAll, setMarkingAll] = useState(false);

  const close = useCallback(() => setOpen(false), []);
  const containerRef = useDismissable<HTMLDivElement>(open, close);

  useEffect(() => {
    const controller = new AbortController();
    notificationService.getAll(controller.signal)
      .then((data) => {
        setItems(data);
        setFailed(false);
      })
      .catch((error: unknown) => {
        if (!axios.isCancel(error)) setFailed(true);
      });
    return () => controller.abort();
  }, []);

  const unread = items.filter((item) => !item.readAt);
  const preview = items.slice(0, PREVIEW_LIMIT);

  const openNotification = async (item: NotificationItem) => {
    close();
    if (!item.readAt) {
      try {
        const updated = await notificationService.markRead(item.id);
        setItems((current) => current.map((entry) => entry.id === item.id ? updated : entry));
      } catch {
        void 0;
      }
    }
    navigate(item.actionUrl && item.actionUrl.startsWith('/') ? item.actionUrl : '/notifications');
  };

  const markAllRead = async () => {
    if (unread.length === 0) return;
    setMarkingAll(true);
    try {
      const updated = await Promise.all(unread.map((item) => notificationService.markRead(item.id)));
      setItems((current) => current.map(
        (entry) => updated.find((item) => item.id === entry.id) ?? entry,
      ));
    } catch {
      void 0;
    } finally {
      setMarkingAll(false);
    }
  };

  if (failed) return null;

  return (
    <div className="notification-bell" ref={containerRef}>
      <button
        type="button"
        className="notification-bell__trigger"
        aria-expanded={open}
        aria-haspopup="menu"
        aria-controls="notification-bell-panel"
        aria-label={unread.length > 0
          ? `Notificaciones, ${unread.length} sin leer`
          : 'Notificaciones'}
        data-dismiss-focus
        onClick={() => setOpen((current) => !current)}
      >
        <Bell size={20} aria-hidden="true" />
        {unread.length > 0 && (
          <span className="notification-bell__badge" aria-hidden="true">
            {unread.length > 9 ? '9+' : unread.length}
          </span>
        )}
      </button>

      {open && (
        <div className="notification-bell__panel surface-panel" id="notification-bell-panel" role="menu">
          <div className="notification-bell__header">
            <h2>Notificaciones</h2>
            {unread.length > 0 && (
              <button
                type="button"
                className="notification-bell__mark-all"
                onClick={() => void markAllRead()}
                disabled={markingAll}
              >
                <CheckCheck size={15} aria-hidden="true" />
                Marcar todas como leídas
              </button>
            )}
          </div>

          {preview.length === 0 ? (
            <p className="notification-bell__empty">Todavía no tienes avisos.</p>
          ) : (
            <ul className="notification-bell__list">
              {preview.map((item) => (
                <li key={item.id}>
                  <button
                    type="button"
                    role="menuitem"
                    className={`notification-bell__item${item.readAt ? '' : ' notification-bell__item--unread'}`}
                    onClick={() => void openNotification(item)}
                  >
                    <span className="notification-bell__item-title">{item.title}</span>
                    <span className="notification-bell__item-message">{item.message}</span>
                    <span className="notification-bell__item-date">{formatDate(item.createdAt)}</span>
                  </button>
                </li>
              ))}
            </ul>
          )}

          <button
            type="button"
            role="menuitem"
            className="notification-bell__all"
            onClick={() => { close(); navigate('/notifications'); }}
          >
            Ver todas y ajustar preferencias
          </button>
        </div>
      )}
    </div>
  );
};

export default NotificationBell;
