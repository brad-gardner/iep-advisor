import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { AdminUser } from '@/types/api';
import { loadErrorText, toLoadError, type LoadError } from '@/lib/api-error';
import { getUsers } from '../api/admin-api';

export function useUsers() {
  const { t } = useTranslation('admin');
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  // A flag (shared `LoadError` shape), not the translated string —
  // translated at render, below, so a language switch after a failed load
  // shows the new language immediately (see `docs/i18n/README.md`'s "Load
  // errors: the shared `LoadError` pattern").
  const [loadError, setLoadError] = useState<LoadError | null>(null);
  // Bumped by `reload()` to re-run the fetch effect (same shape as
  // `useDocumentTypes`/`useTemplates` elsewhere in this feature) — an inline
  // async IIFE directly in the effect, rather than a separately memoized
  // `load` callback invoked from the effect body, so every setState call
  // stays gated behind an `await` from the linter's point of view too.
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    let active = true;
    (async () => {
      setIsLoading(true);
      setLoadError(null);
      try {
        const data = await getUsers();
        if (!active) return;
        setUsers(data);
      } catch (err) {
        if (active) setLoadError(toLoadError(err));
      } finally {
        if (active) setIsLoading(false);
      }
    })();
    return () => {
      active = false;
    };
    // `t` deliberately excluded — see the `loadError` comment above.
  }, [reloadKey]);

  const reload = useCallback(() => {
    setReloadKey((k) => k + 1);
  }, []);

  return { users, isLoading, error: loadErrorText(loadError, t('users.loadFailed')), reload };
}
