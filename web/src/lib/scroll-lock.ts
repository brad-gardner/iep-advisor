// Ref-counted body scroll-lock shared across all overlays (Modal, Drawer,
// ConfirmDialog). Counting is what makes nested dialogs safe: a Modal that
// opens a ConfirmDialog must keep the page locked until BOTH close — so the
// lock is only released when the count returns to zero.
//
// The iOS-safe technique is `position: fixed` on <body> (plain `overflow:
// hidden` does not stop touch scroll on iOS Safari). We save the exact scroll
// offset on the first lock and restore it on the final unlock so the page does
// not jump.

let lockCount = 0;
let savedScrollY = 0;

export function lockBodyScroll(): void {
  if (typeof document === 'undefined') return;
  if (lockCount === 0) {
    savedScrollY = window.scrollY;
    const { style } = document.body;
    style.position = 'fixed';
    style.top = `-${savedScrollY}px`;
    style.left = '0';
    style.right = '0';
    // Keep the scrollbar gutter so the page does not shift horizontally.
    style.overflowY = 'scroll';
    style.overscrollBehavior = 'contain';
  }
  lockCount += 1;
}

export function unlockBodyScroll(): void {
  if (typeof document === 'undefined') return;
  lockCount = Math.max(0, lockCount - 1);
  if (lockCount === 0) {
    const { style } = document.body;
    style.position = '';
    style.top = '';
    style.left = '';
    style.right = '';
    style.overflowY = '';
    style.overscrollBehavior = '';
    window.scrollTo(0, savedScrollY);
  }
}
