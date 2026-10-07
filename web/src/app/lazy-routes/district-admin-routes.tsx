// Barrel for the district-admin pages under `/educator/admin/*` plus the
// district first-run wizard at `/educator/setup` (its own chunk, separate
// from `staff-routes.tsx` — see that file's doc comment, and the
// multilingual plan's phase-5 "one chunk per area is fine").
//
// No namespace is staff/admin-split here yet (none of these pages have
// converted to i18n at all, let alone to a staff namespace) — this import
// is still needed anyway, because `features/educator`'s shared components
// (e.g. the school filter on the compliance board) render here too and
// already use staff namespaces. A future phase converting one of THIS
// barrel's own pages needs no new import here either — see
// `./staff-locales.ts` and `docs/i18n/README.md`'s "Staff and admin
// namespaces".
import './staff-locales';

export { DistrictSchoolsPage } from '@/features/district-admin/pages/district-schools-page';
export { ComplianceBoardPage } from '@/features/district-admin/pages/compliance-board-page';
export { DistrictAuditLogPage } from '@/features/district-admin/pages/district-audit-log-page';
export { DistrictSetupWizard } from '@/features/district-admin/pages/district-setup-wizard';
export { DistrictStaffPage } from '@/features/staff-invites/pages/district-staff-page';
export { ImportPage } from '@/features/roster-import/pages/import-page';
export { ExportsAdminPage } from '@/features/exports/pages/exports-admin-page';
