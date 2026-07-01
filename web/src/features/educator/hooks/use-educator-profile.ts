import { useCallback, useEffect, useState } from 'react';
import { getEducatorProfile } from '../api/educator-api';
import type { EducatorProfile } from '../types';

interface UseEducatorProfileOptions {
  // When false the hook skips fetching entirely and reports not-loading. Lets
  // the sidebar mount the hook for every user but only hit the API for
  // Educators.
  enabled?: boolean;
}

interface UseEducatorProfileResult {
  profile: EducatorProfile | null;
  isLoading: boolean;
  reload: () => Promise<void>;
}

// Module-level cache so the sidebar and any page that reads the educator
// profile share a single in-flight request and result rather than each firing
// their own GET /api/educator/me. Deliberately tiny — no data library.
let cachedProfile: EducatorProfile | null = null;
let inFlight: Promise<EducatorProfile | null> | null = null;
const subscribers = new Set<(profile: EducatorProfile | null) => void>();

async function fetchProfile(): Promise<EducatorProfile | null> {
  if (inFlight) return inFlight;
  inFlight = (async () => {
    try {
      const response = await getEducatorProfile();
      cachedProfile = response.success && response.data ? response.data : null;
    } catch {
      // A 404/failure means no educator profile (e.g. a non-staff user); that
      // is a normal "not onboarded" state, not an error to surface.
      cachedProfile = null;
    } finally {
      inFlight = null;
    }
    subscribers.forEach((notify) => notify(cachedProfile));
    return cachedProfile;
  })();
  return inFlight;
}

// Clears the shared cache and refetches; callers that mutate org state (e.g.
// schools) can invalidate so every consumer re-reads.
export async function reloadEducatorProfile(): Promise<EducatorProfile | null> {
  cachedProfile = null;
  return fetchProfile();
}

// Loads GET /api/educator/me through a shared module cache.
export function useEducatorProfile(
  options: UseEducatorProfileOptions = {}
): UseEducatorProfileResult {
  const { enabled = true } = options;
  const [profile, setProfile] = useState<EducatorProfile | null>(cachedProfile);
  const [isLoading, setIsLoading] = useState(enabled && cachedProfile === null);

  const reload = useCallback(async () => {
    if (!enabled) return;
    setIsLoading(true);
    await reloadEducatorProfile();
    setIsLoading(false);
  }, [enabled]);

  useEffect(() => {
    // The lazy initializers above already set the correct initial profile and
    // loading flag, so the effect only subscribes and (when needed) kicks off a
    // shared fetch — no synchronous setState in the effect body.
    if (!enabled) return;

    const notify = (next: EducatorProfile | null) => {
      setProfile(next);
      setIsLoading(false);
    };
    subscribers.add(notify);

    if (cachedProfile === null) {
      void fetchProfile();
    }

    return () => {
      subscribers.delete(notify);
    };
  }, [enabled]);

  return { profile, isLoading, reload };
}
