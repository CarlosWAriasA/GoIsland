import React from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import ProtectedRoute from './ProtectedRoute';
import Experiences from '../pages/Experiences';
import Login from '../pages/Login';
import Register from '../pages/Register';
import Profile from '../pages/Profile';
import ChangePassword from '../pages/ChangePassword';
import ForgotPassword from '../pages/ForgotPassword';
import ResetPassword from '../pages/ResetPassword';
import ExperienceDetail from '../pages/ExperienceDetail';
import Reservations from '../pages/Reservations';
import ReservationDetail from '../pages/ReservationDetail';

export const AppRoutes: React.FC = () => {
  return (
    <Routes>
      <Route path="/experiences" element={<Experiences />} />
      <Route path="/experiences/:id" element={<ExperienceDetail />} />
      <Route path="/login" element={<Login />} />
      <Route path="/register" element={<Register />} />
      <Route path="/forgot-password" element={<ForgotPassword />} />
      <Route path="/reset-password" element={<ResetPassword />} />
      
      <Route element={<ProtectedRoute />}>
        <Route path="/profile" element={<Profile />} />
        <Route path="/account/password" element={<ChangePassword />} />
        <Route path="/reservations" element={<Reservations />} />
        <Route path="/reservations/:id" element={<ReservationDetail />} />
      </Route>

      <Route path="/" element={<Navigate to="/experiences" replace />} />
      <Route path="*" element={<Navigate to="/experiences" replace />} />
    </Routes>
  );
};

export default AppRoutes;
