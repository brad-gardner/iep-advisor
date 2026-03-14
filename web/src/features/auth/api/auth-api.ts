import { apiClient } from '@/lib/api-client';
import type {
  ApiResponse,
  LoginRequest,
  LoginResponse,
  RegisterRequest,
  UpdateProfileRequest,
  User,
} from '@/types/api';

export async function login(data: LoginRequest): Promise<ApiResponse<LoginResponse>> {
  const response = await apiClient.post<ApiResponse<LoginResponse>>('/api/auth/login', data);
  return response.data;
}

export async function register(data: RegisterRequest): Promise<ApiResponse<null>> {
  const response = await apiClient.post<ApiResponse<null>>('/api/auth/register', data);
  return response.data;
}

export async function getCurrentUser(): Promise<ApiResponse<User>> {
  const response = await apiClient.get<ApiResponse<User>>('/api/auth/me');
  return response.data;
}

export async function updateProfile(data: UpdateProfileRequest): Promise<ApiResponse<User>> {
  const response = await apiClient.put<ApiResponse<User>>('/api/auth/me', data);
  return response.data;
}
