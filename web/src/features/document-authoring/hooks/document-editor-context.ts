import { createContext, useContext } from 'react';

/** Identity the field renderers need to bind AI assist and "pull from student"
 *  to the document being edited. Provided once by DocumentEditor. */
export interface DocumentEditorContextValue {
  instanceId: number;
  studentId: number;
}

export const DocumentEditorContext = createContext<DocumentEditorContextValue | null>(null);

/** Optional accessor: renderers used outside an editor (previews) get null and
 *  simply hide the assist affordances. */
export function useDocumentEditorContext(): DocumentEditorContextValue | null {
  return useContext(DocumentEditorContext);
}
