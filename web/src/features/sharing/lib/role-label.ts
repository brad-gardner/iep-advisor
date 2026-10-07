import i18n from '@/lib/i18n';

/**
 * Human-facing label for a child-access share role (`owner`/`viewer`/
 * `collaborator`) — translated via `sharing:role.<name>` for DISPLAY ONLY.
 * The raw role name remains the source of truth for comparisons/permissions.
 * An unrecognized role (future addition, or a value this UI doesn't know
 * about yet) falls back to a capitalized version of itself rather than a
 * blank or raw key — mirrors `orgRoleLabel`'s fallback.
 */
export function sharingRoleLabel(role: string): string {
  switch (role) {
    case 'owner':
      return i18n.t('sharing:role.owner');
    case 'viewer':
      return i18n.t('sharing:role.viewer');
    case 'collaborator':
      return i18n.t('sharing:role.collaborator');
    default:
      // An unrecognized role (future addition, or a value this UI doesn't
      // know about yet) falls back to a capitalized version of itself
      // rather than a blank or raw key — mirrors `orgRoleLabel`'s fallback.
      return role.charAt(0).toUpperCase() + role.slice(1);
  }
}
