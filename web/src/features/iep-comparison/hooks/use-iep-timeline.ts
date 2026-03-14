import { useCallback, useEffect, useState } from 'react';
import type { TimelineResult } from '@/types/api';
import { getTimeline } from '../api/iep-comparison-api';

export function useIepTimeline(childId: number) {
  const [timeline, setTimeline] = useState<TimelineResult | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const load = useCallback(async () => {
    setIsLoading(true);
    try {
      const response = await getTimeline(childId);
      if (response.success && response.data) {
        setTimeline(response.data);
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

  return { timeline, isLoading, reload: load };
}
