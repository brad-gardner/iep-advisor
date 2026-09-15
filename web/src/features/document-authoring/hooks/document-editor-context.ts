import { createContext, useContext } from 'react';
import type { StudentShareableEntries } from './use-student-shareable-entries';

/** The field (or table cell) that most recently had focus, and how to write into it. */
export interface ActiveFieldTarget {
  label: string;
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
  setActiveField: (target: ActiveFieldTarget | null) => void;
}

export const DocumentEditorContext = createContext<DocumentEditorContextValue | null>(null);

/** Optional accessor: renderers used outside an editor (previews) get null and
 *  simply hide the assist affordances. */
export function useDocumentEditorContext(): DocumentEditorContextValue | null {
  return useContext(DocumentEditorContext);
}
