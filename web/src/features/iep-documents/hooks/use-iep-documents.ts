import { useCallback, useEffect, useState } from 'react';
import type { IepDocument } from '@/types/api';
import { getIepDocuments } from '../api/iep-documents-api';

export function useIepDocuments(childId: number) {
  const [documents, setDocuments] = useState<IepDocument[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  const load = useCallback(async () => {
    setIsLoading(true);
    try {
      const response = await getIepDocuments(childId);
      if (response.success && response.data) {
        setDocuments(response.data);
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

  return { documents, isLoading, reload: load };
}
