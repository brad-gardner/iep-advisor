import { registerEnglishNamespace } from '@/lib/i18n';
import enObligations from '@/locales/en/staff/obligations.json';

// Registers the `obligations` namespace's English synchronously, at module
// top level — importing this file (as the staff lazy route chunk does, in
// `app/lazy-routes/staff-routes.tsx`, since `ObligationStatusChip`/
// `StudentTimelineCard` only ever render from the educator student detail
// page — plus the home/calendar staff dashboards once those convert) is
// itself what loads it. This keeps the namespace's English OUT of the eager
// main-chunk bundle (`lib/i18n/index.ts` only globs direct children of
// `locales/en/`, never `locales/en/staff/`) — see
// `features/educator/staff-locales.ts` for the full reasoning (the worked
// example this follows).
//
// A unit test that renders an `obligations`-namespace component directly
// (bypassing the lazy route) must import this file first, the same way the
// real route chunk does — see `staff-locales.test.ts` and
// `obligation-label.test.ts`.
registerEnglishNamespace('obligations', enObligations);
