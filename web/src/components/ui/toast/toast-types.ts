export type ToastVariant = 'success' | 'error' | 'info';

export interface ToastOptions {
  message: string;
  variant?: ToastVariant;
}

export interface ToastItem {
  id: number;
  message: string;
  variant: ToastVariant;
}
