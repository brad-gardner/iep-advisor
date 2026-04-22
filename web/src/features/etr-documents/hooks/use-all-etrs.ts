import { useCallback, useEffect, useState } from 'react';
import { listAllForUser } from '../api/etr-documents-api';
import type { EtrDocumentListItem } from '../types';

export function useAllEtrs() {
  const [etrs, setEtrs] = useState<EtrDocumentListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await listAllForUser();
      if (response.success && response.data) {
        setEtrs(response.data);
      } else {
        setError(response.message || 'Failed to load ETRs');
      }
    } catch {
      setError('Failed to load ETRs');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  return { etrs, loading, error, refresh: load };
}
