import { useCallback, useEffect, useState } from 'react';
import { type LoadError, toLoadError } from '@/lib/api-error';
import { getAuthoredVersion } from '../api/documents-api';
import type { AuthoredDocumentVersionDetailDto } from '../types';

interface UseAuthoredVersionResult {
  version: AuthoredDocumentVersionDetailDto | null;
  isLoading: boolean;
  error: LoadError | null;
  /** Adopt a server response after a mutation (signed-artifact upload, amend). */
  applyUpdate: (patch: Partial<AuthoredDocumentVersionDetailDto>) => void;
}

/** Loads one finalized authored version's full frozen snapshot. */
export function useAuthoredVersion(versionId: number): UseAuthoredVersionResult {
  const [version, setVersion] = useState<AuthoredDocumentVersionDetailDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<LoadError | null>(null);

  useEffect(() => {
    if (!versionId) return;
    let active = true;
    getAuthoredVersion(versionId)
      .then((res) => {
        if (!active) return;
        if (res.success && res.data) setVersion(res.data);
        else setError(toLoadError(res));
      })
      .catch((err) => {
        if (!active) return;
        setError(toLoadError(err));
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
