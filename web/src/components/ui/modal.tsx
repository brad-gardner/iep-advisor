import { useId } from "react";
import { X } from "lucide-react";
import { cn } from "@/lib/cn";
import { useDialogElement } from "./use-dialog-element";

export type ModalSize = "sm" | "md" | "lg";

interface ModalProps {
  /** Controlled visibility. Children are unmounted while `false`. */
  open: boolean;
  /**
   * Requested close (Esc, backdrop click, or the header close button). The
   * parent owns `open` and is expected to flip it to `false`. Programmatic
   * closes (parent sets `open=false`) do NOT call this — it fires once per
   * user-initiated close.
   */
  onClose: () => void;
  /** Rendered as the dialog's `<h2>` and wired via `aria-labelledby`. */
  title: string;
  size?: ModalSize;
  /** Footer action row (typically the form's submit/cancel buttons). */
  footer?: React.ReactNode;
  /** `alertdialog` for destructive confirmations; defaults to `dialog`. */
  role?: "dialog" | "alertdialog";
  /** Id of the element describing the consequence, wired via `aria-describedby`. */
  describedById?: string;
  /** Hide the header close (X) — used by confirmations that focus Cancel. */
  hideCloseButton?: boolean;
  children: React.ReactNode;
  "data-testid"?: string;
}

const sizeStyles: Record<ModalSize, string> = {
  sm: "max-w-md",
  md: "max-w-lg",
  lg: "max-w-2xl",
};

/**
 * Modal built on the native `<dialog>` element. `showModal()` gives us
 * focus-trapping, background `inert`, top-layer stacking (above the app's
 * `z-50` ceiling), and return-focus for free — we only wire labelling, the
 * backdrop hit-test, scroll-lock, reduced-motion, and controlled open/close.
 *
 * jsdom does not implement `showModal()`/`close()`, so those calls are
 * feature-guarded; the element and its ARIA wiring still render for tests.
 */
export function Modal({
  open,
  onClose,
  title,
  size = "md",
  footer,
  role = "dialog",
  describedById,
  hideCloseButton = false,
  children,
  "data-testid": testId,
}: ModalProps) {
  const { dialogRef, handleCancel, handleBackdropClick } = useDialogElement(
    open,
    onClose,
  );
  const titleId = useId();

  return (
    <dialog
      ref={dialogRef}
      role={role}
      aria-modal={open ? true : undefined}
      aria-labelledby={titleId}
      aria-describedby={describedById}
      data-testid={testId}
      onCancel={handleCancel}
      onClick={handleBackdropClick}
      className={cn(
        "w-[calc(100vw-2rem)] bg-transparent p-0",
        "backdrop:bg-brand-slate-800/50 backdrop:motion-safe:animate-overlay-in",
        sizeStyles[size],
      )}
    >
      {open && (
        <div className="motion-safe:animate-modal-in overflow-hidden rounded-modal border border-brand-slate-200 bg-white shadow-xl">
          <div className="flex items-start justify-between gap-4 border-b border-brand-slate-100 px-6 py-4">
            <h2
              id={titleId}
              className="font-serif text-lg text-brand-slate-800"
            >
              {title}
            </h2>
            {!hideCloseButton && (
              <button
                type="button"
                onClick={onClose}
                aria-label="Close dialog"
                data-testid={testId ? `${testId}-close` : undefined}
                className="-mr-1.5 -mt-0.5 rounded-button p-1 text-brand-slate-400 transition-colors hover:bg-brand-slate-50 hover:text-brand-slate-600 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-teal-400"
              >
                <X className="h-4 w-4" strokeWidth={1.8} aria-hidden="true" />
              </button>
            )}
          </div>

          <div className="px-6 py-5">{children}</div>

          {footer && (
            <div className="flex items-center justify-end gap-2 border-t border-brand-slate-100 bg-brand-slate-50 px-6 py-4">
              {footer}
            </div>
          )}
        </div>
      )}
    </dialog>
  );
}
