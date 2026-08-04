import { useCallback, useEffect, useState } from 'react';
import { AxiosError } from 'axios';
import { createTemplate, listTemplates } from '../admin-templates-api';
import type { ApiResponse } from '@/types/api';
import type { CreateTemplateRequest, DocumentTemplateDto } from '../types';

export interface CreateTemplateResult {
  success: boolean;
  /** Backend failure message (e.g. duplicate (state, type)) when success is false. */
  message?: string;
}

export function useTemplates() {
  const [templates, setTemplates] = useState<DocumentTemplateDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  // Bumped to re-run the fetch effect (retry button and post-create refresh).
  // The effect body only calls setState after an await, so it stays effect-safe.
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    let cancelled = false;
    listTemplates()
      .then((res) => {
        if (cancelled) return;
        if (res.success && res.data) setTemplates(res.data);
        else setError(res.message ?? 'Failed to load templates.');
      })
      .catch(() => {
        if (!cancelled) setError('Failed to load templates.');
      })
      .finally(() => {
        if (!cancelled) setIsLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [reloadKey]);

  const reload = useCallback(() => {
    setIsLoading(true);
    setError(null);
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
        return { success: false, message: res.message ?? 'Failed to create template.' };
      } catch (err) {
        // A 400 (duplicate / invalid state / unknown type / blank name) rejects
        // with the ApiResponse envelope in the body — surface its message.
        const message =
          err instanceof AxiosError
            ? (err.response?.data as ApiResponse<unknown> | undefined)?.message
            : undefined;
        return { success: false, message: message ?? 'Failed to create template.' };
      }
    },
    []
  );

  return { templates, isLoading, error, reload, create };
}
