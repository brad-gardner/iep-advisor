import { useEffect, useState } from 'react';
import type { DocumentTypeDto } from '@/features/admin/templates/types';
import { listDocumentTypes } from '../api/documents-api';

/** A server-provided message is already resolved text; the generic case is translated at render time by the caller. */
export type UseDocumentTypesError = { kind: 'server'; message: string } | { kind: 'generic' };

interface UseDocumentTypesResult {
  types: DocumentTypeDto[];
  isLoading: boolean;
  error: UseDocumentTypesError | null;
}

/** Loads the active document types. Mount a fresh instance per use (e.g. inside
 *  a modal body that only mounts when open) so state starts pending and the
 *  effect only resolves after the await — no synchronous setState. */
export function useDocumentTypes(): UseDocumentTypesResult {
  const [types, setTypes] = useState<DocumentTypeDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<UseDocumentTypesError | null>(null);

  useEffect(() => {
    let cancelled = false;
    listDocumentTypes()
      .then((res) => {
        if (cancelled) return;
        if (res.success && res.data) setTypes(res.data.filter((t) => t.isActive));
        else setError(res.message ? { kind: 'server', message: res.message } : { kind: 'generic' });
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
