import React from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import ProtectedRoute from './ProtectedRoute';
import Experiences from '../pages/Experiences';
import Login from '../pages/Login';
import Register from '../pages/Register';
import Profile from '../pages/Profile';

export const AppRoutes: React.FC = () => {
  return (
    <Routes>
      <Route path="/experiences" element={<Experiences />} />
      <Route path="/login" element={<Login />} />
      <Route path="/register" element={<Register />} />
      
      <Route element={<ProtectedRoute />}>
        <Route path="/profile" element={<Profile />} />
      </Route>

      <Route path="/" element={<Navigate to="/experiences" replace />} />
      <Route path="*" element={<Navigate to="/experiences" replace />} />
    </Routes>
  );
};

export default AppRoutes;
