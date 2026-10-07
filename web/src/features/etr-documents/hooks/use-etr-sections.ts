import { useCallback, useEffect, useRef, useState } from 'react';
import { apiErrorMessage } from '@/lib/api-error';
import type { EtrSection } from '../types';
import { getSections } from '../api/etr-documents-api';

/** A server-provided message is already resolved text; the generic case is translated at render time (see `EtrSectionsList`). */
export type UseEtrSectionsError = { kind: 'server'; message: string } | { kind: 'generic' };

interface UseEtrSectionsResult {
  sections: EtrSection[];
  isLoading: boolean;
  error: UseEtrSectionsError | null;
  reload: () => Promise<void>;
}

/**
 * Loads ETR sections. Refetches when `status` transitions to `parsed`.
 */
export function useEtrSections(etrId: number, status: string | null): UseEtrSectionsResult {
  const [sections, setSections] = useState<EtrSection[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<UseEtrSectionsError | null>(null);
  const lastLoadedStatusRef = useRef<string | null>(null);

  const reload = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const response = await getSections(etrId);
      if (response.success && response.data) {
        const sorted = [...response.data].sort(
          (a, b) => a.displayOrder - b.displayOrder
        );
        setSections(sorted);
      } else {
        setError(response.message ? { kind: 'server', message: response.message } : { kind: 'generic' });
      }
    } catch (err) {
      const message = apiErrorMessage(err, '');
      setError(message ? { kind: 'server', message } : { kind: 'generic' });
    } finally {
      setIsLoading(false);
    }
  }, [etrId]);

  useEffect(() => {
    if (status !== 'parsed') {
      // Reset when the document is no longer parsed (e.g. on re-process).
      if (status && status !== lastLoadedStatusRef.current) {
        lastLoadedStatusRef.current = status;
        setSections([]);
      }
      return;
    }
    if (lastLoadedStatusRef.current === 'parsed') return;
    lastLoadedStatusRef.current = 'parsed';
    reload();
  }, [status, reload]);

  return { sections, isLoading, error, reload };
}
