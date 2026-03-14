import { useCallback, useEffect, useState } from 'react';
import type { ChildProfile } from '@/types/api';
import { getChildren } from '../api/children-api';

export function useChildren() {
  const [children, setChildren] = useState<ChildProfile[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  const load = useCallback(async () => {
    setIsLoading(true);
    try {
      const response = await getChildren();
      if (response.success && response.data) {
        setChildren(response.data);
      }
    } catch {
      // handled by interceptor
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  return { children, isLoading, reload: load };
}
