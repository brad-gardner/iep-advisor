import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { loadErrorText, toLoadError, type LoadError } from '@/lib/api-error';
import { listDocumentTypes } from '../admin-templates-api';
import type { DocumentTypeDto } from '../types';

export function useDocumentTypes() {
  const { t } = useTranslation('admin');
  const [documentTypes, setDocumentTypes] = useState<DocumentTypeDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  // Same shared `LoadError` shape as `useTemplates` — see that file's
  // comment, and `docs/i18n/README.md`'s "Load errors: the shared
  // `LoadError` pattern".
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
          setLoadError(toLoadError(res));
        }
      })
      .catch((err) => {
        if (!cancelled) setLoadError(toLoadError(err));
      })
      .finally(() => {
        if (!cancelled) setIsLoading(false);
      });
    return () => {
      cancelled = true;
    };
    // `t` deliberately excluded — see the `loadError` comment above.
  }, [reloadKey]);

  const error = loadErrorText(loadError, t('templates.createModal.errorDocTypesLoadFailed'));

  const reload = useCallback(() => {
    setIsLoading(true);
    setLoadError(null);
    setReloadKey((k) => k + 1);
  }, []);

  return { documentTypes, isLoading, error, reload };
}
