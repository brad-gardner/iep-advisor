import i18n from '@/lib/i18n';

/**
 * User-facing copy shared across the advocate feature (kept together so
 * tests and components agree). Each is a live `i18n.t()` call, not a frozen
 * constant, so a language switch mid-session is reflected immediately — see
 * `docs/i18n/README.md`'s "Display-label helpers" pattern. Tests call the
 * same function the components do, rather than asserting against a value
 * frozen at module-load time.
 */
export function privacyBannerCopy(): string {
  return i18n.t('advocate:copy.privacyBanner');
}

export function truncatedNoticeCopy(): string {
  return i18n.t('advocate:copy.truncatedNotice');
}

export function stoppedCopy(): string {
  return i18n.t('advocate:copy.stopped');
}

export function prepQuestionCopiedToast(): string {
  return i18n.t('advocate:copy.prepQuestionCopiedToast');
}

export function viewerNoticeCopy(): string {
  return i18n.t('advocate:copy.viewerNotice');
}

/** Three starter questions shown on a blank conversation. */
export function exampleQuestions(): string[] {
  return [i18n.t('advocate:copy.example1'), i18n.t('advocate:copy.example2'), i18n.t('advocate:copy.example3')];
}

/** Offered as a fourth example only when the child's journal has entries. */
export function journalExampleQuestion(): string {
  return i18n.t('advocate:copy.journalExample');
}

/** The "set your state" link text in `StateHint` — kept here (not in that component file) so the file keeps exporting only the component (`react-refresh/only-export-components`). */
export function stateHintCopy(): string {
  return i18n.t('advocate:stateHint.copy');
}
