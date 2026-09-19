import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';
import type { JournalEntryDto, JournalTag, SaveJournalEntryRequest } from '../types/journal';

export interface ListJournalOptions {
  tag?: JournalTag;
  /** Default 50 server-side, max 200. */
  take?: number;
}

/** Newest `occurredOn` first. Viewer+ on the child; there is no educator endpoint. */
export async function listJournalEntries(
  childId: number,
  options: ListJournalOptions = {},
): Promise<ApiResponse<JournalEntryDto[]>> {
  const params: Record<string, string | number> = {};
  if (options.tag) params.tag = options.tag;
  if (options.take) params.take = options.take;
  const res = await apiClient.get<ApiResponse<JournalEntryDto[]>>(`/api/children/${childId}/journal`, { params });
  return res.data;
}

export async function createJournalEntry(childId: number, body: SaveJournalEntryRequest): Promise<ApiResponse<JournalEntryDto>> {
  const res = await apiClient.post<ApiResponse<JournalEntryDto>>(`/api/children/${childId}/journal`, body);
  return res.data;
}

export async function updateJournalEntry(id: number, body: SaveJournalEntryRequest): Promise<ApiResponse<JournalEntryDto>> {
  const res = await apiClient.put<ApiResponse<JournalEntryDto>>(`/api/journal/${id}`, body);
  return res.data;
}

export async function deleteJournalEntry(id: number): Promise<ApiResponse<unknown>> {
  const res = await apiClient.delete<ApiResponse<unknown>>(`/api/journal/${id}`);
  return res.data;
}
