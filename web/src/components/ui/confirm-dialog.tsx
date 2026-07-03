import { useId } from "react";
import { Button } from "./button";
import { Modal } from "./modal";
import { Notice } from "./notice";

interface ConfirmDialogProps {
  open: boolean;
  /** Heading, e.g. "Delete report". */
  title: string;
  /** Consequence text — wired to the dialog via `aria-describedby`. */
  message: React.ReactNode;
  /**
   * Confirm button label. Name the action ("Delete report", "Revoke access"),
   * not a generic "OK", so the choice is clear from the button alone.
   */
  confirmLabel: string;
  cancelLabel?: string;
  /** Confirm styling — destructive actions use `danger` (the default). */
  confirmVariant?: "danger" | "primary";
  loading?: boolean;
  /** Server-side failure to keep rendered inside the open dialog. */
  error?: string | null;
  onConfirm: () => void;
  onCancel: () => void;
  "data-testid"?: string;
}

/**
 * Destructive-action confirmation. A `role="alertdialog"` Modal whose message
 * is the accessible description and whose **Cancel** button takes initial focus
 * (via `autoFocus`) so an accidental Enter/Space cannot trigger the destructive
 * path. Replaces native `confirm()`.
 */
export function ConfirmDialog({
  open,
  title,
  message,
  confirmLabel,
  cancelLabel = "Cancel",
  confirmVariant = "danger",
  loading = false,
  error,
  onConfirm,
  onCancel,
  "data-testid": testId,
}: ConfirmDialogProps) {
  const messageId = useId();

  return (
    <Modal
      open={open}
      onClose={onCancel}
      title={title}
      size="sm"
      role="alertdialog"
      describedById={messageId}
      hideCloseButton
      data-testid={testId}
      footer={
        <>
          <Button
            variant="ghost"
            onClick={onCancel}
            disabled={loading}
            autoFocus
            data-testid={testId ? `${testId}-cancel` : undefined}
          >
            {cancelLabel}
          </Button>
          <Button
            variant={confirmVariant}
            onClick={onConfirm}
            loading={loading}
            data-testid={testId ? `${testId}-confirm` : undefined}
          >
            {confirmLabel}
          </Button>
        </>
      }
    >
      <div className="space-y-3">
        <p id={messageId} className="text-sm text-brand-slate-600">
          {message}
        </p>
        {error && (
          <div role="alert">
            <Notice variant="error" title={error} />
          </div>
        )}
      </div>
    </Modal>
  );
}
