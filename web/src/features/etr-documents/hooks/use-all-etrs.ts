import { useCallback, useEffect, useState } from 'react';
import { apiErrorMessage } from '@/lib/api-error';
import { listAllForUser } from '../api/etr-documents-api';
import type { EtrDocumentListItem } from '../types';

/** A server-provided message is already resolved text; the generic case is translated at render time (see `EtrListPage`). */
export type UseAllEtrsError = { kind: 'server'; message: string } | { kind: 'generic' };

export function useAllEtrs() {
  const [etrs, setEtrs] = useState<EtrDocumentListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<UseAllEtrsError | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await listAllForUser();
      if (response.success && response.data) {
        setEtrs(response.data);
      } else {
        setError(response.message ? { kind: 'server', message: response.message } : { kind: 'generic' });
      }
    } catch (err) {
      const message = apiErrorMessage(err, '');
      setError(message ? { kind: 'server', message } : { kind: 'generic' });
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  return { etrs, loading, error, refresh: load };
}
