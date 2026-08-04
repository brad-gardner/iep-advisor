import { useEffect, useState } from 'react';
import { getAuthoredVersion } from '../api/documents-api';
import type { AuthoredDocumentVersionDetailDto } from '../types';

interface UseAuthoredVersionResult {
  version: AuthoredDocumentVersionDetailDto | null;
  isLoading: boolean;
  error: string | null;
}

/** Loads one finalized authored version's full frozen snapshot. */
export function useAuthoredVersion(versionId: number): UseAuthoredVersionResult {
  const [version, setVersion] = useState<AuthoredDocumentVersionDetailDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!versionId) return;
    let active = true;
    getAuthoredVersion(versionId)
      .then((res) => {
        if (!active) return;
        if (res.success && res.data) setVersion(res.data);
        else setError(res.message ?? 'This version is unavailable.');
      })
      .catch(() => {
        if (active) setError('This version is unavailable.');
      })
      .finally(() => {
        if (active) setIsLoading(false);
      });
    return () => {
      active = false;
    };
  }, [versionId]);

  return { version, isLoading, error };
}
