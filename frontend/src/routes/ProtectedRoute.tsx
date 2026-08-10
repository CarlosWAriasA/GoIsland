import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';

export const ProtectedRoute = () => {
  const { isAuthenticated, isLoading, sessionExpired, signedOut } = useAuth();
  const location = useLocation();

  if (isLoading) {
    return (
      <div className="route-loading" role="status">
        <span className="route-loading__spinner" aria-hidden="true" />
        <p>Cargando…</p>
      </div>
    );
  }

  if (!isAuthenticated) {
    // Quien cierra sesión ya sabe por qué sale de la página: no hay nada que avisarle ni a
    // dónde devolverlo cuando vuelva a entrar.
    if (signedOut) {
      return <Navigate to="/login" replace state={{ loggedOut: true }} />;
    }

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
