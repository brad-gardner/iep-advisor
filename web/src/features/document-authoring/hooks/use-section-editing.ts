import { useCallback, useMemo, useRef, useState } from 'react';

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
  /** A `SectionCard` registers a getter for "do I currently have an unresolved
   *  save failure" (same shape as `FlushRegistry.register`: returns an
   *  unregister cleanup). `hasFailures()` below reads every registered getter
   *  fresh, so Finalize always sees the latest state without this controller
   *  needing to re-render on every field save. */
  registerFailureStatus: (sectionId: number, hasFailures: () => boolean) => () => void;
  /** True if ANY registered section currently reports an unresolved failure —
   *  Finalize refuses to close sections (and snapshot) while this is true. */
  hasFailures: () => boolean;
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

  // Plain ref map, not state: a section's failure status changes on every
  // field save, far more often than anything here needs to re-render for —
  // callers (Finalize) only ever read it on demand, right before deciding
  // whether to close everything.
  const failureGettersRef = useRef<Map<number, () => boolean>>(new Map());
  const registerFailureStatus = useCallback((sectionId: number, hasFailures: () => boolean) => {
    failureGettersRef.current.set(sectionId, hasFailures);
    return () => {
      failureGettersRef.current.delete(sectionId);
    };
  }, []);
  const hasFailures = useCallback(
    () => [...failureGettersRef.current.values()].some((getter) => getter()),
    []
  );

  return useMemo(
    () => ({ isOpen, open, close, closeAll, anyOpen: openIds.size > 0, registerFailureStatus, hasFailures }),
    [isOpen, open, close, closeAll, openIds, registerFailureStatus, hasFailures]
  );
}
