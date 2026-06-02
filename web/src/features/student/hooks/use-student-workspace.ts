import { useCallback, useEffect, useState } from 'react';
import {
  createWorkspaceEntry,
  deleteWorkspaceEntry,
  getStudentWorkspace,
  requestInterviewSuggestion,
  updateWorkspaceEntry,
} from '../api/student-workspace-api';
import type {
  StudentWorkspaceEntryDto,
  StudentWorkspaceEntryKind,
} from '../types';

export type WorkspaceStatus = 'loading' | 'ready' | 'error';

interface AddEntryInput {
  entryKind: StudentWorkspaceEntryKind;
  content: string;
  isShareable: boolean;
}

/**
 * Loads the student's workspace once and exposes the entries plus local-state
 * mutators (add / update / delete / setShareable / interview). Each mutator
 * persists via the API and reflects the result into local state so the UI
 * updates without a full reload.
 */
export function useStudentWorkspace() {
  const [entries, setEntries] = useState<StudentWorkspaceEntryDto[]>([]);
  const [status, setStatus] = useState<WorkspaceStatus>('loading');

  // Fetches and reflects the workspace into local state. All setState calls live
  // in promise callbacks (not synchronously in any effect body) to avoid
  // cascading renders.
  const fetchInto = useCallback(
    () =>
      getStudentWorkspace()
        .then((response) => {
          if (response.success && response.data) {
            setEntries(response.data.entries ?? []);
            setStatus('ready');
          } else {
            setStatus('error');
          }
        })
        .catch(() => setStatus('error')),
    []
  );

  // User-triggered reload re-shows the loading state, then refetches.
  const reload = useCallback(() => {
    setStatus('loading');
    return fetchInto();
  }, [fetchInto]);

  useEffect(() => {
    // `status` already starts at 'loading'; the effect only kicks off the fetch.
    void fetchInto();
  }, [fetchInto]);

  const addEntry = useCallback(
    async (input: AddEntryInput): Promise<boolean> => {
      const response = await createWorkspaceEntry(input);
      if (response.success && response.data) {
        const created = response.data;
        setEntries((prev) => [...prev, created]);
        return true;
      }
      return false;
    },
    []
  );

  const updateEntry = useCallback(
    async (
      id: number,
      content: string,
      isShareable: boolean
    ): Promise<boolean> => {
      const response = await updateWorkspaceEntry(id, { content, isShareable });
      if (response.success && response.data) {
        const updated = response.data;
        setEntries((prev) => prev.map((e) => (e.id === id ? updated : e)));
        return true;
      }
      return false;
    },
    []
  );

  // Flips only the shareable flag, keeping the content unchanged (PUT requires
  // both fields). Optimistically updates, reverting on failure.
  const setShareable = useCallback(
    async (id: number, isShareable: boolean): Promise<boolean> => {
      const target = entries.find((e) => e.id === id);
      if (!target) return false;

      setEntries((prev) =>
        prev.map((e) => (e.id === id ? { ...e, isShareable } : e))
      );

      const response = await updateWorkspaceEntry(id, {
        content: target.content,
        isShareable,
      });
      if (response.success && response.data) {
        const updated = response.data;
        setEntries((prev) => prev.map((e) => (e.id === id ? updated : e)));
        return true;
      }

      // Revert on failure.
      setEntries((prev) =>
        prev.map((e) =>
          e.id === id ? { ...e, isShareable: target.isShareable } : e
        )
      );
      return false;
    },
    [entries]
  );

  const removeEntry = useCallback(async (id: number): Promise<boolean> => {
    const response = await deleteWorkspaceEntry(id);
    if (response.success) {
      setEntries((prev) => prev.filter((e) => e.id !== id));
      return true;
    }
    return false;
  }, []);

  // Returns the AI suggestion text (not persisted). The caller decides whether
  // to save it as an entry via addEntry.
  const interview = useCallback(
    async (prompt: string): Promise<string | null> => {
      const response = await requestInterviewSuggestion(prompt);
      if (response.success && response.data) {
        return response.data.suggestion;
      }
      return null;
    },
    []
  );

  return {
    entries,
    status,
    reload,
    addEntry,
    updateEntry,
    setShareable,
    removeEntry,
    interview,
  };
}
