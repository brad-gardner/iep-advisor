import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';
import type { MeetingBriefDto } from '../types';

/** 404 when no brief has been generated yet — callers distinguish that from
 *  a genuine load failure (see `useMeetingBrief`). */
export async function getBrief(meetingId: number): Promise<ApiResponse<MeetingBriefDto>> {
  const res = await apiClient.get<ApiResponse<MeetingBriefDto>>(`/api/meetings/${meetingId}/brief`);
  return res.data;
}

/** Generate (first time) or regenerate (replace) the brief. */
export async function generateBrief(meetingId: number): Promise<ApiResponse<MeetingBriefDto>> {
  const res = await apiClient.post<ApiResponse<MeetingBriefDto>>(`/api/meetings/${meetingId}/brief`, {});
  return res.data;
}
