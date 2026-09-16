import { createContext, useContext } from 'react';
import type { UseDraftExplanationsResult } from './use-draft-explanations';
import type { DraftResponseDto, ParentDraftNoteDto } from '../types';

/** Shared, revision-scoped state every `DraftItemCard` (and the dialogs it
 *  opens) needs: the parent's own private notes and responses for this
 *  revision, and the lazily-loaded shared explanations. Provided once by
 *  `SharedDraftReviewPage` so each card doesn't re-fetch the same lists. */
export interface DraftReviewContextValue {
  revisionId: number;
  /** Responses can only be submitted against the Active revision. */
  canRespond: boolean;
  notes: ParentDraftNoteDto[];
  addNote: (note: ParentDraftNoteDto) => void;
  removeNote: (noteId: number) => Promise<{ ok: boolean; message?: string }>;
  responses: DraftResponseDto[];
  addResponse: (response: DraftResponseDto) => void;
  explanations: UseDraftExplanationsResult;
}

export const DraftReviewContext = createContext<DraftReviewContextValue | null>(null);

/** Cards used outside the review page (there are none today) get `null` and
 *  simply hide the Explain/Ask/Respond affordances. */
export function useDraftReviewContext(): DraftReviewContextValue | null {
  return useContext(DraftReviewContext);
}
