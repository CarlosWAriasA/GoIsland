import React from 'react';
import { Link, useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import Button from './Button';
import Logo from './Logo';

export const Navbar: React.FC = () => {
  const { user, isAuthenticated, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  const isActive = (path: string) => location.pathname === path;

  const getNavLinkStyle = (path: string): React.CSSProperties => {
    const active = isActive(path);
    return {
      fontSize: '0.95rem',
      fontWeight: 600,
      color: active ? 'var(--color-primary)' : 'var(--color-text)',
      padding: '8px 0',
      borderBottom: active ? '2px solid var(--color-gold)' : '2px solid transparent',
      transition: 'all 0.2s ease',
      display: 'inline-block',
      fontFamily: "'Poppins', sans-serif",
    };
  };

  return (
    <header style={{
      position: 'sticky',
      top: 0,
      left: 0,
      right: 0,
      zIndex: 100,
      background: 'var(--color-white)',
      borderBottom: '1px solid var(--color-border)',
      boxShadow: '0 2px 10px rgba(0, 0, 0, 0.04)',
    }}>
      <div style={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        height: '70px',
        padding: '0 24px',
        width: '100%',
      }}>
        <Link to="/" style={{ display: 'flex', alignItems: 'center' }}>
          <Logo fontSize="1.45rem" />
        </Link>

        <nav style={{
          display: 'flex',
          alignItems: 'center',
          gap: '24px',
        }}>
          <Link
            to="/experiences"
            style={getNavLinkStyle('/experiences')}
            className="nav-link-item"
          >
            Experiencias
          </Link>

          {isAuthenticated ? (
            <>
              <Link
                to="/profile"
                style={getNavLinkStyle('/profile')}
                className="nav-link-item"
              >
                Mi Perfil
              </Link>
              
              <div style={{ display: 'flex', alignItems: 'center', gap: '16px', marginLeft: '12px' }}>
                <span style={{
                  fontSize: '0.85rem',
                  color: 'var(--color-text-muted)',
                  borderLeft: '1px solid var(--color-border)',
                  paddingLeft: '16px',
                  fontFamily: "'Poppins', sans-serif",
                }}>
                  Hola, <strong style={{ color: 'var(--color-primary)' }}>{user?.fullName.split(' ')[0]}</strong>
                </span>
                
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={handleLogout}
                  style={{
                    padding: '6px 12px',
                    fontSize: '0.85rem',
                  }}
                >
                  Salir
                </Button>
              </div>
            </>
          ) : (
            <div style={{ display: 'flex', alignItems: 'center', gap: '12px', marginLeft: '12px' }}>
              <Link to="/login">
                <Button variant="ghost" size="sm">
                  Iniciar Sesión
                </Button>
              </Link>
              <Link to="/register">
                <Button variant="primary" size="sm">
                  Registrarse
                </Button>
              </Link>
            </div>
          )}
        </nav>
      </div>
      <style>{`
        .nav-link-item:hover {
          color: var(--color-primary) !important;
          border-bottom-color: var(--color-gold) !important;
        }
      `}</style>
    </header>
  );
};
export default Navbar;
