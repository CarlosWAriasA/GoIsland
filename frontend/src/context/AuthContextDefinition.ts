import { createContext } from 'react';
import type { AuthenticationMethod, LoginRequest, RegisterRequest, UserResponse } from '../types';

export interface AuthContextValue {
  user: UserResponse | null;
  token: string | null;
  authenticationMethod: AuthenticationMethod | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  sessionExpired: boolean;
  login: (data: LoginRequest) => Promise<void>;
  register: (data: RegisterRequest) => Promise<void>;
  loginWithGoogle: (credential: string) => Promise<void>;
  logout: () => void;
  updateUser: (fullName: string) => Promise<void>;
  refreshUser: () => Promise<UserResponse>;
}

export const AuthContext = createContext<AuthContextValue | undefined>(undefined);
