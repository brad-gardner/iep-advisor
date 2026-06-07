import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';
import type {
  ChildLink,
  CreateSchoolStudentRequest,
  EducatorProfile,
  GrantStudentStaffAccessRequest,
  InviteParentRequest,
  SchoolStudent,
  StudentStaffAccess,
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

export async function getStudentStaffAccess(
  studentId: number
): Promise<ApiResponse<StudentStaffAccess[]>> {
  const response = await apiClient.get<ApiResponse<StudentStaffAccess[]>>(
    `/api/educator/students/${studentId}/staff-access`
  );
  return response.data;
}

export async function grantStudentStaffAccess(
  studentId: number,
  data: GrantStudentStaffAccessRequest
): Promise<ApiResponse<StudentStaffAccess>> {
  const response = await apiClient.post<ApiResponse<StudentStaffAccess>>(
    `/api/educator/students/${studentId}/staff-access`,
    data
  );
  return response.data;
}

export async function revokeStudentStaffAccess(
  studentId: number,
  accessId: number
): Promise<ApiResponse<null>> {
  const response = await apiClient.delete<ApiResponse<null>>(
    `/api/educator/students/${studentId}/staff-access/${accessId}`
  );
  return response.data;
}
