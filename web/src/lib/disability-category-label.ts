import i18n from './i18n';
import {
  DISABILITY_CATEGORIES,
  DISABILITY_CATEGORY_LABELS,
  type DisabilityCategory,
} from '@/features/educator/types';
import { normalizeDisabilityCategory } from '@/features/children/lib/child-profile-options';

// Human-facing labels for the canonical `DISABILITY_CATEGORY_OPTIONS` values
// (`features/children/lib/child-profile-options.ts`) — the readable English
// label strings (e.g. "Deaf-blindness") that are what's actually STORED on
// `ChildProfile.disabilityCategory` (a free-text field, unlike the
// educator side's `DisabilityCategory` enum). Translated via
// `common:disabilityCategory.<code>` (see `locales/{en,es}/common.json`,
// keyed by the IDEA category CODE, not the label text) for DISPLAY ONLY —
// the stored value never changes with the active language. See
// `docs/solutions/logic-errors/2026-10-06-free-text-to-dropdown-legacy-values-and-not-set-clear.md`.
const LABEL_TO_CODE = new Map<string, DisabilityCategory>(
  DISABILITY_CATEGORIES.map((code) => [DISABILITY_CATEGORY_LABELS[code], code])
);

/**
 * Map a canonical (or legacy/unmatched) disability-category label to its
 * human, translated label. A legacy/raw stored value — any case, the IDEA
 * code itself (`"SLD"`), or the server's own display string
 * (`"Specific Learning Disability"`, title case) — is normalized to the
 * exact canonical label first (`normalizeDisabilityCategory`, the same
 * mapping the child profile form uses to pick the matching `<option>`)
 * before looking up its code. Only a value that STILL doesn't match
 * anything — an unmatched legacy string a parent typed before the dropdown
 * existed — passes through unchanged, rather than rendering blank or a raw
 * key.
 */
export function disabilityCategoryLabel(value: string | null | undefined): string {
  if (!value) return '';
  const normalized = normalizeDisabilityCategory(value);
  const code = LABEL_TO_CODE.get(normalized);
  if (!code) return value;
  return i18n.t(`common:disabilityCategory.${code}`, { defaultValue: value });
}
