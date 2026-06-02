import { useEffect } from 'react';
import type { FlushRegistry } from './use-flush-registry';

// Best-effort flush of pending row saves on a hard unload (tab close / refresh).
//
// In-app route navigation is already covered without a router-level blocker: leaving
// the workspace unmounts the row components, and each row's autosave flushes its pending
// save in its unmount cleanup (see use-row-autosave). We deliberately do NOT use
// `useBlocker` here — it requires React Router's data router (createBrowserRouter), and
// this app uses the component <Routes>/<BrowserRouter> setup, so useBlocker throws.
export function useFlushOnNavigate(registry: FlushRegistry) {
  useEffect(() => {
    const handler = () => {
      void registry.flushAll();
    };
    window.addEventListener('beforeunload', handler);
    return () => window.removeEventListener('beforeunload', handler);
  }, [registry]);
}
