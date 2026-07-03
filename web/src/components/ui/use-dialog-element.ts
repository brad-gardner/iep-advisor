import { useEffect, useRef } from "react";
import { lockBodyScroll, unlockBodyScroll } from "@/lib/scroll-lock";

/**
 * Shared native-`<dialog>` plumbing for the overlay primitives (Modal, Drawer).
 * Drives the element from a controlled `open` prop and returns a single-fire
 * close contract:
 *
 * - `showModal()` / `close()` are called in an effect, feature-guarded for
 *   jsdom (which implements neither) so tests still render the element.
 * - Body scroll is locked via the ref-counted helper, so nested dialogs stay
 *   locked until the last one closes.
 * - `handleCancel` intercepts the native `cancel` (Esc) event, prevents the
 *   dialog from self-closing (the parent owns `open`), and forwards one close.
 * - `handleBackdropClick` closes only on a true backdrop hit (the `<dialog>`
 *   itself is the event target), never on clicks inside the content.
 */
export function useDialogElement(open: boolean, onClose: () => void) {
  const dialogRef = useRef<HTMLDialogElement>(null);

  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog) return;
    if (open) {
      if (dialog.open) return;
      if (typeof dialog.showModal === "function") dialog.showModal();
      else dialog.setAttribute("open", "");
    } else if (dialog.open) {
      if (typeof dialog.close === "function") dialog.close();
      else dialog.removeAttribute("open");
    }
  }, [open]);

  useEffect(() => {
    if (!open) return;
    lockBodyScroll();
    return unlockBodyScroll;
  }, [open]);

  const handleCancel = (event: React.SyntheticEvent<HTMLDialogElement>) => {
    event.preventDefault();
    onClose();
  };

  const handleBackdropClick = (event: React.MouseEvent<HTMLDialogElement>) => {
    if (event.target === dialogRef.current) onClose();
  };

  return { dialogRef, handleCancel, handleBackdropClick };
}
