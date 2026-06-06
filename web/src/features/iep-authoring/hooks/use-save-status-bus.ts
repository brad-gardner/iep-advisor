import { useCallback, useMemo, useRef, useState } from 'react';
import type { AutosaveStatus } from './use-autosave';

export interface SaveStatusBus {
  // Aggregated status across all rows for the header indicator.
  status: AutosaveStatus;
  // Each row reports its own status; the bus folds them into one.
  report: (rowKey: string, status: AutosaveStatus) => void;
}

// Folds many per-row autosave statuses into a single header status:
// any 'error' → error, else any 'saving' → saving, else any 'saved' → saved, else idle.
function fold(map: Map<string, AutosaveStatus>): AutosaveStatus {
  let sawSaving = false;
  let sawSaved = false;
  for (const s of map.values()) {
    if (s === 'error') return 'error';
    if (s === 'saving') sawSaving = true;
    if (s === 'saved') sawSaved = true;
  }
  if (sawSaving) return 'saving';
  if (sawSaved) return 'saved';
  return 'idle';
}

export function useSaveStatusBus(): SaveStatusBus {
  const mapRef = useRef<Map<string, AutosaveStatus>>(new Map());
  const [status, setStatus] = useState<AutosaveStatus>('idle');

  const report = useCallback((rowKey: string, rowStatus: AutosaveStatus) => {
    mapRef.current.set(rowKey, rowStatus);
    setStatus(fold(mapRef.current));
  }, []);

  return useMemo(() => ({ status, report }), [status, report]);
}
