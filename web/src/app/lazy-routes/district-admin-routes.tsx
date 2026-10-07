// Barrel for the district-admin pages under `/educator/admin/*` plus the
// district first-run wizard at `/educator/setup` (its own chunk, separate
// from `staff-routes.tsx` — see that file's doc comment, and the
// multilingual plan's phase-5 "one chunk per area is fine").
//
// No namespace is staff/admin-split here yet (none of these pages have
// converted to i18n at all, let alone to a staff namespace) — a future
// phase's worker adds its own `<feature>/staff-locales.ts` (the same
// pattern as `features/educator/staff-locales.ts`) and imports it here, the
// same way `staff-routes.tsx` imports its own.
export { DistrictSchoolsPage } from '@/features/district-admin/pages/district-schools-page';
export { ComplianceBoardPage } from '@/features/district-admin/pages/compliance-board-page';
export { DistrictAuditLogPage } from '@/features/district-admin/pages/district-audit-log-page';
export { DistrictSetupWizard } from '@/features/district-admin/pages/district-setup-wizard';
export { DistrictStaffPage } from '@/features/staff-invites/pages/district-staff-page';
export { ImportPage } from '@/features/roster-import/pages/import-page';
export { ExportsAdminPage } from '@/features/exports/pages/exports-admin-page';
