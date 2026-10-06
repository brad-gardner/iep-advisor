import i18n from './i18n';
import { getActiveLanguage } from './i18n/format';

// Compact "x ago" formatter for last-edited stamps. Not a React component, so
// it reads the active language directly off the i18next instance (via `t`)
// rather than through the `useTranslation` hook.
export function relativeTime(iso: string): string {
  const then = new Date(iso).getTime();
  if (Number.isNaN(then)) return '';
  const diffSec = Math.round((Date.now() - then) / 1000);

  if (diffSec < 45) return i18n.t('common:relativeTime.justNow');
  const diffMin = Math.round(diffSec / 60);
  // `n`, not `count` — i18next treats `count` as a pluralization trigger
  // (CLDR `_one`/`_other` key suffixes), and these units were never
  // pluralized ("1m ago", same as "5m ago"); plain interpolation matches
  // the original template-literal behavior exactly.
  if (diffMin < 60) return i18n.t('common:relativeTime.minutesAgo', { n: diffMin });
  const diffHr = Math.round(diffMin / 60);
  if (diffHr < 24) return i18n.t('common:relativeTime.hoursAgo', { n: diffHr });
  const diffDay = Math.round(diffHr / 24);
  if (diffDay < 7) return i18n.t('common:relativeTime.daysAgo', { n: diffDay });
  return new Date(then).toLocaleDateString(getActiveLanguage());
}
