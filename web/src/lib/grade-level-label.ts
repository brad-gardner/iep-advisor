import i18n from './i18n';
import { normalizeGradeLevel } from '@/features/children/lib/child-profile-options';

// Human-facing labels for the canonical `GRADE_LEVEL_OPTIONS` values
// (`features/children/lib/child-profile-options.ts`) — translated via
// `common:gradeLevel.<value>` (see `locales/{en,es}/common.json`) for
// DISPLAY ONLY. The stored value (sent to the API, used in `<option value>`)
// stays the canonical English string regardless of the active language — see
// `docs/solutions/logic-errors/2026-10-06-free-text-to-dropdown-legacy-values-and-not-set-clear.md`
// on display labels vs. stored values.

/**
 * Map a canonical (or legacy/unmatched) grade-level string to its human,
 * translated label. `common:gradeLevel.*` is keyed by the CANONICAL form
 * (`"5th"`, `"Kindergarten"`, `"Pre-K"`, …), so a legacy/raw stored value —
 * `"5"`, `"K"`, `"PK"`, any case — is normalized to that canonical form
 * first (`normalizeGradeLevel`), the same mapping the child profile form
 * already uses to pick the matching `<option>`. Only a value that STILL
 * doesn't match anything after normalizing passes through unchanged, rather
 * than rendering blank or a raw key.
 */
export function gradeLevelLabel(value: string | null | undefined): string {
  if (!value) return '';
  const normalized = normalizeGradeLevel(value);
  return i18n.t(`common:gradeLevel.${normalized}`, { defaultValue: value });
}
