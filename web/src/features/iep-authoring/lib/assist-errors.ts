import { AxiosError } from 'axios';

// Maps an assist/chat failure to a short, friendly message for educators.
export function friendlyAssistError(err: unknown): string {
  const status = err instanceof AxiosError ? err.response?.status : undefined;
  const serverMessage =
    err instanceof AxiosError
      ? (err.response?.data as { message?: string } | undefined)?.message
      : undefined;

  if (status === 503) return 'AI is temporarily unavailable. Please try again shortly.';
  if (status === 403) return "You don't have permission to use AI help here.";
  if (status === 400) return serverMessage || 'That request could not be processed.';
  return serverMessage || 'Something went wrong with AI help. Please try again.';
}
