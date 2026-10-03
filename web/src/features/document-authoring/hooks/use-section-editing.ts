import { useCallback, useMemo, useState } from 'react';

export interface SectionEditingController {
  /** Whether `sectionId` is currently in edit mode. */
  isOpen: (sectionId: number) => boolean;
  /** Open a section (Edit, or the "E" shortcut on the active section). No-op if
   *  already open — the keyboard "E" shortcut never silently discards/saves by
   *  itself; Done/Discard are explicit buttons. */
  open: (sectionId: number) => void;
  /** Close a section (Done / Discard have already flushed or restored). */
  close: (sectionId: number) => void;
  /** Close every open section (used by Finalize, after flushing). */
  closeAll: () => void;
  /** True while at least one section is open — gates the idle-flush interval. */
  anyOpen: boolean;
}

/**
 * Owns which sections are in edit mode. Several sections can be open at once;
 * each `SectionCard` keeps its own snapshot/save-state locally and reads its
 * openness from here so the page-level "E" shortcut and Finalize's
 * close-everything can act on any section without the section needing to
 * expose imperative handles.
 */
export function useSectionEditing(): SectionEditingController {
  const [openIds, setOpenIds] = useState<ReadonlySet<number>>(() => new Set());

  const isOpen = useCallback((sectionId: number) => openIds.has(sectionId), [openIds]);

  const open = useCallback((sectionId: number) => {
    setOpenIds((cur) => (cur.has(sectionId) ? cur : new Set(cur).add(sectionId)));
  }, []);

  const close = useCallback((sectionId: number) => {
    setOpenIds((cur) => {
      if (!cur.has(sectionId)) return cur;
      const next = new Set(cur);
      next.delete(sectionId);
      return next;
    });
  }, []);

  const closeAll = useCallback(() => {
    setOpenIds((cur) => (cur.size === 0 ? cur : new Set()));
  }, []);

  return useMemo(
    () => ({ isOpen, open, close, closeAll, anyOpen: openIds.size > 0 }),
    [isOpen, open, close, closeAll, openIds]
  );
}
