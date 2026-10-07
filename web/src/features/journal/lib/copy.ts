import i18n from '@/lib/i18n';

/**
 * Shown wherever the journal has no entries yet — the card and the full
 * page. A live `i18n.t()` call (not a frozen constant) so a language switch
 * mid-session is reflected immediately — see `docs/i18n/README.md`'s
 * "Display-label helpers" pattern.
 */
export function journalEmptyCopy(): string {
  return i18n.t('journal:emptyCopy');
}
