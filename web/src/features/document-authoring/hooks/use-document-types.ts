import { useEffect, useState } from 'react';
import type { DocumentTypeDto } from '@/features/admin/templates/types';
import { type LoadError, toLoadError } from '@/lib/api-error';
import { listDocumentTypes } from '../api/documents-api';

interface UseDocumentTypesResult {
  types: DocumentTypeDto[];
  isLoading: boolean;
  error: LoadError | null;
}

/** Loads the active document types. Mount a fresh instance per use (e.g. inside
 *  a modal body that only mounts when open) so state starts pending and the
 *  effect only resolves after the await — no synchronous setState. */
export function useDocumentTypes(): UseDocumentTypesResult {
  const [types, setTypes] = useState<DocumentTypeDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<LoadError | null>(null);

  useEffect(() => {
    let cancelled = false;
    listDocumentTypes()
      .then((res) => {
        if (cancelled) return;
        if (res.success && res.data) setTypes(res.data.filter((t) => t.isActive));
        else setError(toLoadError(res));
      })
      .catch(() => {
        if (!cancelled) setError({ kind: 'generic' });
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
