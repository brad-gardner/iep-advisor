import { useCallback, useEffect, useState } from 'react';
import { apiErrorMessage } from '@/lib/api-error';
import { deleteDraftNote, getDraftNotes } from '../api/shared-drafts-api';
import type { ParentDraftNoteDto } from '../types';

// See `use-shared-draft-detail.ts`'s `SharedDraftDetailError` for why the
// generic fallback is a KIND, translated at render time by the sole consumer
// (`SharedDraftReviewPage`, via `shared-drafts:notesLoadError`) rather than a
// string stored here.
export type DraftNotesLoadError = { kind: 'server'; message: string } | { kind: 'generic' };

interface UseDraftNotesResult {
  notes: ParentDraftNoteDto[];
  isLoading: boolean;
  error: DraftNotesLoadError | null;
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
  const [error, setError] = useState<DraftNotesLoadError | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    if (!revisionId) return;
    let active = true;
    (async () => {
      try {
        const res = await getDraftNotes(revisionId);
        if (!active) return;
        if (res.success && res.data) setNotes(res.data);
        else setError(res.message ? { kind: 'server', message: res.message } : { kind: 'generic' });
      } catch (err) {
        if (!active) return;
        const serverMessage = apiErrorMessage(err, '');
        setError(serverMessage ? { kind: 'server', message: serverMessage } : { kind: 'generic' });
      } finally {
        if (active) setIsLoading(false);
      }
    })();
    return () => {
      active = false;
    };
    // `t` deliberately excluded — see `DraftNotesLoadError` above.
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
