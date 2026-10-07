import { useCallback, useEffect, useState } from 'react';
import i18n from '@/lib/i18n';
import type { ComparisonResult } from '@/types/api';
import { compareIeps } from '../api/iep-comparison-api';

export function useIepComparison(iepId: number, otherId: number) {
  const [comparison, setComparison] = useState<ComparisonResult | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const response = await compareIeps(iepId, otherId);
      if (response.success && response.data) {
        setComparison(response.data);
      } else {
        setError(response.message || i18n.t('iep-comparison:loadErrors.loadFailed'));
      }
    } catch {
      setError(i18n.t('iep-comparison:loadErrors.loadError'));
    } finally {
      setIsLoading(false);
    }
  }, [iepId, otherId]);

  useEffect(() => {
    load();
  }, [load]);

  return { comparison, isLoading, error, reload: load };
}
