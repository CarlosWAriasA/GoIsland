import React, { useCallback, useEffect, useState } from 'react';
import type { UserResponse, AuthResponse, LoginRequest, RegisterRequest } from '../types';
import { authService } from '../services/authService';
import { setAuthToken, setUnauthorizedHandler } from '../services/api';
import { clearAuthSession, loadAuthSession, saveAuthSession } from '../services/authSession';
import { AuthContext } from './AuthContextDefinition';

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [user, setUser] = useState<UserResponse | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [expiresAt, setExpiresAt] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [sessionExpired, setSessionExpired] = useState(false);

  const clearAuthentication = useCallback((expired: boolean) => {
    setToken(null);
    setExpiresAt(null);
    setUser(null);
    setSessionExpired(expired);
    setAuthToken(null);
    clearAuthSession();
  }, []);

  useEffect(() => {
    let cancelled = false;
    setUnauthorizedHandler(() => clearAuthentication(true));

    const restoreSession = async () => {
      const stored = loadAuthSession();
      if (!stored.session) {
        if (!cancelled) {
          setSessionExpired(stored.expired);
          setIsLoading(false);
        }
        return;
      }

      const session = stored.session;
      setAuthToken(session.token);
      if (!cancelled) {
        setToken(session.token);
        setExpiresAt(session.expiresAt);
        setUser(session.user);
      }

      try {
        const currentUser = await authService.getMe();
        if (!cancelled) {
          setUser(currentUser);
          saveAuthSession({ ...session, user: currentUser });
        }
      } catch {
        // Un 401 se procesa en el interceptor. Ante un fallo de red se conserva
        // la sesión temporal hasta que el servidor pueda volver a validarla.
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    };

    void restoreSession();

    return () => {
      cancelled = true;
      setUnauthorizedHandler(null);
    };
  }, [clearAuthentication]);

  useEffect(() => {
    if (!expiresAt) return;

    const remainingMilliseconds = Date.parse(expiresAt) - Date.now();
    const expirationTimer = window.setTimeout(
      () => clearAuthentication(true),
      Math.max(0, remainingMilliseconds),
    );
    return () => window.clearTimeout(expirationTimer);
  }, [clearAuthentication, expiresAt]);

  const applyAuthResponse = (response: AuthResponse) => {
    setAuthToken(response.token);
    setToken(response.token);
    setExpiresAt(response.expiresAt);
    setUser(response.user);
    setSessionExpired(false);
    saveAuthSession(response);
  };

  const login = async (data: LoginRequest) => {
    setIsLoading(true);
    try {
      const res: AuthResponse = await authService.login(data);
      applyAuthResponse(res);
    } catch (error) {
      clearAuthentication(false);
      throw error;
    } finally {
      setIsLoading(false);
    }
  };

  const register = async (data: RegisterRequest) => {
    setIsLoading(true);
    try {
      const res: AuthResponse = await authService.register(data);
      applyAuthResponse(res);
    } catch (error) {
      clearAuthentication(false);
      throw error;
    } finally {
      setIsLoading(false);
    }
  };

  const logout = () => {
    clearAuthentication(false);
  };

  const loginWithGoogle = async (credential: string) => {
    setIsLoading(true);
    try {
      const response = await authService.google({ credential });
      applyAuthResponse(response);
    } catch (error) {
      clearAuthentication(false);
      throw error;
    } finally {
      setIsLoading(false);
    }
  };

  const updateUser = async (fullName: string) => {
    setIsLoading(true);
    try {
      const updatedUser = await authService.updateProfile(fullName);
      setUser(updatedUser);
      if (token && expiresAt) {
        saveAuthSession({ token, expiresAt, user: updatedUser });
      }
    } finally {
      setIsLoading(false);
    }
  };

  const refreshUser = useCallback(async () => {
    const currentUser = await authService.getMe();
    setUser(currentUser);
    if (token && expiresAt) {
      saveAuthSession({ token, expiresAt, user: currentUser });
    }
    return currentUser;
  }, [expiresAt, token]);

  return (
    <AuthContext.Provider
      value={{
        user,
        token,
        isAuthenticated: !!token,
        isLoading,
        sessionExpired,
        login,
        register,
        loginWithGoogle,
        logout,
        updateUser,
        refreshUser,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
};
