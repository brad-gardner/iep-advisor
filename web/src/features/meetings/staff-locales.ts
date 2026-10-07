import { registerEnglishNamespace } from '@/lib/i18n';
import enMeetingsStaff from '@/locales/en/staff/meetings-staff.json';

// Registers the `meetings-staff` namespace's English synchronously, at
// module top level — importing this file (as the staff lazy route chunk
// does, in `app/lazy-routes/staff-routes.tsx`, since every component that
// uses this namespace — schedule-meeting-form, meeting-drawer,
// participant-list, meeting-manager-actions, decisions-panel/decision-form/
// edit-decision-dialog, student-meetings-card — only ever renders from the
// educator calendar or student detail pages) is itself what loads it. This
// keeps the namespace's English OUT of the eager main-chunk bundle
// (`lib/i18n/index.ts` only globs direct children of `locales/en/`, never
// `locales/en/staff/`) — see `features/educator/staff-locales.ts` for the
// full reasoning (the worked example this follows).
//
// Named `meetings-staff`, not `meetings`: the parent-facing RSVP page
// (`pages/meeting-rsvp-page.tsx`) and `rsvp-button-group.tsx` already use
// the EAGER `meetings` namespace (phase 3), so parents must never load this
// staff-only copy — see `docs/i18n/README.md`'s "Staff and admin
// namespaces" on why a staff namespace's name must be unique and why a
// shared parent component must never import a staff-only one.
//
// A unit test that renders a `meetings-staff`-namespace component directly
// (bypassing the lazy route) must import this file first, the same way the
// real route chunk does — see `staff-locales.test.ts` and
// `meeting-labels.test.ts`.
registerEnglishNamespace('meetings-staff', enMeetingsStaff);
