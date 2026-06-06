import { createContext, useContext } from 'react';
import type { FlushRegistry } from './use-flush-registry';
import type { SaveStatusBus } from './use-save-status-bus';
import type { useIepDraft } from './use-iep-draft';

export interface IepEditorContextValue {
  draftId: number;
  // The school student this draft belongs to (route param). Used to pull
  // shareable entries from the student's self-advocacy workspace (P8b).
  studentId: number;
  currentUserId: number;
  bus: SaveStatusBus;
  registry: FlushRegistry;
  draftApi: ReturnType<typeof useIepDraft>;
}

export const IepEditorContext = createContext<IepEditorContextValue | null>(null);

export function useEditorContext(): IepEditorContextValue {
  const ctx = useContext(IepEditorContext);
  if (!ctx) throw new Error('useEditorContext must be used within IepEditorContext.Provider');
  return ctx;
}
