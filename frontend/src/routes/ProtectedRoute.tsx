import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';

export const ProtectedRoute = () => {
  const { isAuthenticated, isLoading, sessionExpired } = useAuth();
  const location = useLocation();

  if (isLoading) {
    return (
      <div className="route-loading" role="status">
        <span className="route-loading__spinner" aria-hidden="true" />
        <p>Verificando autenticación...</p>
      </div>
    );
  }

  if (!isAuthenticated) {
    const from = `${location.pathname}${location.search}${location.hash}`;
    return (
      <Navigate
        to="/login"
        replace
        state={{
          from,
          message: sessionExpired
            ? 'Tu sesión expiró. Inicia sesión nuevamente para continuar.'
            : 'Inicia sesión para acceder a esta página.',
        }}
      />
    );
  }

  return <Outlet />;
};

export default ProtectedRoute;
