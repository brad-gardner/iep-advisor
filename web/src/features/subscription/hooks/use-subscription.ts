import { useState, useEffect, useCallback } from 'react';
import { getSubscriptionStatus } from '../api/subscription-api';
import type { SubscriptionStatus } from '@/types/api';

export function useSubscription() {
  const [status, setStatus] = useState<SubscriptionStatus | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const load = useCallback(async () => {
    setIsLoading(true);
    try {
      const data = await getSubscriptionStatus();
      setStatus(data);
    } catch {
      setStatus(null);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const reload = useCallback(() => {
    load();
  }, [load]);

  return { status, isLoading, reload };
}
