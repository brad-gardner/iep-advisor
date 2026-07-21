import { useCallback, useEffect, useState } from 'react';
import { listDocuments } from '../api/documents-api';
import type { DocumentInstanceSummaryDto } from '../types';

interface UseDocumentListResult {
  documents: DocumentInstanceSummaryDto[];
  isLoading: boolean;
  error: string | null;
  /** Drop a document from the list after a successful delete. */
  removeDocument: (id: number) => void;
}

/** Loads a student's authored document instances (summaries). */
export function useDocumentList(studentId: number): UseDocumentListResult {
  const [documents, setDocuments] = useState<DocumentInstanceSummaryDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    // isLoading/error already start pending, so the effect only resolves them.
    let cancelled = false;
    listDocuments(studentId)
      .then((res) => {
        if (cancelled) return;
        if (res.success && res.data) setDocuments(res.data);
        else setError(res.message ?? 'Failed to load documents.');
      })
      .catch(() => {
        if (!cancelled) setError('Failed to load documents.');
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
