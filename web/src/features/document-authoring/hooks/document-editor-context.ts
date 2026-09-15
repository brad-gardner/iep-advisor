import { createContext, useContext } from 'react';
import type { StudentShareableEntries } from './use-student-shareable-entries';

/** The field (or table cell) that most recently had focus, and how to write into it. */
export interface ActiveFieldTarget {
  /** Stable identity: the field key, or `${fieldKey}:${rowKey}:${columnKey}` for a table cell.
   *  Renderers clear their own target by this id on unmount / row removal. */
  id: string;
  /** Resolved at read time so a table-cell label ("Goal 2 — Baseline") stays right after rows move. */
  label: () => string;
  /** Appends `text` to the field's current content (never replaces it) and flushes the save.
   *  A no-op once the field has become disabled. */
  apply: (text: string) => void;
}

/** Identity the field renderers need to bind AI assist and "pull from student"
 *  to the document being edited, plus the shared caches and the focused-field
 *  registry the Evidence drawer inserts into. Provided once by DocumentEditor. */
export interface DocumentEditorContextValue {
  instanceId: number;
  studentId: number;
  /** Shared, lazily-loaded cache of the student's shareable workspace entries. */
  shareableEntries: StudentShareableEntries;
  /** Renderers call this on focus so "Insert" in the Evidence drawer knows where to write. */
  setActiveField: (target: ActiveFieldTarget) => void;
  /** Drops the active target when its id equals `id` or starts with `${id}:` (a field and its cells). */
  clearActiveField: (id: string) => void;
}

export const DocumentEditorContext = createContext<DocumentEditorContextValue | null>(null);

/** Optional accessor: renderers used outside an editor (previews) get null and
 *  simply hide the assist affordances. */
export function useDocumentEditorContext(): DocumentEditorContextValue | null {
  return useContext(DocumentEditorContext);
}

/** Joins existing content and an inserted excerpt: blank content takes the excerpt as-is,
 *  otherwise they are separated by `separator` (a blank line for prose, a space for one-liners). */
export function appendText(current: string, text: string, separator = '\n\n'): string {
  return current.trim() ? `${current.replace(/\s+$/, '')}${separator}${text}` : text;
}
