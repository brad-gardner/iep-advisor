import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { AdminUser } from '@/types/api';
import { getUsers } from '../api/admin-api';

export function useUsers() {
  const { t } = useTranslation('admin');
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  // A flag, not the translated string — translated at render, below, so a
  // language switch after a failed load shows the new language immediately
  // (see `docs/i18n/README.md`'s note on never putting `t` in a mount-effect's
  // dependency array).
  const [hasError, setHasError] = useState(false);
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
      setHasError(false);
      try {
        const data = await getUsers();
        if (!active) return;
        setUsers(data);
      } catch {
        if (active) setHasError(true);
      } finally {
        if (active) setIsLoading(false);
      }
    })();
    return () => {
      active = false;
    };
  }, [reloadKey]);

  const reload = useCallback(() => {
    setReloadKey((k) => k + 1);
  }, []);

  return { users, isLoading, error: hasError ? t('users.loadFailed') : null, reload };
}
