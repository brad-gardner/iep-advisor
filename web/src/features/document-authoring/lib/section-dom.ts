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
