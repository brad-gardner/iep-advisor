import { useCallback, useMemo, useRef } from 'react';

export interface FlushRegistry {
  // A row registers its flush fn; returns an unregister cleanup.
  register: (key: string, flush: () => Promise<void>) => () => void;
  // Flush every pending row save (used on a hard unload via beforeunload).
  flushAll: () => Promise<void>;
}

// Lets the workspace flush all in-flight row autosaves before navigation,
// without the parent needing to know about individual rows.
export function useFlushRegistry(): FlushRegistry {
  const mapRef = useRef<Map<string, () => Promise<void>>>(new Map());

  const register = useCallback((key: string, flush: () => Promise<void>) => {
    mapRef.current.set(key, flush);
    return () => {
      mapRef.current.delete(key);
    };
  }, []);

  const flushAll = useCallback(async () => {
    await Promise.all([...mapRef.current.values()].map((f) => f()));
  }, []);

  return useMemo(() => ({ register, flushAll }), [register, flushAll]);
}
