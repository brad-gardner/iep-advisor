import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';
import type {
  AdoptionDto,
  AuditLogFilters,
  AuditLogPage,
  ComplianceBoardDto,
  DistrictDashboard,
  DistrictOverview,
  DistrictSchool,
  EngagementDto,
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

// Compliance board (plan 5). SchoolAdmin's `schoolId` is ignored/forced server-side
// to their own school — only DistrictAdmin's picker sends one.
export async function getComplianceBoard(
  params: { schoolId?: number; from?: string; to?: string } = {}
): Promise<ApiResponse<ComplianceBoardDto>> {
  const query: Record<string, string> = {};
  if (params.schoolId != null) query.schoolId = String(params.schoolId);
  if (params.from) query.from = params.from;
  if (params.to) query.to = params.to;
  const response = await apiClient.get<ApiResponse<ComplianceBoardDto>>(
    '/api/district/compliance',
    { params: query }
  );
  return response.data;
}

export async function getAdoption(
  params: { schoolId?: number; days?: number } = {}
): Promise<ApiResponse<AdoptionDto>> {
  const query: Record<string, string> = {};
  if (params.schoolId != null) query.schoolId = String(params.schoolId);
  if (params.days != null) query.days = String(params.days);
  const response = await apiClient.get<ApiResponse<AdoptionDto>>('/api/district/adoption', {
    params: query,
  });
  return response.data;
}

export async function getEngagement(
  params: { schoolId?: number } = {}
): Promise<ApiResponse<EngagementDto>> {
  const query: Record<string, string> = {};
  if (params.schoolId != null) query.schoolId = String(params.schoolId);
  const response = await apiClient.get<ApiResponse<EngagementDto>>('/api/district/engagement', {
    params: query,
  });
  return response.data;
}
