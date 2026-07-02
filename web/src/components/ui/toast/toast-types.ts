export type ToastVariant = 'success' | 'error' | 'info';

export interface ToastOptions {
  message: string;
  variant?: ToastVariant;
  /** Time before auto-dismiss. Defaults to `DEFAULT_TOAST_DURATION_MS`. */
  durationMs?: number;
}

export interface ToastItem {
  id: number;
  message: string;
  variant: ToastVariant;
  durationMs: number;
  /**
   * Bumped each time an identical message is re-shown (deduped) so the card can
   * re-arm its auto-dismiss timer — the dwell tracks the latest mention, not the
   * first appearance.
   */
  seq: number;
}
