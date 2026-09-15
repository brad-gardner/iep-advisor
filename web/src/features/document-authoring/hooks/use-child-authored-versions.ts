import { useEffect, useState } from 'react';
import { listAuthoredVersionsForChild } from '../api/documents-api';
import type { AuthoredDocumentVersionSummaryDto } from '../types';

interface UseChildAuthoredVersionsResult {
  versions: AuthoredDocumentVersionSummaryDto[];
  isLoading: boolean;
}

/** Parent-side list of finalized template documents for a linked child. A
 *  child with no school link simply yields an empty list. */
export function useChildAuthoredVersions(childId: number): UseChildAuthoredVersionsResult {
  const [versions, setVersions] = useState<AuthoredDocumentVersionSummaryDto[]>([]);
  // Pending until the first fetch settles; an invalid child id settles immediately.
  const [isLoading, setIsLoading] = useState(Boolean(childId));

  useEffect(() => {
    if (!childId) return;
    let active = true;
    listAuthoredVersionsForChild(childId)
      .then((res) => {
        if (active && res.success && res.data) setVersions(res.data);
      })
      .catch(() => {
        // Non-critical: the section renders empty.
      })
      .finally(() => {
        if (active) setIsLoading(false);
      });
    return () => {
      active = false;
    };
  }, [childId]);

  return { versions, isLoading };
}
