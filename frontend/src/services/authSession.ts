import type { AuthenticationMethod, AuthResponse, UserResponse } from '../types';

const storageKey = 'goisland.auth-session';

export interface StoredAuthSession {
  token: string;
  expiresAt: string;
  authenticationMethod: AuthenticationMethod | null;
  user: UserResponse;
}

export interface StoredAuthSessionResult {
  session: StoredAuthSession | null;
  expired: boolean;
}

const isUserResponse = (value: unknown): value is UserResponse => {
  if (!value || typeof value !== 'object') return false;
  const user = value as Partial<UserResponse>;
  return typeof user.id === 'number'
    && typeof user.fullName === 'string'
    && typeof user.email === 'string'
    && typeof user.role === 'string'
    // isAdmin llegó después, así que una sesión guardada antes del cambio no debe expulsarse.
    && (typeof user.isAdmin === 'boolean' || user.isAdmin === undefined)
    && (typeof user.hasPassword === 'boolean' || user.hasPassword === undefined)
    && typeof user.createdAt === 'string';
};

// La sesión vivía en sessionStorage, así que cerrar la pestaña obligaba a iniciar sesión de
// nuevo aunque el token siguiera vigente. Se lee localStorage y se migra lo que quedó en
// sessionStorage para no expulsar a quien tenga una sesión abierta al desplegar.
const readRawSession = (): string | null => {
  const persisted = window.localStorage.getItem(storageKey);
  if (persisted) return persisted;

  const legacy = window.sessionStorage.getItem(storageKey);
  if (legacy) {
    window.localStorage.setItem(storageKey, legacy);
    window.sessionStorage.removeItem(storageKey);
  }
  return legacy;
};

export const loadAuthSession = (): StoredAuthSessionResult => {
  const rawSession = readRawSession();
  if (!rawSession) return { session: null, expired: false };

  try {
    const session = JSON.parse(rawSession) as Partial<StoredAuthSession>;
    const expiresAt = typeof session.expiresAt === 'string' ? Date.parse(session.expiresAt) : Number.NaN;
    const isValid = typeof session.token === 'string'
      && session.token.length > 0
      && Number.isFinite(expiresAt)
      && isUserResponse(session.user);

    if (!isValid || expiresAt <= Date.now()) {
      clearAuthSession();
      return { session: null, expired: true };
    }

    const authenticationMethod = session.authenticationMethod === 'Google'
      || session.authenticationMethod === 'Password'
      ? session.authenticationMethod
      : null;
    return {
      session: {
        token: session.token!,
        expiresAt: session.expiresAt!,
        authenticationMethod,
        user: session.user!,
      },
      expired: false,
    };
  } catch {
    clearAuthSession();
    return { session: null, expired: true };
  }
};

export const saveAuthSession = (session: StoredAuthSession | AuthResponse) => {
  window.localStorage.setItem(storageKey, JSON.stringify(session));
};

export const clearAuthSession = () => {
  window.localStorage.removeItem(storageKey);
  window.sessionStorage.removeItem(storageKey);
};
