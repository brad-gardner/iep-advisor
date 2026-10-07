// Barrel for the platform-admin area, `/admin/*` — its own chunk, separate
// from `staff-routes.tsx`/`district-admin-routes.tsx` (see those files' doc
// comments, and the multilingual plan's phase-5 "one chunk per area is
// fine"). `AdminRouteGuard` is included because every `/admin/*` route
// wraps its page with it (`app/routes.tsx`) — it's only ever needed once
// this chunk is already loading, so it belongs in the same chunk rather
// than the main one.
//
// No namespace is staff/admin-split here yet (none of these pages have
// converted to i18n at all, let alone to a staff namespace) — this import
// is still needed anyway, since shared staff components used across areas
// already use staff namespaces. A future phase converting one of THIS
// barrel's own pages needs no new import here either — see
// `./staff-locales.ts` and `docs/i18n/README.md`'s "Staff and admin
// namespaces".
import './staff-locales';

export { AdminRouteGuard } from '@/features/admin/components/admin-route-guard';
export { AdminDashboardPage } from '@/features/admin/components/admin-dashboard-page';
export { AdminUsersPage } from '@/features/admin/components/admin-users-page';
export { AdminUserDetail } from '@/features/admin/components/admin-user-detail';
export { TemplateListPage } from '@/features/admin/templates/template-list-page';
export { TemplateBuilderPage } from '@/features/admin/templates/template-builder-page';
export { AdminNotificationFailuresPage } from '@/features/notifications/pages/admin-notification-failures-page';
export { AdminEmailPage } from '@/features/admin/email/components/admin-email-page';
export { AdminAuditPage } from '@/features/admin/audit/components/admin-audit-page';
