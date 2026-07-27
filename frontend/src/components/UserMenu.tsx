import { ChevronDown, LogOut, UserRound } from 'lucide-react';
import { useCallback, useState } from 'react';
import { Link } from 'react-router-dom';
import { useDismissable } from '../hooks/useDismissable';
import type { UserResponse } from '../types';

interface UserMenuProps {
  user: UserResponse;
  onLogout: () => void;
}

export const UserMenu = ({ user, onLogout }: UserMenuProps) => {
  const [open, setOpen] = useState(false);
  const close = useCallback(() => setOpen(false), []);
  const containerRef = useDismissable<HTMLDivElement>(open, close);

  const firstName = user.fullName.split(' ')[0];
  const initial = firstName.charAt(0).toUpperCase();

  return (
    <div className="user-menu" ref={containerRef}>
      <button
        type="button"
        className="user-menu__trigger"
        aria-expanded={open}
        aria-haspopup="menu"
        aria-controls="user-menu-panel"
        data-dismiss-focus
        onClick={() => setOpen((current) => !current)}
      >
        <span className="user-menu__avatar" aria-hidden="true">{initial}</span>
        <span className="user-menu__name">{firstName}</span>
        <ChevronDown size={16} aria-hidden="true" />
      </button>

      {open && (
        <div className="user-menu__panel surface-panel" id="user-menu-panel" role="menu">
          <p className="user-menu__identity">
            <strong>{user.fullName}</strong>
            <small>{user.email}</small>
          </p>

          <Link to="/profile" role="menuitem" className="user-menu__item" onClick={close}>
            <UserRound size={17} aria-hidden="true" /> Mi perfil
          </Link>

          <button type="button" role="menuitem" className="user-menu__item user-menu__item--danger" onClick={onLogout}>
            <LogOut size={17} aria-hidden="true" /> Cerrar sesión
          </button>
        </div>
      )}
    </div>
  );
};

export default UserMenu;
