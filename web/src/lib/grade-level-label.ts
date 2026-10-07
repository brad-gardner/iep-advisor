import i18n from './i18n';

// Human-facing labels for the canonical `GRADE_LEVEL_OPTIONS` values
// (`features/children/lib/child-profile-options.ts`) — translated via
// `common:gradeLevel.<value>` (see `locales/{en,es}/common.json`) for
// DISPLAY ONLY. The stored value (sent to the API, used in `<option value>`)
// stays the canonical English string regardless of the active language — see
// `docs/solutions/logic-errors/2026-10-06-free-text-to-dropdown-legacy-values-and-not-set-clear.md`
// on display labels vs. stored values.

/**
 * Map a canonical (or legacy/unmatched) grade-level string to its human,
 * translated label. A value with no translation entry — an unmatched legacy
 * string a parent typed before the dropdown existed — passes through
 * unchanged rather than rendering blank or a raw key.
 */
export function gradeLevelLabel(value: string | null | undefined): string {
  if (!value) return '';
  return i18n.t(`common:gradeLevel.${value}`, { defaultValue: value });
}
