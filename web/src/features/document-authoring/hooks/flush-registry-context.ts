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
    return registry.register(key, flush);
  }, [registry, key, flush]);
}
