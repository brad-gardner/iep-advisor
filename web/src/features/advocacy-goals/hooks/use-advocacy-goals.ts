import { useCallback, useEffect, useState } from 'react';
import type { AdvocacyGoal } from '@/types/api';
import { getAdvocacyGoals } from '../api/advocacy-goals-api';

export function useAdvocacyGoals(childId: number) {
  const [goals, setGoals] = useState<AdvocacyGoal[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  const load = useCallback(async () => {
    setIsLoading(true);
    try {
      const response = await getAdvocacyGoals(childId);
      if (response.success && response.data) {
        setGoals(response.data);
      }
    } catch {
      // handled by interceptor
    } finally {
      setIsLoading(false);
    }
  }, [childId]);

  useEffect(() => {
    load();
  }, [load]);

  return { goals, isLoading, reload: load };
}
