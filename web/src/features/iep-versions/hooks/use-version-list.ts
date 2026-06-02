import { useEffect, useState } from 'react';
import type { ApiResponse } from '@/types/api';
import {
  listVersionsForChild,
  listVersionsForStudent,
} from '../api/iep-versions-api';
import type { IepVersionSummaryDto } from '../types';

interface UseVersionListResult {
  versions: IepVersionSummaryDto[];
  isLoading: boolean;
}

function useVersionList(
  id: number,
  enabled: boolean,
  fetcher: (id: number) => Promise<ApiResponse<IepVersionSummaryDto[]>>
): UseVersionListResult {
  const [versions, setVersions] = useState<IepVersionSummaryDto[]>([]);
  // Pending until a fetch resolves; false immediately when the fetch is disabled.
  const [fetched, setFetched] = useState(false);

  useEffect(() => {
    if (!enabled || !id) return;
    let active = true;
    fetcher(id)
      .then((res) => {
        if (active && res.success && res.data) setVersions(res.data);
      })
      .catch(() => {
        // Non-critical: the section renders empty.
      })
      .finally(() => {
        if (active) setFetched(true);
      });
    return () => {
      active = false;
    };
  }, [id, enabled, fetcher]);

  const isLoading = enabled && Boolean(id) && !fetched;
  return { versions, isLoading };
}

// Educator: finalized versions for a school student.
export function useStudentVersions(studentId: number): UseVersionListResult {
  return useVersionList(studentId, true, listVersionsForStudent);
}

// Parent: finalized versions for the SchoolStudent linked to this child.
// Pass `enabled` (the SchoolSide flag) so it never fires when the feature is off.
export function useChildVersions(childId: number, enabled: boolean): UseVersionListResult {
  return useVersionList(childId, enabled, listVersionsForChild);
}
