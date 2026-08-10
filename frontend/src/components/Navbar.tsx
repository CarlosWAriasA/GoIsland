import { useState } from 'react';
import { Menu, X } from 'lucide-react';
import { Link, NavLink, useLocation, useNavigate } from 'react-router-dom';
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
  const location = useLocation();
  const navigate = useNavigate();
  const experiencesActive = location.pathname === '/experiences'
    || (location.pathname.startsWith('/experiences/') && !location.pathname.startsWith('/experiences/map'));

  const closeMenu = () => setMenuOpen(false);

  const handleLogout = () => {
    closeMenu();
    logout();
    navigate('/login', { replace: true, state: { loggedOut: true } });
  };

  return (
    <header className="site-header">
      <div className="site-header__inner">
        <Link to="/" className="site-header__brand" aria-label="GoIsland, ir al inicio" onClick={closeMenu}>
          <Logo fontSize="1.45rem" />
        </Link>

        <nav
          id="main-navigation"
          className={`site-nav${menuOpen ? ' site-nav--open' : ''}`}
          aria-label="Navegación principal"
        >
          <NavLink to="/" end className={getNavLinkClass} onClick={closeMenu}>
            Inicio
          </NavLink>

          <NavLink
            to="/experiences"
            className={() => getNavLinkClass({ isActive: experiencesActive })}
            onClick={closeMenu}
          >
            Experiencias
          </NavLink>
          <NavLink to="/experiences/map" className={getNavLinkClass} onClick={closeMenu}>
            Mapa
          </NavLink>

          {isAuthenticated && (
            <>
              <NavLink to="/reservations" className={getNavLinkClass} onClick={closeMenu}>
                Mi actividad
              </NavLink>

              {user?.role === 'Host' && (
                <>
                  <NavLink to="/host/dashboard" className={getNavLinkClass} onClick={closeMenu}>
                    Panel
                  </NavLink>
                  <NavLink to="/host/experiences" className={getNavLinkClass} onClick={closeMenu}>
                    Mis experiencias
                  </NavLink>
                  <NavLink to="/host/reservations" className={getNavLinkClass} onClick={closeMenu}>
                    Reservas recibidas
                  </NavLink>
                </>
              )}

              {user?.isAdmin && (
                <NavLink to="/admin/moderation" className={getNavLinkClass} onClick={closeMenu}>
                  Moderación
                </NavLink>
              )}
            </>
          )}

          {/* En móvil el menú se despliega y estos accesos van dentro; en escritorio
              viven en el bloque de la derecha, así que aquí se ocultan. */}
          {!isAuthenticated && (
            <div className="site-nav__auth">
              <Link to="/login" className="site-nav__login" onClick={closeMenu}>
                Iniciar sesión
              </Link>
              <Link to="/register" className="site-nav__register" onClick={closeMenu}>
                Crear cuenta
              </Link>
            </div>
          )}
        </nav>

        {/* Bloque derecho: acción de captación, avisos y cuenta. Fuera del menú
            colapsable para que sigan a un clic en móvil. */}
        <div className="site-header__actions">
          {isAuthenticated && user?.role !== 'Host' && (
            <Link to="/host-profile" className="site-header__host-cta" onClick={closeMenu}>
              Ser anfitrión
            </Link>
          )}

          {isAuthenticated && user && (
            <div className="site-header__session">
              <NotificationBell />
              <UserMenu user={user} onLogout={handleLogout} />
            </div>
          )}

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
      </div>
    </header>
  );
};

export default Navbar;
