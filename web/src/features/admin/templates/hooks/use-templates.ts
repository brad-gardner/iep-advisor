import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AxiosError } from 'axios';
import { createTemplate, listTemplates } from '../admin-templates-api';
import type { ApiResponse } from '@/types/api';
import type { CreateTemplateRequest, DocumentTemplateDto } from '../types';

export interface CreateTemplateResult {
  success: boolean;
  /** Backend failure message (e.g. duplicate (state, type)) when success is false. */
  message?: string;
}

// A server-provided message is already resolved text and shown as-is; the
// generic fallback is translated at RENDER time (see `error` below) rather
// than load time, so a language switch after a failed load shows the new
// language immediately — see `docs/i18n/README.md`'s note on never putting
// `t` in a mount-effect's dependency array.
type LoadError = { kind: 'server'; message: string } | { kind: 'generic' };

export function useTemplates() {
  const { t } = useTranslation('admin');
  const [templates, setTemplates] = useState<DocumentTemplateDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<LoadError | null>(null);
  // Bumped to re-run the fetch effect (retry button and post-create refresh).
  // The effect body only calls setState after an await, so it stays effect-safe.
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    let cancelled = false;
    listTemplates()
      .then((res) => {
        if (cancelled) return;
        if (res.success && res.data) {
          setTemplates(res.data);
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
      : t('templates.list.loadFailedFallback')
    : null;

  const reload = useCallback(() => {
    setIsLoading(true);
    setLoadError(null);
    setReloadKey((k) => k + 1);
  }, []);

  const create = useCallback(
    async (data: CreateTemplateRequest): Promise<CreateTemplateResult> => {
      try {
        const res = await createTemplate(data);
        if (res.success) {
          // Refresh the list in the background; existing rows stay visible.
          setReloadKey((k) => k + 1);
          return { success: true };
        }
        return { success: false, message: res.message ?? t('templates.createModal.errorCreateFailed') };
      } catch (err) {
        // A 400 (duplicate / invalid state / unknown type / blank name) rejects
        // with the ApiResponse envelope in the body — surface its message.
        const message =
          err instanceof AxiosError
            ? (err.response?.data as ApiResponse<unknown> | undefined)?.message
            : undefined;
        return { success: false, message: message ?? t('templates.createModal.errorCreateFailed') };
      }
    },
    [t]
  );

  return { templates, isLoading, error, reload, create };
}
