import { useEffect, useMemo, useState } from 'react';
import { apiErrorMessage } from '@/lib/api-error';
import { getAdoption, getEngagement } from '../api/district-api';
import type { AdoptionDto, EngagementDto } from '../types';

interface Loaded {
  key: string;
  adoption: AdoptionDto | null;
  adoptionError: string | null;
  engagement: EngagementDto | null;
  engagementError: string | null;
}

interface UseAdoptionEngagementResult {
  adoption: AdoptionDto | null;
  engagement: EngagementDto | null;
  isLoading: boolean;
  /** Set only when BOTH pieces failed (nothing at all to render). */
  error: string | null;
  /** Per-piece failures, so a caller can still render whichever DTO
   * succeeded alongside a small inline notice for the one that didn't. */
  adoptionError: string | null;
  engagementError: string | null;
  retry: () => void;
}

async function loadAdoption(schoolId: number | null): Promise<{ data: AdoptionDto | null; error: string | null }> {
  try {
    const res = await getAdoption({ schoolId: schoolId ?? undefined });
    return res.success && res.data
      ? { data: res.data, error: null }
      : { data: null, error: res.message || 'Could not load adoption data' };
  } catch (err) {
    return { data: null, error: apiErrorMessage(err, 'Could not load adoption data') };
  }
}

async function loadEngagement(
  schoolId: number | null
): Promise<{ data: EngagementDto | null; error: string | null }> {
  try {
    const res = await getEngagement({ schoolId: schoolId ?? undefined });
    return res.success && res.data
      ? { data: res.data, error: null }
      : { data: null, error: res.message || 'Could not load engagement data' };
  } catch (err) {
    return { data: null, error: apiErrorMessage(err, 'Could not load engagement data') };
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
