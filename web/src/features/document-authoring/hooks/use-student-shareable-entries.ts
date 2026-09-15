import { useCallback, useState } from 'react';
import { getEducatorShareableEntries } from '@/features/student/api/shareable-entries-api';
import type { StudentWorkspaceEntryDto } from '@/features/student/types';

type LoadState = 'idle' | 'loading' | 'ready' | 'error';

/**
 * Lazily loads the student's shareable entries (educator scope) the first time
 * the picker is opened, then caches the result for subsequent opens.
 */
export function useStudentShareableEntries(studentId: number) {
  const [entries, setEntries] = useState<StudentWorkspaceEntryDto[]>([]);
  const [state, setState] = useState<LoadState>('idle');

  const ensureLoaded = useCallback(async () => {
    if (state === 'loading' || state === 'ready') return;
    setState('loading');
    try {
      const response = await getEducatorShareableEntries(studentId);
      if (response.success && response.data) {
        setEntries(response.data);
        setState('ready');
      } else {
        setState('error');
      }
    } catch {
      setState('error');
    }
  }, [studentId, state]);

  return {
    entries,
    isLoading: state === 'loading',
    isError: state === 'error',
    ensureLoaded,
  };
}
