// Human-facing labels for the seeded org-role names. The raw enum names
// (`DistrictAdmin`/`SchoolAdmin`/`Teacher`) come straight from the API and read
// as internal identifiers, so map them to sentence-style labels for DISPLAY
// ONLY. Never use these for comparisons, routing, or testids — the raw
// `orgRoleName`/`ORG_ROLE` values remain the source of truth.
const ORG_ROLE_LABELS: Record<string, string> = {
  DistrictAdmin: 'District administrator',
  SchoolAdmin: 'School administrator',
  Teacher: 'Teacher',
};

/**
 * Map a raw org-role name to its human label. Unknown or empty values pass
 * through unchanged so an unexpected role never renders as a blank.
 */
export function orgRoleLabel(name: string | null | undefined): string {
  if (!name) return '';
  return ORG_ROLE_LABELS[name] ?? name;
}
