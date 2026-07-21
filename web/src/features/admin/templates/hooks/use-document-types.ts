import { useCallback, useEffect, useState } from 'react';
import { listDocumentTypes } from '../admin-templates-api';
import type { DocumentTypeDto } from '../types';

export function useDocumentTypes() {
  const [documentTypes, setDocumentTypes] = useState<DocumentTypeDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  // Bumped by reload() to re-run the fetch effect. The effect body only calls
  // setState after an await, keeping it effect-safe (no synchronous setState).
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    let cancelled = false;
    listDocumentTypes()
      .then((res) => {
        if (cancelled) return;
        if (res.success && res.data) setDocumentTypes(res.data);
        else setError(res.message ?? 'Failed to load document types.');
      })
      .catch(() => {
        if (!cancelled) setError('Failed to load document types.');
      })
      .finally(() => {
        if (!cancelled) setIsLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [reloadKey]);

  const reload = useCallback(() => {
    setIsLoading(true);
    setError(null);
    setReloadKey((k) => k + 1);
  }, []);

  return { documentTypes, isLoading, error, reload };
}
