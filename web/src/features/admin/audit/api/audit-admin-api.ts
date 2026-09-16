import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';
import type { AuditIntegrityRunDto } from '../types';

/** The last 10 audit hash-chain integrity runs, newest first. */
export async function listAuditIntegrityRuns(): Promise<ApiResponse<AuditIntegrityRunDto[]>> {
  const res = await apiClient.get<ApiResponse<AuditIntegrityRunDto[]>>('/api/admin/audit/integrity');
  return res.data;
}

/** Runs the hash-chain integrity walk synchronously (in addition to the nightly worker run). */
export async function runAuditIntegrityCheck(): Promise<ApiResponse<AuditIntegrityRunDto>> {
  const res = await apiClient.post<ApiResponse<AuditIntegrityRunDto>>('/api/admin/audit/integrity-check');
  return res.data;
}
