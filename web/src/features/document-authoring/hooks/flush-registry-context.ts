import { createContext, useContext, useEffect } from 'react';
import type { FlushRegistry } from '@/features/iep-authoring/hooks/use-flush-registry';

// Distributes a FlushRegistry from the editor down to each field renderer so
// finalize can flush every pending per-field autosave before snapshotting.
// Null when no provider is present (e.g. a read-only snapshot) → registration
// is a no-op. Reuses the iep-authoring FlushRegistry shape verbatim.
export const DocumentFlushContext = createContext<FlushRegistry | null>(null);

/** Register a field's autosave `flush` with the surrounding editor so a finalize
 *  can drain it. No-op outside a provider. `flush` must be stable (useAutosave's
 *  flush is memoized), so this effect registers once per field key. */
export function useRegisterFlush(key: string, flush: () => Promise<void>): void {
  const registry = useContext(DocumentFlushContext);
  useEffect(() => {
    if (!registry) return;
    const unregister = registry.register(key, flush);
    return () => {
      // Persist any pending edit before the field unmounts (e.g. user types then
      // navigates away before the 700ms debounce fires). Without this the debounce
      // timer is just cleared and the edit is silently lost — worst for Table cells,
      // which have no onBlur backstop. Fire-and-forget: the in-flight promise/refs
      // settle even though this component is gone, and all setState in the autosave
      // is mountedRef-guarded, so no unmounted-update warning fires.
      // Mirrors iep-authoring's use-row-autosave unmount cleanup.
      void flush();
      unregister();
    };
  }, [registry, key, flush]);
}
