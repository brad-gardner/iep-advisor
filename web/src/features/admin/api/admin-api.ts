import { apiClient } from '@/lib/api-client';
import type { ApiResponse, AdminUser, UpdateUserRequest, AdminDashboardStats } from '@/types/api';

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

export async function getDashboardStats(): Promise<AdminDashboardStats> {
  const res = await apiClient.get<ApiResponse<AdminDashboardStats>>('/api/admin/dashboard');
  return res.data.data!;
}

export async function getRecentUsers(count: number = 10): Promise<AdminUser[]> {
  const res = await apiClient.get<ApiResponse<AdminUser[]>>(`/api/admin/recent-users?count=${count}`);
  return res.data.data ?? [];
}
