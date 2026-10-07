import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { listDocumentTypes } from '../admin-templates-api';
import type { DocumentTypeDto } from '../types';

// Same `{server message} | {generic}` shape as `useTemplates` — see that
// file's comment, and `docs/i18n/README.md`'s note on never putting `t` in a
// mount-effect's dependency array.
type LoadError = { kind: 'server'; message: string } | { kind: 'generic' };

export function useDocumentTypes() {
  const { t } = useTranslation('admin');
  const [documentTypes, setDocumentTypes] = useState<DocumentTypeDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<LoadError | null>(null);
  // Bumped by reload() to re-run the fetch effect. The effect body only calls
  // setState after an await, keeping it effect-safe (no synchronous setState).
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    let cancelled = false;
    listDocumentTypes()
      .then((res) => {
        if (cancelled) return;
        if (res.success && res.data) {
          setDocumentTypes(res.data);
          setLoadError(null);
        } else {
          setLoadError(res.message ? { kind: 'server', message: res.message } : { kind: 'generic' });
        }
      })
      .catch(() => {
        if (!cancelled) setLoadError({ kind: 'generic' });
      })
      .finally(() => {
        if (!cancelled) setIsLoading(false);
      });
    return () => {
      cancelled = true;
    };
    // `t` deliberately excluded — see the `LoadError` comment above.
  }, [reloadKey]);

  const error = loadError
    ? loadError.kind === 'server'
      ? loadError.message
      : t('templates.createModal.errorDocTypesLoadFailed')
    : null;

  const reload = useCallback(() => {
    setIsLoading(true);
    setLoadError(null);
    setReloadKey((k) => k + 1);
  }, []);

  return { documentTypes, isLoading, error, reload };
}
