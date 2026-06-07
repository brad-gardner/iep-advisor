import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';
import type {
  ChildLink,
  CreateSchoolStudentRequest,
  EducatorProfile,
  InviteParentRequest,
  SchoolStudent,
} from '../types';

export async function getEducatorProfile(): Promise<ApiResponse<EducatorProfile>> {
  const response = await apiClient.get<ApiResponse<EducatorProfile>>('/api/educator/me');
  return response.data;
}

export async function getStudents(): Promise<ApiResponse<SchoolStudent[]>> {
  const response = await apiClient.get<ApiResponse<SchoolStudent[]>>('/api/educator/students');
  return response.data;
}

export async function createStudent(
  data: CreateSchoolStudentRequest
): Promise<ApiResponse<SchoolStudent>> {
  const response = await apiClient.post<ApiResponse<SchoolStudent>>(
    '/api/educator/students',
    data
  );
  return response.data;
}

export async function getStudent(studentId: number): Promise<ApiResponse<SchoolStudent>> {
  const response = await apiClient.get<ApiResponse<SchoolStudent>>(
    `/api/educator/students/${studentId}`
  );
  return response.data;
}

export async function inviteParent(
  studentId: number,
  data: InviteParentRequest
): Promise<ApiResponse<ChildLink>> {
  const response = await apiClient.post<ApiResponse<ChildLink>>(
    `/api/educator/students/${studentId}/invite-parent`,
    data
  );
  return response.data;
}

export async function getStudentLinks(studentId: number): Promise<ApiResponse<ChildLink[]>> {
  const response = await apiClient.get<ApiResponse<ChildLink[]>>(
    `/api/educator/students/${studentId}/links`
  );
  return response.data;
}

export async function revokeStudentLink(
  studentId: number,
  linkId: number
): Promise<ApiResponse<null>> {
  const response = await apiClient.delete<ApiResponse<null>>(
    `/api/educator/students/${studentId}/links/${linkId}`
  );
  return response.data;
}
