import { useId } from "react";
import { X } from "lucide-react";
import { cn } from "@/lib/cn";
import { useDialogElement } from "./use-dialog-element";

export type DrawerSize = "md" | "lg";

interface DrawerProps {
  /** Controlled visibility. Children are unmounted while `false`. */
  open: boolean;
  /** User-initiated close (Esc, backdrop, header X); fires once. */
  onClose: () => void;
  title: string;
  size?: DrawerSize;
  footer?: React.ReactNode;
  children: React.ReactNode;
  "data-testid"?: string;
}

const sizeStyles: Record<DrawerSize, string> = {
  md: "sm:max-w-md",
  lg: "sm:max-w-lg",
};

/**
 * Right-anchored panel for long / multi-section forms, on the same native
 * `<dialog>` substrate as Modal (shared a11y + open/close contract). It keeps
 * more list context visible than a centered Modal. Slides in under
 * `motion-safe`; a plain fade under `prefers-reduced-motion`.
 */
export function Drawer({
  open,
  onClose,
  title,
  size = "md",
  footer,
  children,
  "data-testid": testId,
}: DrawerProps) {
  const { dialogRef, handleCancel, handleBackdropClick } = useDialogElement(
    open,
    onClose,
  );
  const titleId = useId();

  return (
    <dialog
      ref={dialogRef}
      aria-modal={open ? true : undefined}
      aria-labelledby={titleId}
      data-testid={testId}
      onCancel={handleCancel}
      onClick={handleBackdropClick}
      // Pin the dialog to the right edge, full height. `mr-0 ml-auto` anchors
      // the (non-modal fallback) box; a modal dialog is centered by the UA so
      // the fixed inset below does the real positioning.
      className={cn(
        "fixed inset-y-0 right-0 left-auto m-0 h-full max-h-full w-[calc(100vw-2rem)] bg-transparent p-0",
        "backdrop:bg-brand-slate-800/50 backdrop:motion-safe:animate-overlay-in",
        sizeStyles[size],
      )}
    >
      {open && (
        <div className="motion-safe:animate-drawer-in flex h-full flex-col border-l border-brand-slate-200 bg-white shadow-xl">
          <div className="flex items-start justify-between gap-4 border-b border-brand-slate-100 px-6 py-4">
            <h2
              id={titleId}
              className="font-serif text-lg text-brand-slate-800"
            >
              {title}
            </h2>
            <button
              type="button"
              onClick={onClose}
              aria-label="Close dialog"
              data-testid={testId ? `${testId}-close` : undefined}
              className="-mr-1.5 -mt-0.5 rounded-button p-1 text-brand-slate-400 transition-colors hover:bg-brand-slate-50 hover:text-brand-slate-600 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-teal-400"
            >
              <X className="h-4 w-4" strokeWidth={1.8} aria-hidden="true" />
            </button>
          </div>

          <div className="flex-1 overflow-y-auto px-6 py-5">{children}</div>

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
