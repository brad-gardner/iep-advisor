import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';
import type { CalendarFeedDto, CalendarItemDto } from '../types';

export async function listCalendarItems(params: {
  from: string;
  to: string;
}): Promise<ApiResponse<CalendarItemDto[]>> {
  const response = await apiClient.get<ApiResponse<CalendarItemDto[]>>('/api/calendar/mine', {
    params,
  });
  return response.data;
}

export async function getCalendarFeed(): Promise<ApiResponse<CalendarFeedDto>> {
  const response = await apiClient.get<ApiResponse<CalendarFeedDto>>('/api/calendar/feed');
  return response.data;
}

export async function regenerateCalendarFeed(): Promise<ApiResponse<CalendarFeedDto>> {
  const response = await apiClient.post<ApiResponse<CalendarFeedDto>>('/api/calendar/feed/regenerate');
  return response.data;
}
