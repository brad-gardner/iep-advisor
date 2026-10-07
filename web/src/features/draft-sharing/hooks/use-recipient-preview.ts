import { useCallback, useEffect, useState } from 'react';
import { apiErrorMessage } from '@/lib/api-error';
import { getSharePreview } from '../api/draft-sharing-api';
import type { RecipientPreviewDto } from '../types';

/** A server-provided message is already resolved text; the generic case is translated at render time by the caller. */
export type UseRecipientPreviewError = { kind: 'server'; message: string } | { kind: 'generic' };

interface UseRecipientPreviewResult {
  preview: RecipientPreviewDto | null;
  isLoading: boolean;
  error: UseRecipientPreviewError | null;
  reload: () => void;
}

/**
 * Recipient preview for the "Share with family" gate: whether the district's
 * policy allows sharing at all (drives whether the button renders), who would
 * receive it, and whether this would supersede an existing revision. Fetched
 * on mount so the button can hide itself before the modal ever opens, and
 * refetchable so the modal shows freshest recipients right before a share.
 */
export function useRecipientPreview(instanceId: number): UseRecipientPreviewResult {
  const [preview, setPreview] = useState<RecipientPreviewDto | null>(null);
  const [error, setError] = useState<UseRecipientPreviewError | null>(null);
  const [retryToken, setRetryToken] = useState(0);

  useEffect(() => {
    if (!instanceId) return;
    let active = true;
    (async () => {
      try {
        const res = await getSharePreview(instanceId);
        if (!active) return;
        if (res.success && res.data) setPreview(res.data);
        else setError(res.message ? { kind: 'server', message: res.message } : { kind: 'generic' });
      } catch (err) {
        if (!active) return;
        const message = apiErrorMessage(err, '');
        setError(message ? { kind: 'server', message } : { kind: 'generic' });
      }
    })();
    return () => {
      active = false;
    };
  }, [instanceId, retryToken]);

  const reload = useCallback(() => setRetryToken((t) => t + 1), []);

  return { preview, isLoading: preview === null && error === null, error, reload };
}
