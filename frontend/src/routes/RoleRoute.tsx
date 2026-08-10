import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';

interface RoleRouteProps {
  allowedRoles: string[];
}

export const RoleRoute = ({ allowedRoles }: RoleRouteProps) => {
  const { user, isAuthenticated, isLoading } = useAuth();

  if (isLoading) {
    return (
      <div className="route-loading" role="status">
        <span className="route-loading__spinner" aria-hidden="true" />
        <p>Cargando…</p>
      </div>
    );
  }

  if (!isAuthenticated || !user) {
    return <Navigate to="/login" replace />;
  }

  // Administrar es un permiso aparte del rol, así que quien administra también conserva el
  // acceso que le corresponde como turista o anfitrión.
  const hasAccess = allowedRoles.includes(user.role)
    || (allowedRoles.includes('Admin') && user.isAdmin);
  if (!hasAccess) {
    return <Navigate to="/account" replace />;
  }

  return <Outlet />;
};

export default RoleRoute;
