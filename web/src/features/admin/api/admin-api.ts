import { apiClient } from '@/lib/api-client';
import type { ApiResponse, AdminUser, UpdateUserRequest } from '@/types/api';

export async function getUsers(): Promise<AdminUser[]> {
  const res = await apiClient.get<ApiResponse<AdminUser[]>>('/api/users');
  return res.data.data ?? [];
}

export async function getUser(id: number): Promise<AdminUser> {
  const res = await apiClient.get<ApiResponse<AdminUser>>(`/api/users/${id}`);
  return res.data.data!;
}

export async function updateUser(id: number, data: UpdateUserRequest): Promise<AdminUser> {
  const res = await apiClient.put<ApiResponse<AdminUser>>(`/api/users/${id}`, data);
  return res.data.data!;
}

export async function deleteUser(id: number): Promise<void> {
  await apiClient.delete(`/api/users/${id}`);
}
