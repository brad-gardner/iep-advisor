import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';
import type {
  DistrictDashboard,
  DistrictOverview,
  DistrictSchool,
  SaveSchoolRequest,
} from '../types';

export async function getDistrict(): Promise<ApiResponse<DistrictOverview>> {
  const response = await apiClient.get<ApiResponse<DistrictOverview>>('/api/district');
  return response.data;
}

export async function getDistrictDashboard(): Promise<ApiResponse<DistrictDashboard>> {
  const response = await apiClient.get<ApiResponse<DistrictDashboard>>(
    '/api/district/dashboard'
  );
  return response.data;
}

export async function getDistrictSchools(): Promise<ApiResponse<DistrictSchool[]>> {
  const response = await apiClient.get<ApiResponse<DistrictSchool[]>>('/api/district/schools');
  return response.data;
}

export async function createSchool(
  data: SaveSchoolRequest
): Promise<ApiResponse<DistrictSchool>> {
  const response = await apiClient.post<ApiResponse<DistrictSchool>>(
    '/api/district/schools',
    data
  );
  return response.data;
}

export async function updateSchool(
  schoolId: number,
  data: SaveSchoolRequest
): Promise<ApiResponse<DistrictSchool>> {
  const response = await apiClient.put<ApiResponse<DistrictSchool>>(
    `/api/district/schools/${schoolId}`,
    data
  );
  return response.data;
}

export async function deactivateSchool(schoolId: number): Promise<ApiResponse<null>> {
  const response = await apiClient.delete<ApiResponse<null>>(
    `/api/district/schools/${schoolId}`
  );
  return response.data;
}
