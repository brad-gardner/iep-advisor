import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';
import type {
  AuditLogFilters,
  AuditLogPage,
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

// Fetches a keyset page of the district audit log. Only defined filter fields
// are serialized into the query string so unset filters never send empty params
// (which the backend would treat as present).
export async function getAuditLog(
  params: AuditLogFilters
): Promise<ApiResponse<AuditLogPage>> {
  const query: Record<string, string> = {};
  if (params.staffUserId != null) query.staffUserId = String(params.staffUserId);
  if (params.studentId != null) query.studentId = String(params.studentId);
  if (params.action) query.action = params.action;
  if (params.fromUtc) query.fromUtc = params.fromUtc;
  if (params.toUtc) query.toUtc = params.toUtc;
  if (params.cursor != null) query.cursor = String(params.cursor);
  if (params.pageSize != null) query.pageSize = String(params.pageSize);

  const response = await apiClient.get<ApiResponse<AuditLogPage>>(
    '/api/district/audit-log',
    { params: query }
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
