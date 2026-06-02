import { useEffect } from 'react';
import { useBlocker } from 'react-router-dom';
import type { FlushRegistry } from './use-flush-registry';

// Blocks in-app navigation once, flushes all pending row saves, then proceeds.
// Also flushes on a hard unload (tab close / refresh) on a best-effort basis.
export function useFlushOnNavigate(registry: FlushRegistry) {
  const blocker = useBlocker(true);

  useEffect(() => {
    if (blocker.state === 'blocked') {
      void registry.flushAll().finally(() => blocker.proceed());
    }
  }, [blocker, registry]);

  useEffect(() => {
    const handler = () => {
      void registry.flushAll();
    };
    window.addEventListener('beforeunload', handler);
    return () => window.removeEventListener('beforeunload', handler);
  }, [registry]);
}
