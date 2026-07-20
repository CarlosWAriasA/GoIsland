import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';

interface RoleRouteProps {
  allowedRoles: string[];
}

export const RoleRoute = ({ allowedRoles }: RoleRouteProps) => {
  const { user, isLoading } = useAuth();

  if (isLoading) {
    return (
      <div className="route-loading" role="status">
        <span className="route-loading__spinner" aria-hidden="true" />
        <p>Verificando permisos...</p>
      </div>
    );
  }

  if (!user || !allowedRoles.includes(user.role)) {
    return <Navigate to="/profile" replace />;
  }

  return <Outlet />;
};

export default RoleRoute;
