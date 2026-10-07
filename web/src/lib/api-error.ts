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

/**
 * A failed load's result: either a server-provided message (already resolved
 * text, shown as-is) or the generic flag, translated by the CALLER at render
 * time — never baked in here. This is the one shape every mount-effect load
 * across the app should use for its error state (see `docs/i18n/README.md`'s
 * "Load errors: the shared `LoadError` pattern") instead of a hand-rolled
 * `{ kind: 'server' } | { kind: 'generic' }` union or an untranslated-string-
 * plus-empty-sentinel flag — both of which this type/its helpers replace.
 */
export type LoadError = { kind: 'server'; message: string } | { kind: 'generic' };

/**
 * Builds a `LoadError` from either side of a load: a resolved `ApiResponse`
 * that failed (`res.success === false`, read its optional `message`) or a
 * caught error from a rejected request (resolved via `apiErrorMessage`).
 * Pass whichever one you have — the two are told apart by `instanceof Error`
 * (a caught rejection is always an `Error`/`AxiosError`; a decoded response
 * body never is).
 */
export function toLoadError(resOrErr: { message?: string | null } | unknown): LoadError {
  const message = resOrErr instanceof Error ? apiErrorMessage(resOrErr, '') : (resOrErr as { message?: string | null } | null | undefined)?.message;
  return message ? { kind: 'server', message } : { kind: 'generic' };
}

/** Renders a `LoadError` (or `null`, meaning no error) to display text — a
 *  server message as-is, the generic case as `fallback` (the caller's own,
 *  current-language translation). */
export function loadErrorText(e: LoadError | null, fallback: string): string | null {
  if (!e) return null;
  return e.kind === 'server' ? e.message : fallback;
}
