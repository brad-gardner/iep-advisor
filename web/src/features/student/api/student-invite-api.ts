import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';
import type {
  AcceptedStudentInviteDto,
  StudentInviteDto,
  StudentInvitePreviewDto,
} from '../types';

// The invited student previews who/what they're linking to. The backend checks
// the authenticated user's email matches the invited email.
export async function previewInvite(
  token: string
): Promise<ApiResponse<StudentInvitePreviewDto>> {
  const response = await apiClient.get<ApiResponse<StudentInvitePreviewDto>>(
    '/api/student-invites/preview',
    { params: { token } }
  );
  return response.data;
}

// Accepts the invite. consentAccepted must be true or the backend returns 400.
export async function acceptInvite(
  token: string,
  consentAccepted: boolean
): Promise<ApiResponse<AcceptedStudentInviteDto>> {
  const response = await apiClient.post<ApiResponse<AcceptedStudentInviteDto>>(
    '/api/student-invites/accept',
    { token, consentAccepted }
  );
  return response.data;
}

// Parent invites their child (must own the child profile).
export async function inviteStudentFromParent(
  childId: number,
  studentEmail: string
): Promise<ApiResponse<StudentInviteDto>> {
  const response = await apiClient.post<ApiResponse<StudentInviteDto>>(
    `/api/children/${childId}/invite-student`,
    { studentEmail }
  );
  return response.data;
}

// Educator invites a school student.
export async function inviteStudentFromEducator(
  studentId: number,
  studentEmail: string
): Promise<ApiResponse<StudentInviteDto>> {
  const response = await apiClient.post<ApiResponse<StudentInviteDto>>(
    `/api/educator/students/${studentId}/invite-student`,
    { studentEmail }
  );
  return response.data;
}
