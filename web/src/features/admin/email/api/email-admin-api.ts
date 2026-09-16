import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';
import type { OutboundEmailDto, OutboundEmailStatusDto, OutboundEmailStatusFilter } from '../types';

/** Platform admin: outbound email rows, optionally filtered by status. */
export async function listOutboundEmails(
  status: OutboundEmailStatusFilter,
  take = 100
): Promise<ApiResponse<OutboundEmailDto[]>> {
  const res = await apiClient.get<ApiResponse<OutboundEmailDto[]>>('/api/admin/email', {
    params: { status, take },
  });
  return res.data;
}

/** Delivery-configuration + at-a-glance queue health for the admin banner. */
export async function getOutboundEmailStatus(): Promise<ApiResponse<OutboundEmailStatusDto>> {
  const res = await apiClient.get<ApiResponse<OutboundEmailStatusDto>>('/api/admin/email/status');
  return res.data;
}

/** Re-queues a Failed or Cancelled row for immediate retry. */
export async function resendOutboundEmail(id: number): Promise<ApiResponse<null>> {
  const res = await apiClient.post<ApiResponse<null>>(`/api/admin/email/${id}/resend`);
  return res.data;
}

/** Cancels a not-yet-sent (Queued) email so it will never be attempted again. */
export async function cancelOutboundEmail(id: number): Promise<ApiResponse<null>> {
  const res = await apiClient.post<ApiResponse<null>>(`/api/admin/email/${id}/cancel`);
  return res.data;
}
