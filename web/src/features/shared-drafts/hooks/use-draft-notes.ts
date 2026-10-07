import { useCallback, useEffect, useState } from 'react';
import { type LoadError, toLoadError, apiErrorMessage } from '@/lib/api-error';
import { deleteDraftNote, getDraftNotes } from '../api/shared-drafts-api';
import type { ParentDraftNoteDto } from '../types';

interface UseDraftNotesResult {
  notes: ParentDraftNoteDto[];
  isLoading: boolean;
  error: LoadError | null;
  /** Append a freshly-answered note (from `askDraftQuestion`) to the list. */
  addNote: (note: ParentDraftNoteDto) => void;
  /** `message` is the server's own text when it supplied one; the caller
   *  (`AskQuestionDrawer`) falls back to its own translated generic text
   *  when it's `undefined` — same reasoning as the load error above. */
  removeNote: (noteId: number) => Promise<{ ok: boolean; message?: string }>;
}

/**
 * This parent's own private Q&A notes for a revision — one list, filtered
 * per-card by `targetFieldKey`/`targetRowId`. Private to the parent; staff have
 * no endpoint for these at all.
 */
export function useDraftNotes(revisionId: number): UseDraftNotesResult {
  const [notes, setNotes] = useState<ParentDraftNoteDto[]>([]);
  const [error, setError] = useState<LoadError | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    if (!revisionId) return;
    let active = true;
    (async () => {
      try {
        const res = await getDraftNotes(revisionId);
        if (!active) return;
        if (res.success && res.data) setNotes(res.data);
        else setError(toLoadError(res));
      } catch (err) {
        if (!active) return;
        setError(toLoadError(err));
      } finally {
        if (active) setIsLoading(false);
      }
    })();
    return () => {
      active = false;
    };
    // `t` deliberately excluded — the generic fallback is a KIND, translated
    // at render time by the sole consumer (`SharedDraftReviewPage`, via
    // `shared-drafts:notesLoadError`) rather than a string stored here.
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
      const serverMessage = apiErrorMessage(err, '');
      return { ok: false, message: serverMessage || undefined };
    }
  }, []);

  return { notes, isLoading, error, addNote, removeNote };
}
