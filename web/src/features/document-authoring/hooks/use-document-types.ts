import { useEffect, useState } from 'react';
import type { DocumentTypeDto } from '@/features/admin/templates/types';
import { listDocumentTypes } from '../api/documents-api';

interface UseDocumentTypesResult {
  types: DocumentTypeDto[];
  isLoading: boolean;
  error: string | null;
}

/** Loads the active document types. Mount a fresh instance per use (e.g. inside
 *  a modal body that only mounts when open) so state starts pending and the
 *  effect only resolves after the await — no synchronous setState. */
export function useDocumentTypes(): UseDocumentTypesResult {
  const [types, setTypes] = useState<DocumentTypeDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    listDocumentTypes()
      .then((res) => {
        if (cancelled) return;
        if (res.success && res.data) setTypes(res.data.filter((t) => t.isActive));
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
  }, []);

  return { types, isLoading, error };
}
