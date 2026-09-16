import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';
import type { HomeDto } from '../types';

export async function getHome(): Promise<ApiResponse<HomeDto>> {
  const response = await apiClient.get<ApiResponse<HomeDto>>('/api/home');
  return response.data;
}
