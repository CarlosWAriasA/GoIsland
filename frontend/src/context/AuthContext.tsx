import React, { useState, useEffect } from 'react';
import type { UserResponse, AuthResponse, LoginRequest, RegisterRequest } from '../types';
import { authService } from '../services/authService';
import { setAuthToken } from '../services/api';
import { AuthContext } from './AuthContextDefinition';

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [user, setUser] = useState<UserResponse | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(false);

  // Sincroniza el token con los headers de las peticiones de Axios
  useEffect(() => {
    setAuthToken(token);
  }, [token]);

  const login = async (data: LoginRequest) => {
    setIsLoading(true);
    try {
      const res: AuthResponse = await authService.login(data);
      setToken(res.token);
      setUser(res.user);
    } catch (error) {
      logout();
      throw error;
    } finally {
      setIsLoading(false);
    }
  };

  const register = async (data: RegisterRequest) => {
    setIsLoading(true);
    try {
      const res: AuthResponse = await authService.register(data);
      setToken(res.token);
      setUser(res.user);
    } catch (error) {
      logout();
      throw error;
    } finally {
      setIsLoading(false);
    }
  };

  const logout = () => {
    setToken(null);
    setUser(null);
    setAuthToken(null);
  };

  const updateUser = async (fullName: string) => {
    setIsLoading(true);
    try {
      const updatedUser = await authService.updateProfile(fullName);
      setUser(updatedUser);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <AuthContext.Provider
      value={{
        user,
        token,
        isAuthenticated: !!token,
        isLoading,
        login,
        register,
        logout,
        updateUser,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
};
