import { useState } from 'react';
import { LogOut, Menu, X } from 'lucide-react';
import { Link, NavLink, useNavigate } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import Button from './Button';
import Logo from './Logo';

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
        <Link to="/experiences" className="site-header__brand" aria-label="GoIsland, ir a experiencias" onClick={closeMenu}>
          <Logo fontSize="1.45rem" />
        </Link>

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

        <nav
          id="main-navigation"
          className={`site-nav${menuOpen ? ' site-nav--open' : ''}`}
          aria-label="Navegación principal"
        >
          <NavLink to="/experiences" className={getNavLinkClass} onClick={closeMenu}>
            Experiencias
          </NavLink>

          {isAuthenticated ? (
            <>
              <NavLink to="/reservations" className={getNavLinkClass} onClick={closeMenu}>
                Mis reservas
              </NavLink>
              {user?.role === 'Host' ? (
                <>
                  <NavLink to="/host/experiences" className={getNavLinkClass} onClick={closeMenu}>
                    Mis experiencias
                  </NavLink>
                  <NavLink to="/host/reservations" className={getNavLinkClass} onClick={closeMenu}>
                    Reservas recibidas
                  </NavLink>
                </>
              ) : user?.role !== 'Admin' ? (
                <NavLink to="/host-profile" className={getNavLinkClass} onClick={closeMenu}>
                  Ser anfitrión
                </NavLink>
              ) : null}
              {user?.role === 'Admin' && (
                <NavLink to="/admin/moderation" className={getNavLinkClass} onClick={closeMenu}>
                  Moderación
                </NavLink>
              )}
              <NavLink to="/profile" className={getNavLinkClass} onClick={closeMenu}>
                Mi perfil
              </NavLink>
              <span className="site-nav__greeting">
                Hola, <strong>{user?.fullName.split(' ')[0]}</strong>
              </span>
              <Button variant="ghost" size="sm" onClick={handleLogout}>
                <LogOut size={17} aria-hidden="true" />
                Salir
              </Button>
            </>
          ) : (
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
      </div>
    </header>
  );
};

export default Navbar;
