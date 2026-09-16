import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';
import type {
  AddTeamMemberRequest,
  AssignCaseManagerBulkRequest,
  AssignCaseManagerBulkResult,
  ChildLink,
  CreateSchoolStudentRequest,
  EducatorProfile,
  EligibleStaff,
  ExitStudentRequest,
  InviteParentRequest,
  PagedResult,
  SchoolStudent,
  StudentSearchParams,
  StudentTeamMember,
  TransferStudentRequest,
  UpdateSchoolStudentRequest,
  UpdateTeamMemberRequest,
} from '../types';

const STUDENTS = '/api/educator/students';

export async function getEducatorProfile(): Promise<ApiResponse<EducatorProfile>> {
  const response = await apiClient.get<ApiResponse<EducatorProfile>>('/api/educator/me');
  return response.data;
}

// ---------------------------------------------------------------- Students

// Server-driven roster search. Only defined params are serialized so unset
// filters never send empty strings (the backend defaults status to Active).
export async function searchStudents(
  params: StudentSearchParams = {}
): Promise<ApiResponse<PagedResult<SchoolStudent>>> {
  const query: Record<string, string> = {};
  if (params.query) query.query = params.query;
  if (params.schoolId != null) query.schoolId = String(params.schoolId);
  if (params.status) query.status = params.status;
  if (params.grade) query.grade = params.grade;
  if (params.attention) query.attention = params.attention;
  if (params.from) query.from = params.from;
  if (params.to) query.to = params.to;
  if (params.page != null) query.page = String(params.page);
  if (params.pageSize != null) query.pageSize = String(params.pageSize);

  const response = await apiClient.get<ApiResponse<PagedResult<SchoolStudent>>>(STUDENTS, {
    params: query,
  });
  return response.data;
}

export async function createStudent(
  data: CreateSchoolStudentRequest
): Promise<ApiResponse<SchoolStudent>> {
  const response = await apiClient.post<ApiResponse<SchoolStudent>>(STUDENTS, data);
  return response.data;
}

export async function getStudent(studentId: number): Promise<ApiResponse<SchoolStudent>> {
  const response = await apiClient.get<ApiResponse<SchoolStudent>>(`${STUDENTS}/${studentId}`);
  return response.data;
}

export async function updateStudent(
  studentId: number,
  data: UpdateSchoolStudentRequest
): Promise<ApiResponse<SchoolStudent>> {
  const response = await apiClient.put<ApiResponse<SchoolStudent>>(
    `${STUDENTS}/${studentId}`,
    data
  );
  return response.data;
}

export async function exitStudent(
  studentId: number,
  data: ExitStudentRequest
): Promise<ApiResponse<SchoolStudent>> {
  const response = await apiClient.post<ApiResponse<SchoolStudent>>(
    `${STUDENTS}/${studentId}/exit`,
    data
  );
  return response.data;
}

export async function reactivateStudent(studentId: number): Promise<ApiResponse<SchoolStudent>> {
  const response = await apiClient.post<ApiResponse<SchoolStudent>>(
    `${STUDENTS}/${studentId}/reactivate`
  );
  return response.data;
}

export async function archiveStudent(studentId: number): Promise<ApiResponse<SchoolStudent>> {
  const response = await apiClient.post<ApiResponse<SchoolStudent>>(
    `${STUDENTS}/${studentId}/archive`
  );
  return response.data;
}

export async function transferStudent(
  studentId: number,
  data: TransferStudentRequest
): Promise<ApiResponse<SchoolStudent>> {
  const response = await apiClient.post<ApiResponse<SchoolStudent>>(
    `${STUDENTS}/${studentId}/transfer`,
    data
  );
  return response.data;
}

export async function assignCaseManagerBulk(
  data: AssignCaseManagerBulkRequest
): Promise<ApiResponse<AssignCaseManagerBulkResult>> {
  const response = await apiClient.post<ApiResponse<AssignCaseManagerBulkResult>>(
    `${STUDENTS}/bulk/case-manager`,
    data
  );
  return response.data;
}

// ---------------------------------------------------------------- IEP team

export async function getTeam(studentId: number): Promise<ApiResponse<StudentTeamMember[]>> {
  const response = await apiClient.get<ApiResponse<StudentTeamMember[]>>(
    `${STUDENTS}/${studentId}/team`
  );
  return response.data;
}

// Staff the caller may add to this student's team (admin in scope or the
// current lead case manager).
export async function getEligibleTeamStaff(
  studentId: number
): Promise<ApiResponse<EligibleStaff[]>> {
  const response = await apiClient.get<ApiResponse<EligibleStaff[]>>(
    `${STUDENTS}/${studentId}/team/eligible`
  );
  return response.data;
}

export async function addTeamMember(
  studentId: number,
  data: AddTeamMemberRequest
): Promise<ApiResponse<StudentTeamMember>> {
  const response = await apiClient.post<ApiResponse<StudentTeamMember>>(
    `${STUDENTS}/${studentId}/team`,
    data
  );
  return response.data;
}

export async function updateTeamMember(
  studentId: number,
  memberId: number,
  data: UpdateTeamMemberRequest
): Promise<ApiResponse<StudentTeamMember>> {
  const response = await apiClient.put<ApiResponse<StudentTeamMember>>(
    `${STUDENTS}/${studentId}/team/${memberId}`,
    data
  );
  return response.data;
}

export async function setTeamLead(
  studentId: number,
  memberId: number
): Promise<ApiResponse<StudentTeamMember>> {
  const response = await apiClient.post<ApiResponse<StudentTeamMember>>(
    `${STUDENTS}/${studentId}/team/${memberId}/lead`
  );
  return response.data;
}

export async function removeTeamMember(
  studentId: number,
  memberId: number
): Promise<ApiResponse<null>> {
  const response = await apiClient.delete<ApiResponse<null> | ''>(
    `${STUDENTS}/${studentId}/team/${memberId}`
  );
  // The route answers with the envelope; an empty 204 body (older servers)
  // means the same thing, so both count as success.
  return response.status === 204 || response.data === ''
    ? { success: true, data: null }
    : response.data;
}

// ---------------------------------------------------------------- Family links

export async function inviteParent(
  studentId: number,
  data: InviteParentRequest
): Promise<ApiResponse<ChildLink>> {
  const response = await apiClient.post<ApiResponse<ChildLink>>(
    `${STUDENTS}/${studentId}/invite-parent`,
    data
  );
  return response.data;
}

export async function getStudentLinks(studentId: number): Promise<ApiResponse<ChildLink[]>> {
  const response = await apiClient.get<ApiResponse<ChildLink[]>>(
    `${STUDENTS}/${studentId}/links`
  );
  return response.data;
}

export async function revokeStudentLink(
  studentId: number,
  linkId: number
): Promise<ApiResponse<null>> {
  const response = await apiClient.delete<ApiResponse<null>>(
    `${STUDENTS}/${studentId}/links/${linkId}`
  );
  return response.data;
}
