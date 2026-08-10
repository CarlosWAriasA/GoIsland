import {
  ArrowLeft,
  Bell,
  ChevronRight,
  LogOut,
  Settings,
  UserRound,
  X,
} from 'lucide-react';
import { useCallback, useEffect, useRef, useState } from 'react';
import type { KeyboardEvent } from 'react';
import { createPortal } from 'react-dom';
import { Link, useNavigate } from 'react-router-dom';
import {
  NOTIFICATIONS_CHANGED_EVENT,
  notificationService,
} from '../services/notificationService';
import type { UserResponse } from '../types';
import AccountNotifications from './AccountNotifications';

interface UserMenuProps {
  user: UserResponse;
  onLogout: () => void;
}

export const UserMenu = ({ user, onLogout }: UserMenuProps) => {
  const [open, setOpen] = useState(false);
  const [drawerView, setDrawerView] = useState<'account' | 'notifications'>('account');
  const [unreadCount, setUnreadCount] = useState(0);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const closeButtonRef = useRef<HTMLButtonElement>(null);
  const backButtonRef = useRef<HTMLButtonElement>(null);
  const navigate = useNavigate();

  const firstName = user?.fullName ? user.fullName.split(' ')[0] : '';
  const initial = firstName ? firstName.charAt(0).toUpperCase() : '';
  const participationLabel = user?.role === 'Host' ? 'Anfitrión' : 'Viajero';
  const roleLabel = user?.isAdmin
    ? `${participationLabel} · Administrador`
    : participationLabel;

  const refreshUnreadCount = useCallback(async () => {
    try {
      setUnreadCount(await notificationService.getUnreadCount());
    } catch {
      // La navegación de la cuenta debe seguir disponible si los avisos fallan.
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

  const dismiss = useCallback(() => {
    setOpen(false);
    setDrawerView('account');
    window.requestAnimationFrame(() => triggerRef.current?.focus());
  }, []);

  useEffect(() => {
    if (!open) return;

    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    closeButtonRef.current?.focus();

    const handleEscape = (event: globalThis.KeyboardEvent) => {
      if (event.key === 'Escape') dismiss();
    };
    document.addEventListener('keydown', handleEscape);
    return () => {
      document.body.style.overflow = previousOverflow;
      document.removeEventListener('keydown', handleEscape);
    };
  }, [dismiss, open]);

  useEffect(() => {
    if (open && drawerView === 'notifications') {
      window.requestAnimationFrame(() => backButtonRef.current?.focus());
    }
  }, [drawerView, open]);

  const openDrawer = () => {
    setDrawerView('account');
    setOpen(true);
    void refreshUnreadCount();
  };

  const closeForNavigation = () => {
    setOpen(false);
    setDrawerView('account');
  };

  const navigateFromDrawer = (path: string) => {
    closeForNavigation();
    navigate(path);
  };

  const keepFocusInside = (event: KeyboardEvent<HTMLElement>) => {
    if (event.key !== 'Tab') return;
    const focusable = Array.from(event.currentTarget.querySelectorAll<HTMLElement>(
      'a[href], button:not([disabled]), [tabindex]:not([tabindex="-1"])',
    ));
    if (focusable.length === 0) return;

    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  };

  return (
    <div className="user-menu">
      <button
        ref={triggerRef}
        type="button"
        className="user-menu__trigger"
        aria-expanded={open}
        aria-haspopup="dialog"
        aria-controls="account-drawer"
        aria-label={unreadCount > 0
          ? `Abrir cuenta de ${firstName}, ${unreadCount} notificaciones sin leer`
          : `Abrir cuenta de ${firstName}`}
        onClick={openDrawer}
      >
        <span className="user-menu__avatar" aria-hidden="true">
          {initial}
          {unreadCount > 0 && <span className="user-menu__notification-dot" />}
        </span>
        <span className="user-menu__name">{firstName}</span>
        <ChevronRight size={16} aria-hidden="true" />
      </button>

      {open && createPortal(
        <div className="account-drawer__backdrop" onMouseDown={dismiss}>
          <aside
            id="account-drawer"
            className={`account-drawer account-drawer--${drawerView}`}
            role="dialog"
            aria-modal="true"
            aria-labelledby="account-drawer-title"
            onKeyDown={keepFocusInside}
            onMouseDown={(event) => event.stopPropagation()}
          >
            {drawerView === 'account' ? (
              <>
                <div className="account-drawer__header">
                  <span className="account-drawer__eyebrow">Tu cuenta</span>
                  <button
                    ref={closeButtonRef}
                    type="button"
                    className="account-drawer__icon-button"
                    aria-label="Cerrar panel de cuenta"
                    onClick={dismiss}
                  >
                    <X size={21} aria-hidden="true" />
                  </button>
                </div>

                <div className="account-drawer__identity">
                  <span className="account-drawer__avatar" aria-hidden="true">{initial}</span>
                  <div>
                    <h2 id="account-drawer-title">{user.fullName}</h2>
                    <p>{user.email}</p>
                    <span>{roleLabel}</span>
                  </div>
                </div>

                <nav className="account-drawer__navigation" aria-label="Opciones de cuenta">
                  <Link to="/profile" className="account-drawer__item" onClick={closeForNavigation}>
                    <span className="account-drawer__item-icon"><UserRound size={20} aria-hidden="true" /></span>
                    <span>
                      <strong>Mi perfil</strong>
                      <small>Datos personales y seguridad</small>
                    </span>
                    <ChevronRight size={18} aria-hidden="true" />
                  </Link>

                  <button
                    type="button"
                    className="account-drawer__item"
                    onClick={() => {
                      setDrawerView('notifications');
                      void refreshUnreadCount();
                    }}
                  >
                    <span className="account-drawer__item-icon"><Bell size={20} aria-hidden="true" /></span>
                    <span>
                      <strong>Notificaciones</strong>
                      <small>
                        {unreadCount > 0
                          ? `${unreadCount} ${unreadCount === 1 ? 'aviso pendiente' : 'avisos pendientes'}`
                          : 'No tienes avisos pendientes'}
                      </small>
                    </span>
                    {unreadCount > 0
                      ? <span className="account-drawer__badge">{unreadCount > 99 ? '99+' : unreadCount}</span>
                      : <ChevronRight size={18} aria-hidden="true" />}
                  </button>
                </nav>

                <div className="account-drawer__footer">
                  <button type="button" className="account-drawer__logout" onClick={onLogout}>
                    <LogOut size={19} aria-hidden="true" />
                    Cerrar sesión
                  </button>
                </div>
              </>
            ) : (
              <>
                <div className="account-drawer__header account-drawer__header--notifications">
                  <button
                    ref={backButtonRef}
                    type="button"
                    className="account-drawer__icon-button"
                    aria-label="Volver a las opciones de cuenta"
                    onClick={() => setDrawerView('account')}
                  >
                    <ArrowLeft size={21} aria-hidden="true" />
                  </button>
                  <div className="account-drawer__title">
                    <span className="account-drawer__eyebrow">Centro de avisos</span>
                    <h2 id="account-drawer-title">Notificaciones</h2>
                  </div>
                  <div className="account-drawer__header-actions">
                    <button
                      type="button"
                      className="account-drawer__icon-button"
                      aria-label="Configurar notificaciones"
                      title="Configurar notificaciones"
                      onClick={() => navigateFromDrawer('/notifications')}
                    >
                      <Settings size={20} aria-hidden="true" />
                    </button>
                    <button
                      ref={closeButtonRef}
                      type="button"
                      className="account-drawer__icon-button"
                      aria-label="Cerrar panel de cuenta"
                      onClick={dismiss}
                    >
                      <X size={21} aria-hidden="true" />
                    </button>
                  </div>
                </div>

                <AccountNotifications onNavigate={navigateFromDrawer} />
              </>
            )}
          </aside>
        </div>,
        document.body,
      )}
    </div>
  );
};

export default UserMenu;
