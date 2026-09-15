import axios from 'axios';
import type { ApiResponse } from '@/types/api';

/**
 * The user-facing message for a failed API call. Controllers return business
 * refusals as 4xx with an `ApiResponse` envelope (`{ success: false, message }`),
 * which axios surfaces as a *rejection*, so a bare `catch` would discard the
 * server's wording. Non-axios errors and envelopes without a message fall
 * back to `fallback`.
 */
export function apiErrorMessage(err: unknown, fallback: string): string {
  if (!axios.isAxiosError(err)) return fallback;
  const body = err.response?.data as ApiResponse<unknown> | undefined;
  return body?.message || fallback;
}
