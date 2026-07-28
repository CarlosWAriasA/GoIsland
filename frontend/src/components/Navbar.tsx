import { useState } from 'react';
import { Menu, X } from 'lucide-react';
import { Link, NavLink, useNavigate } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import Button from './Button';
import Logo from './Logo';
import NotificationBell from './NotificationBell';
import UserMenu from './UserMenu';

const getNavLinkClass = ({ isActive }: { isActive: boolean }) => (
  `site-nav__link${isActive ? ' site-nav__link--active' : ''}`
);

export const Navbar = () => {
  const { user, isAuthenticated, logout } = useAuth();
  const [menuOpen, setMenuOpen] = useState(false);
  const navigate = useNavigate();

  const closeMenu = () => setMenuOpen(false);

  const handleLogout = () => {
    closeMenu();
    logout();
    navigate('/login');
  };

  return (
    <header className="site-header">
      <div className="site-header__inner">
        <Link to="/" className="site-header__brand" aria-label="GoIsland, ir al inicio" onClick={closeMenu}>
          <Logo fontSize="1.45rem" />
        </Link>

        <div className="site-header__actions">
          {isAuthenticated && <NotificationBell />}
          <Button
            variant="ghost"
            className="site-header__menu-button"
            aria-label={menuOpen ? 'Cerrar menú principal' : 'Abrir menú principal'}
            aria-expanded={menuOpen}
            aria-controls="main-navigation"
            onClick={() => setMenuOpen((current) => !current)}
          >
            {menuOpen ? <X aria-hidden="true" /> : <Menu aria-hidden="true" />}
          </Button>
        </div>

        <nav
          id="main-navigation"
          className={`site-nav${menuOpen ? ' site-nav--open' : ''}`}
          aria-label="Navegación principal"
        >
          <NavLink to="/" end className={getNavLinkClass} onClick={closeMenu}>
            Inicio
          </NavLink>

          <NavLink to="/experiences" className={getNavLinkClass} onClick={closeMenu}>
            Experiencias
          </NavLink>
          <NavLink to="/experiences/map" className={getNavLinkClass} onClick={closeMenu}>
            Mapa
          </NavLink>

          {isAuthenticated ? (
            <>
              <NavLink to="/reservations" className={getNavLinkClass} onClick={closeMenu}>
                Mis reservas
              </NavLink>

              {user?.role === 'Host' && (
                <>
                  <NavLink to="/host/dashboard" className={getNavLinkClass} onClick={closeMenu}>
                    Mi actividad
                  </NavLink>
                  <NavLink to="/host/experiences" className={getNavLinkClass} onClick={closeMenu}>
                    Mis experiencias
                  </NavLink>
                  <NavLink to="/host/reservations" className={getNavLinkClass} onClick={closeMenu}>
                    Reservas recibidas
                  </NavLink>
                </>
              )}

              {user?.role === 'Admin' && (
                <NavLink to="/admin/moderation" className={getNavLinkClass} onClick={closeMenu}>
                  Moderación
                </NavLink>
              )}

              {user?.role !== 'Host' && user?.role !== 'Admin' && (
                <NavLink to="/host-profile" className={getNavLinkClass} onClick={closeMenu}>
                  Ser anfitrión
                </NavLink>
              )}

              <div className="site-nav__session">
                <NavLink
                  to="/notifications"
                  className={({ isActive }) => `${getNavLinkClass({ isActive })} site-nav__link--mobile-only`}
                  onClick={closeMenu}
                >
                  Notificaciones
                </NavLink>
                {user && <UserMenu user={user} onLogout={handleLogout} />}
              </div>
            </>
          ) : (
            <>
              <NavLink to="/host-profile" className={getNavLinkClass} onClick={closeMenu}>
                Ser anfitrión
              </NavLink>

              <div className="site-nav__auth">
                <Link to="/login" className="site-nav__login" onClick={closeMenu}>
                  Iniciar sesión
                </Link>
                <Link to="/register" className="site-nav__register" onClick={closeMenu}>
                  Crear cuenta
                </Link>
              </div>
            </>
          )}
        </nav>
      </div>
    </header>
  );
};

export default Navbar;
