import { useCallback, useEffect, useState } from 'react';
import { type LoadError, toLoadError } from '@/lib/api-error';
import { listAllForUser } from '../api/etr-documents-api';
import type { EtrDocumentListItem } from '../types';

export function useAllEtrs() {
  const [etrs, setEtrs] = useState<EtrDocumentListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<LoadError | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await listAllForUser();
      if (response.success && response.data) {
        setEtrs(response.data);
      } else {
        setError(toLoadError(response));
      }
    } catch (err) {
      setError(toLoadError(err));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  return { etrs, loading, error, refresh: load };
}
