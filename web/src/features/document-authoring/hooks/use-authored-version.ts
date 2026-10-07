import { useCallback, useEffect, useState } from 'react';
import { apiErrorMessage } from '@/lib/api-error';
import { getAuthoredVersion } from '../api/documents-api';
import type { AuthoredDocumentVersionDetailDto } from '../types';

/** A server-provided message is already resolved text; the generic case is translated at render time by the caller. */
export type UseAuthoredVersionError = { kind: 'server'; message: string } | { kind: 'generic' };

interface UseAuthoredVersionResult {
  version: AuthoredDocumentVersionDetailDto | null;
  isLoading: boolean;
  error: UseAuthoredVersionError | null;
  /** Adopt a server response after a mutation (signed-artifact upload, amend). */
  applyUpdate: (patch: Partial<AuthoredDocumentVersionDetailDto>) => void;
}

/** Loads one finalized authored version's full frozen snapshot. */
export function useAuthoredVersion(versionId: number): UseAuthoredVersionResult {
  const [version, setVersion] = useState<AuthoredDocumentVersionDetailDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<UseAuthoredVersionError | null>(null);

  useEffect(() => {
    if (!versionId) return;
    let active = true;
    getAuthoredVersion(versionId)
      .then((res) => {
        if (!active) return;
        if (res.success && res.data) setVersion(res.data);
        else setError(res.message ? { kind: 'server', message: res.message } : { kind: 'generic' });
      })
      .catch((err) => {
        if (!active) return;
        const message = apiErrorMessage(err, '');
        setError(message ? { kind: 'server', message } : { kind: 'generic' });
      })
      .finally(() => {
        if (active) setIsLoading(false);
      });
    return () => {
      active = false;
    };
  }, [versionId]);

  const applyUpdate = useCallback((patch: Partial<AuthoredDocumentVersionDetailDto>) => {
    setVersion((prev) => (prev ? { ...prev, ...patch } : prev));
  }, []);

  return { version, isLoading, error, applyUpdate };
}
