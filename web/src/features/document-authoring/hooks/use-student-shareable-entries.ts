import { useCallback, useRef, useState } from 'react';
import { getEducatorShareableEntries } from '@/features/student/api/shareable-entries-api';
import type { StudentWorkspaceEntryDto } from '@/features/student/types';

type LoadState = 'idle' | 'loading' | 'ready' | 'error';

export interface StudentShareableEntries {
  entries: StudentWorkspaceEntryDto[];
  isLoading: boolean;
  isError: boolean;
  ensureLoaded: () => Promise<void>;
}

/**
 * Lazily loads the student's shareable entries (educator scope) the first time
 * any picker asks for them, then caches the result. One instance lives in the
 * editor context so every "Pull from student" button in a document shares it.
 * Re-entry is guarded by a ref so back-to-back calls (or StrictMode's double
 * invocation) never issue two requests.
 */
export function useStudentShareableEntries(studentId: number): StudentShareableEntries {
  const [entries, setEntries] = useState<StudentWorkspaceEntryDto[]>([]);
  const [state, setState] = useState<LoadState>('idle');
  const stateRef = useRef<LoadState>('idle');

  const ensureLoaded = useCallback(async () => {
    if (stateRef.current === 'loading' || stateRef.current === 'ready') return;
    stateRef.current = 'loading';
    setState('loading');
    try {
      const response = await getEducatorShareableEntries(studentId);
      if (response.success && response.data) {
        setEntries(response.data);
        stateRef.current = 'ready';
        setState('ready');
      } else {
        stateRef.current = 'error';
        setState('error');
      }
    } catch {
      stateRef.current = 'error';
      setState('error');
    }
  }, [studentId]);

  return {
    entries,
    isLoading: state === 'loading',
    isError: state === 'error',
    ensureLoaded,
  };
}
