import { useEffect, useMemo, useState } from 'react';
import { apiErrorMessage } from '@/lib/api-error';
import { getAdoption, getEngagement } from '../api/district-api';
import type { AdoptionDto, EngagementDto } from '../types';

interface Loaded {
  key: string;
  adoption: AdoptionDto | null;
  engagement: EngagementDto | null;
  error: string | null;
}

interface UseAdoptionEngagementResult {
  adoption: AdoptionDto | null;
  engagement: EngagementDto | null;
  isLoading: boolean;
  error: string | null;
  retry: () => void;
}

/**
 * Combined adoption + engagement fetch for a school scope (`null` = district-
 * wide). Loading is *derived* from comparing the last-completed request's key
 * against the wanted one — same idiom as `use-student-search.ts` — so no
 * setState runs synchronously inside the effect, and a school-filter change
 * immediately shows loading rather than a stale scope's numbers. Shared by
 * the DistrictAdmin home teaser and the full compliance board.
 */
export function useAdoptionEngagement(schoolId: number | null): UseAdoptionEngagementResult {
  const [retryToken, setRetryToken] = useState(0);
  const requestKey = useMemo(() => `${schoolId ?? 'all'}#${retryToken}`, [schoolId, retryToken]);
  const [loaded, setLoaded] = useState<Loaded | null>(null);

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const [adoptionRes, engagementRes] = await Promise.all([
          getAdoption({ schoolId: schoolId ?? undefined }),
          getEngagement({ schoolId: schoolId ?? undefined }),
        ]);
        if (!active) return;
        if (adoptionRes.success && adoptionRes.data && engagementRes.success && engagementRes.data) {
          setLoaded({ key: requestKey, adoption: adoptionRes.data, engagement: engagementRes.data, error: null });
        } else {
          setLoaded({
            key: requestKey,
            adoption: null,
            engagement: null,
            error: adoptionRes.message || engagementRes.message || 'Could not load adoption data',
          });
        }
      } catch (err) {
        if (active) {
          setLoaded({
            key: requestKey,
            adoption: null,
            engagement: null,
            error: apiErrorMessage(err, 'Could not load adoption data'),
          });
        }
      }
    })();
    return () => {
      active = false;
    };
  }, [schoolId, requestKey]);

  const isLoading = loaded?.key !== requestKey;
  return {
    adoption: isLoading ? null : (loaded?.adoption ?? null),
    engagement: isLoading ? null : (loaded?.engagement ?? null),
    error: isLoading ? null : (loaded?.error ?? null),
    isLoading,
    retry: () => setRetryToken((t) => t + 1),
  };
}
