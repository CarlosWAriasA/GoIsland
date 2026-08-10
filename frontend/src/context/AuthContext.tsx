import React, { useCallback, useEffect, useState } from 'react';
import type {
  AuthenticationMethod,
  UserResponse,
  AuthResponse,
  LoginRequest,
  RegisterRequest,
} from '../types';
import { authService } from '../services/authService';
import { setAuthToken, setUnauthorizedHandler } from '../services/api';
import { clearAuthSession, loadAuthSession, saveAuthSession } from '../services/authSession';
import { loadLoginPage } from '../routes/loginPage';
import { AuthContext } from './AuthContextDefinition';

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [user, setUser] = useState<UserResponse | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [expiresAt, setExpiresAt] = useState<string | null>(null);
  const [authenticationMethod, setAuthenticationMethod] = useState<AuthenticationMethod | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [sessionExpired, setSessionExpired] = useState(false);
  const [signedOut, setSignedOut] = useState(false);

  const clearAuthentication = useCallback((expired: boolean) => {
    setToken(null);
    setExpiresAt(null);
    setAuthenticationMethod(null);
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
        setAuthenticationMethod(session.authenticationMethod);
        setUser(session.user);
      }

      try {
        const refreshed = await authService.refreshSession();
        if (!cancelled) {
          setAuthToken(refreshed.token);
          setToken(refreshed.token);
          setExpiresAt(refreshed.expiresAt);
          setAuthenticationMethod(refreshed.authenticationMethod);
          setUser(refreshed.user);
          saveAuthSession(refreshed);
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

  // Toda sesión abierta termina en el login, y descargar esa pantalla justo al cerrarla dejaba a
  // merced de la red el único destino que queda sin sesión. Se trae por adelantado mientras hay
  // conexión de sobra, y se reintenta al recuperarla si el primer intento no llegó a completarse.
  useEffect(() => {
    if (!token) return;

    let loaded = false;
    const preload = () => {
      if (loaded) return;
      void loadLoginPage().then(() => { loaded = true; }, () => {
        // Sin conexión no hay nada que hacer todavía: se reintenta cuando vuelva.
      });
    };

    preload();
    window.addEventListener('online', preload);
    return () => window.removeEventListener('online', preload);
  }, [token]);

  useEffect(() => {
    if (!expiresAt) return;

    // Con sesiones largas un setTimeout no sirve: desborda el máximo de 24,8 días y además no
    // avanza mientras el equipo está suspendido. Se comprueba la caducidad periódicamente.
    const expiration = Date.parse(expiresAt);
    const checkExpiration = () => {
      if (Date.now() >= expiration) clearAuthentication(true);
    };

    checkExpiration();
    const expirationTimer = window.setInterval(checkExpiration, 30_000);
    return () => window.clearInterval(expirationTimer);
  }, [clearAuthentication, expiresAt]);

  useEffect(() => {
    if (!token) return;
    let lastRefreshAt = Date.now();
    let refreshing = false;

    const refreshVisibleSession = async () => {
      if (refreshing || document.visibilityState !== 'visible'
        || Date.now() - lastRefreshAt < 60_000) return;
      refreshing = true;
      lastRefreshAt = Date.now();
      try {
        const refreshed = await authService.refreshSession();
        setAuthToken(refreshed.token);
        setToken(refreshed.token);
        setExpiresAt(refreshed.expiresAt);
        setAuthenticationMethod(refreshed.authenticationMethod);
        setUser(refreshed.user);
        saveAuthSession(refreshed);
      } catch {
        // Un 401 cierra la sesión mediante el interceptor. Los fallos temporales de red
        // conservan la sesión y se reintentan cuando la persona vuelve a la aplicación.
      } finally {
        refreshing = false;
      }
    };

    const onFocus = () => void refreshVisibleSession();
    window.addEventListener('focus', onFocus);
    document.addEventListener('visibilitychange', onFocus);
    return () => {
      window.removeEventListener('focus', onFocus);
      document.removeEventListener('visibilitychange', onFocus);
    };
  }, [token]);

  const applyAuthResponse = (response: AuthResponse) => {
    setAuthToken(response.token);
    setToken(response.token);
    setExpiresAt(response.expiresAt);
    setAuthenticationMethod(response.authenticationMethod);
    setUser(response.user);
    setSessionExpired(false);
    setSignedOut(false);
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

  // La navegación de salida se procesa como transición, así que las rutas protegidas todavía
  // pueden renderizarse con la sesión ya cerrada. La marca evita que pidan iniciar sesión a
  // quien acaba de cerrarla a propósito.
  const logout = () => {
    setSignedOut(true);
    clearAuthentication(false);
  };

  const acknowledgeSignOut = useCallback(() => setSignedOut(false), []);

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
        saveAuthSession({ token, expiresAt, authenticationMethod, user: updatedUser });
      }
    } finally {
      setIsLoading(false);
    }
  };

  const refreshUser = useCallback(async () => {
    const refreshed = await authService.refreshSession();
    setAuthToken(refreshed.token);
    setToken(refreshed.token);
    setExpiresAt(refreshed.expiresAt);
    setAuthenticationMethod(refreshed.authenticationMethod);
    setUser(refreshed.user);
    saveAuthSession(refreshed);
    return refreshed.user;
  }, []);

  return (
    <AuthContext.Provider
      value={{
        user,
        token,
        authenticationMethod,
        isAuthenticated: !!token,
        isLoading,
        sessionExpired,
        signedOut,
        login,
        register,
        loginWithGoogle,
        logout,
        acknowledgeSignOut,
        updateUser,
        refreshUser,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
};
