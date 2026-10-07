import { AxiosError } from 'axios';
import i18n from '@/lib/i18n';

// Maps an assist/chat failure to a short, friendly message for educators.
export function friendlyAssistError(err: unknown): string {
  const status = err instanceof AxiosError ? err.response?.status : undefined;
  const serverMessage =
    err instanceof AxiosError
      ? (err.response?.data as { message?: string } | undefined)?.message
      : undefined;

  if (status === 503) return i18n.t('document-authoring:assistErrors.unavailable');
  if (status === 403) return i18n.t('document-authoring:assistErrors.forbidden');
  if (status === 400) return serverMessage || i18n.t('document-authoring:assistErrors.badRequestFallback');
  return serverMessage || i18n.t('document-authoring:assistErrors.genericFallback');
}
