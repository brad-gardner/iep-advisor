// Barrel for the Educator ("staff") area of `/educator/*` (everything except
// the district-admin pages under `/educator/admin/*`, which are their own
// chunk — see `district-admin-routes.tsx`). `app/routes.tsx` points one
// `React.lazy(() => import('./staff-routes').then(...))` per page at THIS
// one module specifier, so Vite/Rollup bundles every page below into a
// single chunk rather than one chunk per page (the multilingual plan's
// phase-5 "one chunk per area is fine" — `docs/plans/
// 2026-10-06-001-feat-multilingual-english-spanish-plan.md`).
//
// The side-effect import below registers the `educator` namespace's English
// (`registerEnglishNamespace` in `lib/i18n/index.ts`) as this module
// evaluates — i.e. before any page re-exported here can render — so English
// never flashes a raw `ns:key` and never has to be fetched. A later worker
// adding a staff namespace to one of these pages (or a new staff page) adds
// its own `<feature>/staff-locales.ts` the same way and imports it here.

import './staff-locales';

export { StaffHomePage } from '@/features/home/pages/staff-home-page';
export { EducatorStudentsPage } from '@/features/educator/pages/educator-students-page';
export { EducatorStudentDetailPage } from '@/features/educator/pages/educator-student-detail-page';
export { EducatorCalendarPage } from '@/features/calendar/pages/educator-calendar-page';
export { MeetingBriefPage } from '@/features/meeting-brief/pages/meeting-brief-page';
export { DocumentListPage } from '@/features/document-authoring/pages/document-list-page';
export { DocumentEditorPage } from '@/features/document-authoring/pages/document-editor-page';
export { AuthoredVersionDetailPage } from '@/features/document-authoring/pages/authored-version-detail-page';
export { EducatorVersionDetailPage } from '@/features/iep-versions/components/educator-version-detail-page';
