import React from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import ProtectedRoute from './ProtectedRoute';
import RoleRoute from './RoleRoute';

const Home = React.lazy(() => import('../pages/Home'));
const Experiences = React.lazy(() => import('../pages/Experiences'));
const ExperienceMapPage = React.lazy(() => import('../pages/ExperienceMapPage'));
const ExperienceDetail = React.lazy(() => import('../pages/ExperienceDetail'));
const PublicInfoPage = React.lazy(() => import('../pages/PublicInfoPage'));
const Login = React.lazy(() => import('../pages/Login'));
const Register = React.lazy(() => import('../pages/Register'));
const ForgotPassword = React.lazy(() => import('../pages/ForgotPassword'));
const ResetPassword = React.lazy(() => import('../pages/ResetPassword'));
const AccountLayout = React.lazy(() => import('../pages/AccountLayout'));
const Profile = React.lazy(() => import('../pages/Profile'));
const ChangePassword = React.lazy(() => import('../pages/ChangePassword'));
const Reservations = React.lazy(() => import('../pages/Reservations'));
const ReservationDetail = React.lazy(() => import('../pages/ReservationDetail'));
const HostProfile = React.lazy(() => import('../pages/HostProfile'));
const Notifications = React.lazy(() => import('../pages/Notifications'));
const HostDashboard = React.lazy(() => import('../pages/HostDashboard'));
const HostExperiences = React.lazy(() => import('../pages/HostExperiences'));
const HostSchedules = React.lazy(() => import('../pages/HostSchedules'));
const HostReservations = React.lazy(() => import('../pages/HostReservations'));
const AdminModeration = React.lazy(() => import('../pages/AdminModeration'));
const NotFound = React.lazy(() => import('../pages/NotFound'));

const pageFallback = (
  <div className="container route-loading" role="status">
    Cargando…
  </div>
);

export const AppRoutes: React.FC = () => {
  return (
    <React.Suspense fallback={pageFallback}><Routes>
      <Route path="/" element={<Home />} />
      <Route path="/experiences" element={<Experiences />} />
      <Route path="/experiences/map" element={<ExperienceMapPage />} />
      <Route path="/experiences/:identifier" element={<ExperienceDetail />} />
      <Route path="/contacto" element={<PublicInfoPage page="contact" />} />
      <Route path="/privacidad" element={<PublicInfoPage page="privacy" />} />
      <Route path="/terminos" element={<PublicInfoPage page="terms" />} />
      <Route path="/cancelaciones" element={<PublicInfoPage page="cancellations" />} />
      <Route path="/login" element={<Login />} />
      <Route path="/register" element={<Register />} />
      <Route path="/forgot-password" element={<ForgotPassword />} />
      <Route path="/reset-password" element={<ResetPassword />} />
      
      <Route element={<ProtectedRoute />}>
        {/* Cuenta unificada: perfil, avisos y seguridad como secciones de una misma
            pantalla, en vez de tres páginas sueltas sin navegación entre ellas. */}
        <Route path="/account" element={<AccountLayout />}>
          <Route index element={<Profile />} />
          <Route path="notifications" element={<Notifications />} />
          <Route path="password" element={<ChangePassword />} />
        </Route>
        <Route path="/profile" element={<Navigate to="/account" replace />} />
        <Route path="/notifications" element={<Navigate to="/account/notifications" replace />} />
        <Route path="/reservations" element={<Reservations />} />
        <Route path="/reservations/:id" element={<ReservationDetail />} />
        <Route path="/host-profile" element={<HostProfile />} />

        <Route element={<RoleRoute allowedRoles={['Host']} />}>
          <Route path="/host/dashboard" element={<HostDashboard />} />
          <Route path="/host/experiences" element={<HostExperiences />} />
          <Route path="/host/experiences/:id/schedules" element={<HostSchedules />} />
          <Route path="/host/reservations" element={<HostReservations />} />
        </Route>

        <Route element={<RoleRoute allowedRoles={['Admin']} />}>
          <Route path="/admin/moderation" element={<AdminModeration />} />
        </Route>
      </Route>

      <Route path="*" element={<NotFound />} />
    </Routes></React.Suspense>
  );
};

export default AppRoutes;
