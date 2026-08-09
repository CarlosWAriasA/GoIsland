import { api } from './api';
import type {
  AuthResponse,
  ChangePasswordRequest,
  ForgotPasswordRequest,
  GoogleAuthRequest,
  LoginRequest,
  MessageResponse,
  RegisterRequest,
  ResetPasswordRequest,
  UserResponse,
} from '../types';

export const authService = {
  login: async (data: LoginRequest): Promise<AuthResponse> => {
    const response = await api.post<AuthResponse>('/auth/login', data);
    return response.data;
  },

  register: async (data: RegisterRequest): Promise<AuthResponse> => {
    const response = await api.post<AuthResponse>('/auth/register', data);
    return response.data;
  },

  getMe: async (): Promise<UserResponse> => {
    const response = await api.get<UserResponse>('/auth/me');
    return response.data;
  },

  updateProfile: async (fullName: string): Promise<UserResponse> => {
    const response = await api.put<UserResponse>('/users/profile', { fullName });
    return response.data;
  },

  refreshSession: async (): Promise<AuthResponse> => {
    const response = await api.post<AuthResponse>('/auth/refresh-session');
    return response.data;
  },

  google: async (data: GoogleAuthRequest): Promise<AuthResponse> => {
    const response = await api.post<AuthResponse>('/auth/google', data);
    return response.data;
  },

  changePassword: async (data: ChangePasswordRequest): Promise<void> => {
    await api.put('/auth/change-password', data);
  },

  forgotPassword: async (data: ForgotPasswordRequest): Promise<MessageResponse> => {
    const response = await api.post<MessageResponse>('/auth/forgot-password', data);
    return response.data;
  },

  resetPassword: async (data: ResetPasswordRequest): Promise<void> => {
    await api.post('/auth/reset-password', data);
  },
};
export type { LoginRequest, RegisterRequest };
