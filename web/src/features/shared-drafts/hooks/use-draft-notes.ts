import { useCallback, useEffect, useState } from 'react';
import { apiErrorMessage } from '@/lib/api-error';
import { deleteDraftNote, getDraftNotes } from '../api/shared-drafts-api';
import type { ParentDraftNoteDto } from '../types';

interface UseDraftNotesResult {
  notes: ParentDraftNoteDto[];
  isLoading: boolean;
  error: string | null;
  /** Append a freshly-answered note (from `askDraftQuestion`) to the list. */
  addNote: (note: ParentDraftNoteDto) => void;
  removeNote: (noteId: number) => Promise<{ ok: boolean; message?: string }>;
}

/**
 * This parent's own private Q&A notes for a revision — one list, filtered
 * per-card by `targetFieldKey`/`targetRowId`. Private to the parent; staff have
 * no endpoint for these at all.
 */
export function useDraftNotes(revisionId: number): UseDraftNotesResult {
  const [notes, setNotes] = useState<ParentDraftNoteDto[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    if (!revisionId) return;
    let active = true;
    (async () => {
      try {
        const res = await getDraftNotes(revisionId);
        if (!active) return;
        if (res.success && res.data) setNotes(res.data);
        else setError(res.message ?? 'Could not load your private notes.');
      } catch (err) {
        if (active) setError(apiErrorMessage(err, 'Could not load your private notes.'));
      } finally {
        if (active) setIsLoading(false);
      }
    })();
    return () => {
      active = false;
    };
  }, [revisionId]);

  const addNote = useCallback((note: ParentDraftNoteDto) => {
    setNotes((cur) => [note, ...cur]);
  }, []);

  const removeNote = useCallback(async (noteId: number) => {
    try {
      await deleteDraftNote(noteId);
      setNotes((cur) => cur.filter((n) => n.id !== noteId));
      return { ok: true };
    } catch (err) {
      return { ok: false, message: apiErrorMessage(err, 'Could not delete this note.') };
    }
  }, []);

  return { notes, isLoading, error, addNote, removeNote };
}
