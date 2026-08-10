import { Bell, Settings } from 'lucide-react';
import { useCallback, useEffect, useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import {
  NOTIFICATIONS_CHANGED_EVENT,
  notificationService,
} from '../services/notificationService';
import AccountNotifications from './AccountNotifications';

export const NotificationBell = () => {
  const [open, setOpen] = useState(false);
  const [unreadCount, setUnreadCount] = useState(0);
  const containerRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const navigate = useNavigate();

  const refreshUnreadCount = useCallback(async () => {
    try {
      setUnreadCount(await notificationService.getUnreadCount());
    } catch {
      // El resto de la barra debe seguir usable si el contador falla.
    }
  }, []);

  useEffect(() => {
    const initialRefresh = window.setTimeout(() => void refreshUnreadCount(), 0);
    const refreshInterval = window.setInterval(() => void refreshUnreadCount(), 60_000);
    const handleVisibilityChange = () => {
      if (document.visibilityState === 'visible') void refreshUnreadCount();
    };

    window.addEventListener('focus', refreshUnreadCount);
    window.addEventListener(NOTIFICATIONS_CHANGED_EVENT, refreshUnreadCount);
    document.addEventListener('visibilitychange', handleVisibilityChange);
    return () => {
      window.clearTimeout(initialRefresh);
      window.clearInterval(refreshInterval);
      window.removeEventListener('focus', refreshUnreadCount);
      window.removeEventListener(NOTIFICATIONS_CHANGED_EVENT, refreshUnreadCount);
      document.removeEventListener('visibilitychange', handleVisibilityChange);
    };
  }, [refreshUnreadCount]);

  const dismiss = useCallback((returnFocus = true) => {
    setOpen(false);
    if (returnFocus) triggerRef.current?.focus();
  }, []);

  useEffect(() => {
    if (!open) return;

    const handlePointerDown = (event: MouseEvent) => {
      if (!containerRef.current?.contains(event.target as Node)) dismiss(false);
    };
    const handleKeyDown = (event: globalThis.KeyboardEvent) => {
      if (event.key === 'Escape') dismiss();
    };

    document.addEventListener('mousedown', handlePointerDown);
    document.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('mousedown', handlePointerDown);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [dismiss, open]);

  const openPanel = () => {
    setOpen((current) => {
      if (!current) void refreshUnreadCount();
      return !current;
    });
  };

  const navigateFromPanel = (path: string) => {
    setOpen(false);
    navigate(path);
  };

  return (
    <div className="notification-bell" ref={containerRef}>
      <button
        ref={triggerRef}
        type="button"
        className="notification-bell__trigger"
        aria-expanded={open}
        aria-haspopup="dialog"
        aria-label={unreadCount > 0
          ? `Notificaciones, ${unreadCount} sin leer`
          : 'Notificaciones'}
        onClick={openPanel}
      >
        <Bell size={20} aria-hidden="true" />
        {unreadCount > 0 && (
          <span className="notification-bell__badge" aria-hidden="true">
            {unreadCount > 9 ? '9+' : unreadCount}
          </span>
        )}
      </button>

      {open && (
        <div className="notification-bell__panel" role="dialog" aria-label="Notificaciones">
          <div className="notification-bell__header">
            <h2>Notificaciones</h2>
            <Link
              to="/account/notifications"
              className="notification-bell__settings"
              onClick={() => setOpen(false)}
              aria-label="Configurar notificaciones"
              title="Configurar notificaciones"
            >
              <Settings size={18} aria-hidden="true" />
            </Link>
          </div>

          <AccountNotifications onNavigate={navigateFromPanel} />
        </div>
      )}
    </div>
  );
};

export default NotificationBell;
