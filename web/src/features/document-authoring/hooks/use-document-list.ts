import { useCallback, useEffect, useState } from 'react';
import { listDocuments } from '../api/documents-api';
import type { DocumentInstanceSummaryDto } from '../types';

/** A server-provided message is already resolved text; the generic case is translated at render time by the caller. */
export type UseDocumentListError = { kind: 'server'; message: string } | { kind: 'generic' };

interface UseDocumentListResult {
  documents: DocumentInstanceSummaryDto[];
  isLoading: boolean;
  error: UseDocumentListError | null;
  /** Drop a document from the list after a successful delete. */
  removeDocument: (id: number) => void;
}

/** Loads a student's authored document instances (summaries). */
export function useDocumentList(studentId: number): UseDocumentListResult {
  const [documents, setDocuments] = useState<DocumentInstanceSummaryDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<UseDocumentListError | null>(null);

  useEffect(() => {
    // isLoading/error already start pending, so the effect only resolves them.
    let cancelled = false;
    listDocuments(studentId)
      .then((res) => {
        if (cancelled) return;
        if (res.success && res.data) setDocuments(res.data);
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
  }, [studentId]);

  const removeDocument = useCallback((id: number) => {
    setDocuments((prev) => prev.filter((d) => d.id !== id));
  }, []);

  return { documents, isLoading, error, removeDocument };
}
