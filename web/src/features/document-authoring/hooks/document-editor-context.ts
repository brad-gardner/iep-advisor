import { createContext, useContext } from 'react';
import type { StudentShareableEntries } from './use-student-shareable-entries';

/** Identity the field renderers need to bind AI assist and "pull from student"
 *  to the document being edited. Provided once by DocumentEditor. */
export interface DocumentEditorContextValue {
  instanceId: number;
  studentId: number;
  /** Shared, lazily-loaded cache of the student's shareable workspace entries. */
  shareableEntries: StudentShareableEntries;
}

export const DocumentEditorContext = createContext<DocumentEditorContextValue | null>(null);

/** Optional accessor: renderers used outside an editor (previews) get null and
 *  simply hide the assist affordances. */
export function useDocumentEditorContext(): DocumentEditorContextValue | null {
  return useContext(DocumentEditorContext);
}
