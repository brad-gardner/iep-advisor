/** Stable DOM id for a section card (navigator anchors + scroll-spy). */
export function sectionDomId(sectionId: number): string {
  return `document-section-${sectionId}`;
}

/**
 * Scroll a section into view AND move the sequential-focus start point to it
 * (the section cards carry `tabIndex={-1}`), so a keyboard user who activates
 * a nav link or presses `[` / `]` lands *in* the section — the next Tab goes to
 * its first field, not to the next nav link.
 */
export function jumpToSection(sectionId: number): void {
  const el = document.getElementById(sectionDomId(sectionId));
  if (!el) return;
  el.scrollIntoView({ behavior: 'smooth', block: 'start' });
  el.focus({ preventScroll: true });
}

/** Scroll to a field's control (falling back to its section) and focus it. */
export function jumpToField(fieldElementId: string, sectionId: number): void {
  const field = document.getElementById(fieldElementId);
  if (!field) {
    jumpToSection(sectionId);
    return;
  }
  field.scrollIntoView({ behavior: 'smooth', block: 'center' });
  const focusable = field.matches('input,textarea,select,[tabindex]')
    ? field
    : field.querySelector<HTMLElement>('input,textarea,select,[tabindex]');
  focusable?.focus({ preventScroll: true });
}

/**
 * Like `jumpToField`, but waits until the target is no longer inside a `hidden`
 * ancestor before scrolling/focusing (neither works on `display:none`). The
 * document page hides the editor behind the Converge tab and reveals it through
 * a router search-param update, which React Router applies as a *transition* —
 * so the reveal is not guaranteed to have committed by the next frame. Polls a
 * frame at a time, bounded; if the target never appears it jumps anyway so a
 * broken reveal degrades to the old behaviour rather than hanging.
 */
export function jumpToFieldWhenVisible(fieldElementId: string, sectionId: number, maxFrames = 60): void {
  const attempt = (framesLeft: number) => {
    const target = document.getElementById(fieldElementId) ?? document.getElementById(sectionDomId(sectionId));
    const hidden = target?.closest('[hidden]') != null;
    if (!hidden || framesLeft <= 0) {
      jumpToField(fieldElementId, sectionId);
      return;
    }
    requestAnimationFrame(() => attempt(framesLeft - 1));
  };
  requestAnimationFrame(() => attempt(maxFrames));
}
