import { useEffect, useMemo, useState } from 'react';
import { apiErrorMessage } from '@/lib/api-error';
import { getAdoption, getEngagement } from '../api/district-api';
import type { AdoptionDto, EngagementDto } from '../types';

/** A server-provided message is already resolved text and is shown as-is; the
 * generic case is translated by the caller at render time (it has the
 * current `t`) — see the module doc comment below. */
export type AdoptionEngagementLoadError = { kind: 'server'; message: string } | { kind: 'generic' } | null;

interface Loaded {
  key: string;
  adoption: AdoptionDto | null;
  adoptionError: AdoptionEngagementLoadError;
  engagement: EngagementDto | null;
  engagementError: AdoptionEngagementLoadError;
}

interface UseAdoptionEngagementResult {
  adoption: AdoptionDto | null;
  engagement: EngagementDto | null;
  isLoading: boolean;
  /** Set only when BOTH pieces failed (nothing at all to render). */
  error: AdoptionEngagementLoadError;
  /** Per-piece failures, so a caller can still render whichever DTO
   * succeeded alongside a small inline notice for the one that didn't. */
  adoptionError: AdoptionEngagementLoadError;
  engagementError: AdoptionEngagementLoadError;
  retry: () => void;
}

// A server-provided message is already resolved text and is shown as-is; a
// missing one resolves to the `{ kind: 'generic' }` flag (translated at
// render time by the caller, which has the current `t`) rather than a
// pre-translated string baked in here — same reasoning as `useHome`'s
// `HomeLoadError` (see `docs/i18n/README.md`: an effect that fetches on
// mount never has `t` in its dependency array).
interface LoadResult<T> {
  data: T | null;
  error: AdoptionEngagementLoadError;
}

async function loadAdoption(schoolId: number | null): Promise<LoadResult<AdoptionDto>> {
  try {
    const res = await getAdoption({ schoolId: schoolId ?? undefined });
    if (res.success && res.data) return { data: res.data, error: null };
    return { data: null, error: res.message ? { kind: 'server', message: res.message } : { kind: 'generic' } };
  } catch (err) {
    const serverMessage = apiErrorMessage(err, '');
    return { data: null, error: serverMessage ? { kind: 'server', message: serverMessage } : { kind: 'generic' } };
  }
}

async function loadEngagement(schoolId: number | null): Promise<LoadResult<EngagementDto>> {
  try {
    const res = await getEngagement({ schoolId: schoolId ?? undefined });
    if (res.success && res.data) return { data: res.data, error: null };
    return { data: null, error: res.message ? { kind: 'server', message: res.message } : { kind: 'generic' } };
  } catch (err) {
    const serverMessage = apiErrorMessage(err, '');
    return { data: null, error: serverMessage ? { kind: 'server', message: serverMessage } : { kind: 'generic' } };
  }
}

/**
 * Combined adoption + engagement fetch for a school scope (`null` = district-
 * wide). Loading is *derived* from comparing the last-completed request's key
 * against the wanted one — same idiom as `use-student-search.ts` — so no
 * setState runs synchronously inside the effect, and a school-filter change
 * immediately shows loading rather than a stale scope's numbers. Shared by
 * the DistrictAdmin home teaser and the full compliance board.
 *
 * The two endpoints are independent, so a failure on one never discards data
 * the other already returned — each call is caught on its own (rather than
 * `Promise.all` short-circuiting the whole hook) and reported separately.
 */
export function useAdoptionEngagement(schoolId: number | null): UseAdoptionEngagementResult {
  const [retryToken, setRetryToken] = useState(0);
  const requestKey = useMemo(() => `${schoolId ?? 'all'}#${retryToken}`, [schoolId, retryToken]);
  const [loaded, setLoaded] = useState<Loaded | null>(null);

  useEffect(() => {
    let active = true;
    (async () => {
      const [adoption, engagement] = await Promise.all([loadAdoption(schoolId), loadEngagement(schoolId)]);
      if (!active) return;
      setLoaded({
        key: requestKey,
        adoption: adoption.data,
        adoptionError: adoption.error,
        engagement: engagement.data,
        engagementError: engagement.error,
      });
    })();
    return () => {
      active = false;
    };
  }, [schoolId, requestKey]);

  const isLoading = loaded?.key !== requestKey;
  const adoption = isLoading ? null : (loaded?.adoption ?? null);
  const engagement = isLoading ? null : (loaded?.engagement ?? null);
  const adoptionError = isLoading ? null : (loaded?.adoptionError ?? null);
  const engagementError = isLoading ? null : (loaded?.engagementError ?? null);

  return {
    adoption,
    engagement,
    adoptionError,
    engagementError,
    // Only a joint failure (nothing to render at all) is the hook's own
    // `error` — a single-piece failure is left to the caller to show inline
    // alongside whichever DTO did load.
    error: !isLoading && !adoption && !engagement ? (adoptionError ?? engagementError) : null,
    isLoading,
    retry: () => setRetryToken((t) => t + 1),
  };
}
