import { createContext } from 'react';
import type { LoginRequest, RegisterRequest, UserResponse } from '../types';

export interface AuthContextValue {
  user: UserResponse | null;
  token: string | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  sessionExpired: boolean;
  login: (data: LoginRequest) => Promise<void>;
  register: (data: RegisterRequest) => Promise<void>;
  logout: () => void;
  updateUser: (fullName: string) => Promise<void>;
}

export const AuthContext = createContext<AuthContextValue | undefined>(undefined);
