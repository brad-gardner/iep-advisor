import i18n from './i18n';

// Human-facing labels for the seeded org-role names. The raw enum names
// (`DistrictAdmin`/`SchoolAdmin`/`Teacher`/…) come straight from the API and
// read as internal identifiers, so they're translated via
// `common:orgRole.<name>` (see `locales/{en,es}/common.json`) for DISPLAY
// ONLY. Never use these for comparisons, routing, or testids — the raw
// `orgRoleName`/`ORG_ROLE` values remain the source of truth.

/**
 * Map a raw org-role name to its human, translated label. Unknown or empty
 * values pass through unchanged so an unexpected role never renders as a
 * blank (or, now that this is translated, as a raw key).
 */
export function orgRoleLabel(name: string | null | undefined): string {
  if (!name) return '';
  return i18n.t(`common:orgRole.${name}`, { defaultValue: name });
}
