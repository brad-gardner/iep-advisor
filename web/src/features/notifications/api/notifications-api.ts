import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';
import type { MarkAllReadResult, NotificationDto, NotificationListResult } from '../types';

export async function listNotifications(
  params: { unread?: boolean; limit?: number } = {}
): Promise<ApiResponse<NotificationListResult>> {
  const query: Record<string, string> = {};
  if (params.unread) query.unread = 'true';
  if (params.limit != null) query.limit = String(params.limit);
  const response = await apiClient.get<ApiResponse<NotificationListResult>>('/api/notifications', {
    params: query,
  });
  return response.data;
}

export async function markNotificationRead(id: number): Promise<ApiResponse<null>> {
  const response = await apiClient.post<ApiResponse<null>>(`/api/notifications/${id}/read`);
  return response.data;
}

export async function markAllNotificationsRead(): Promise<ApiResponse<MarkAllReadResult>> {
  const response = await apiClient.post<ApiResponse<MarkAllReadResult>>('/api/notifications/read-all');
  return response.data;
}

// Platform admin only.
export async function listNotificationFailures(): Promise<ApiResponse<NotificationDto[]>> {
  const response = await apiClient.get<ApiResponse<NotificationDto[]>>(
    '/api/admin/notifications/failures'
  );
  return response.data;
}
