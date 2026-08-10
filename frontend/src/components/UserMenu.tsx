import { ChevronDown, LogOut, UserRound } from 'lucide-react';
import { useCallback, useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import type { UserResponse } from '../types';

interface UserMenuProps {
  user: UserResponse;
  onLogout: () => void;
}

export const UserMenu = ({ user, onLogout }: UserMenuProps) => {
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const firstItemRef = useRef<HTMLAnchorElement>(null);

  const firstName = user.fullName ? user.fullName.split(' ')[0] : '';
  const initial = firstName ? firstName.charAt(0).toUpperCase() : '';
  const participationLabel = user.role === 'Host' ? 'Anfitrión' : 'Viajero';
  const roleLabel = user.isAdmin ? `${participationLabel} · Administrador` : participationLabel;

  const dismiss = useCallback((returnFocus = true) => {
    setOpen(false);
    if (returnFocus) triggerRef.current?.focus();
  }, []);

  useEffect(() => {
    if (!open) return;

    firstItemRef.current?.focus();

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

  return (
    <div className="user-menu" ref={containerRef}>
      <button
        ref={triggerRef}
        type="button"
        className="user-menu__trigger"
        aria-expanded={open}
        aria-haspopup="menu"
        aria-label={`Cuenta de ${firstName}`}
        onClick={() => setOpen((current) => !current)}
      >
        <span className="user-menu__avatar" aria-hidden="true">{initial}</span>
        <ChevronDown size={15} aria-hidden="true" />
      </button>

      {open && (
        <div className="user-menu__dropdown" role="menu">
          <div className="user-menu__identity">
            <strong>{user.fullName}</strong>
            <small>{user.email}</small>
            <span>{roleLabel}</span>
          </div>

          <Link
            ref={firstItemRef}
            to="/account"
            role="menuitem"
            className="user-menu__item"
            onClick={() => setOpen(false)}
          >
            <UserRound size={18} aria-hidden="true" />
            Mi perfil
          </Link>

          <button
            type="button"
            role="menuitem"
            className="user-menu__item user-menu__item--logout"
            onClick={() => {
              setOpen(false);
              onLogout();
            }}
          >
            <LogOut size={18} aria-hidden="true" />
            Cerrar sesión
          </button>
        </div>
      )}
    </div>
  );
};

export default UserMenu;
