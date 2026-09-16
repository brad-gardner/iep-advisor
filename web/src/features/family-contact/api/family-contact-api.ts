import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';
import type {
  CreateFamilyContactAttemptRequest,
  CreateOfflineFamilyInputRequest,
  FamilyContactAttemptDto,
  OfflineFamilyInputDto,
} from '../types';

function base(studentId: number): string {
  return `/api/educator/students/${studentId}`;
}

export async function getContactAttempts(studentId: number): Promise<ApiResponse<FamilyContactAttemptDto[]>> {
  const res = await apiClient.get<ApiResponse<FamilyContactAttemptDto[]>>(`${base(studentId)}/contact-attempts`);
  return res.data;
}

export async function recordContactAttempt(
  studentId: number,
  request: CreateFamilyContactAttemptRequest
): Promise<ApiResponse<FamilyContactAttemptDto>> {
  const res = await apiClient.post<ApiResponse<FamilyContactAttemptDto>>(
    `${base(studentId)}/contact-attempts`,
    request
  );
  return res.data;
}

export async function getOfflineInput(studentId: number): Promise<ApiResponse<OfflineFamilyInputDto[]>> {
  const res = await apiClient.get<ApiResponse<OfflineFamilyInputDto[]>>(`${base(studentId)}/offline-input`);
  return res.data;
}

export async function recordOfflineInput(
  studentId: number,
  request: CreateOfflineFamilyInputRequest
): Promise<ApiResponse<OfflineFamilyInputDto>> {
  const res = await apiClient.post<ApiResponse<OfflineFamilyInputDto>>(`${base(studentId)}/offline-input`, request);
  return res.data;
}
