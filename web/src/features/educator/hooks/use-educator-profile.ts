import { useCallback, useEffect, useState } from 'react';
import { getEducatorProfile } from '../api/educator-api';
import type { EducatorProfile } from '../types';

interface UseEducatorProfileResult {
  profile: EducatorProfile | null;
  isOnboarded: boolean;
  isLoading: boolean;
  reload: () => Promise<void>;
}

// Loads GET /api/educator/me. A 404/failure means the user has not onboarded
// as an educator yet, so isOnboarded is false (not an error condition).
export function useEducatorProfile(): UseEducatorProfileResult {
  const [profile, setProfile] = useState<EducatorProfile | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const reload = useCallback(async () => {
    setIsLoading(true);
    try {
      const response = await getEducatorProfile();
      setProfile(response.success && response.data ? response.data : null);
    } catch {
      setProfile(null);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    reload();
  }, [reload]);

  return { profile, isOnboarded: profile !== null, isLoading, reload };
}
