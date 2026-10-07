import i18n from '@/lib/i18n';

/**
 * Toasts shown when an advocate-suggested question is handed off to the
 * parent's own question list. Live `i18n.t()` calls (not frozen constants) so
 * a language switch mid-session is reflected immediately — see
 * `docs/i18n/README.md`'s "Display-label helpers" pattern. Exported as
 * functions (not from the component file) so tests call them the same way
 * the component does, and so `child-meeting-prep-tab.tsx` keeps exporting
 * only its component (`react-refresh/only-export-components`).
 */
export function questionAddedToast(): string {
  return i18n.t('meeting-prep:toast.questionAdded');
}
export function questionExistsToast(): string {
  return i18n.t('meeting-prep:toast.questionExists');
}
export function questionFailedToast(): string {
  return i18n.t('meeting-prep:toast.questionFailed');
}
