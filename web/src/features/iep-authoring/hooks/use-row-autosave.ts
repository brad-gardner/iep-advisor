import { useEffect } from 'react';
import { useAutosave } from './use-autosave';
import type { FlushRegistry } from './use-flush-registry';
import type { SaveStatusBus } from './use-save-status-bus';

interface UseRowAutosaveArgs {
  rowKey: string;
  save: () => Promise<void>;
  bus: SaveStatusBus;
  registry: FlushRegistry;
}

// Wires a row's persist fn into autosave, reports status to the header bus, and
// registers the row's flush so the workspace can flush it before navigating away.
export function useRowAutosave({ rowKey, save, bus, registry }: UseRowAutosaveArgs) {
  const autosave = useAutosave<void>(() => save(), { delay: 700 });
  const { status, flush, cancel } = autosave;

  useEffect(() => {
    bus.report(rowKey, status);
  }, [bus, rowKey, status]);

  useEffect(() => {
    const unregister = registry.register(rowKey, flush);
    return () => {
      // Persist any pending edit before the row unmounts (e.g. user types then
      // switches tabs before the debounce fires). Fire-and-forget: the in-flight
      // promise/refs complete even though this component is gone, and all setState
      // in the autosave is mountedRef-guarded so no unmounted-update warning fires.
      void flush();
      // Drop this row from the bus when it unmounts (e.g. tab switch).
      bus.report(rowKey, 'idle');
      unregister();
    };
  }, [registry, bus, rowKey, flush]);

  return { schedule: () => autosave.save(undefined), cancel };
}
