import { useEffect, useState } from 'react';
import { apiErrorMessage } from '@/lib/api-error';
import { getHome } from '../api/home-api';
import type { HomeDto } from '../types';

interface UseHomeResult {
  home: HomeDto | null;
  isLoading: boolean;
  error: string | null;
  retry: () => void;
}

/**
 * Loads the single role-scoped `GET /api/home` aggregate. A failure never
 * degrades to an empty state — callers render the error with a retry action.
 * Loading is *derived* (`home === null && error === null`), so no setState
 * runs synchronously inside the effect — same idiom as
 * `student-timeline-card.tsx`/`notifications-page.tsx`: a retry re-runs the
 * fetch behind the existing error until it resolves.
 */
export function useHome(): UseHomeResult {
  const [home, setHome] = useState<HomeDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  // Bumped by the "Try again" button to re-run the load effect below.
  const [retryToken, setRetryToken] = useState(0);

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const response = await getHome();
        if (!active) return;
        if (response.success && response.data) {
          setHome(response.data);
          setError(null);
        } else {
          setError(response.message ?? 'Could not load your home');
        }
      } catch (err) {
        if (active) setError(apiErrorMessage(err, 'Could not load your home'));
      }
    })();
    return () => {
      active = false;
    };
  }, [retryToken]);

  return {
    home,
    isLoading: home === null && error === null,
    error,
    retry: () => setRetryToken((t) => t + 1),
  };
}
