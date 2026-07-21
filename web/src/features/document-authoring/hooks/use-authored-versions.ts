import { useCallback, useEffect, useState } from 'react';
import { listAuthoredVersions } from '../api/documents-api';
import type { AuthoredDocumentVersionSummaryDto } from '../types';

interface UseAuthoredVersionsResult {
  versions: AuthoredDocumentVersionSummaryDto[];
  isLoading: boolean;
  error: string | null;
  /** Re-fetch the list (e.g. after a finalize creates a new version). */
  refresh: () => void;
}

/** Loads a student's finalized authored versions (newest first). `refresh`
 *  bumps a key so a finalize can pull the new version into the list. */
export function useAuthoredVersions(studentId: number): UseAuthoredVersionsResult {
  const [versions, setVersions] = useState<AuthoredDocumentVersionSummaryDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reloadKey, setReloadKey] = useState(0);

  // The effect body only calls setState after an await, keeping it effect-safe
  // (no synchronous setState). `refresh()` owns the pre-fetch loading/error reset.
  useEffect(() => {
    if (!studentId) return;
    let cancelled = false;
    listAuthoredVersions(studentId)
      .then((res) => {
        if (cancelled) return;
        if (res.success && res.data) setVersions(res.data);
        else setError(res.message ?? 'Failed to load finalized versions.');
      })
      .catch(() => {
        if (!cancelled) setError('Failed to load finalized versions.');
      })
      .finally(() => {
        if (!cancelled) setIsLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [studentId, reloadKey]);

  const refresh = useCallback(() => {
    setIsLoading(true);
    setError(null);
    setReloadKey((k) => k + 1);
  }, []);

  return { versions, isLoading, error, refresh };
}
