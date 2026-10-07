import { registerEnglishNamespace } from '@/lib/i18n';
import enFamilyContact from '@/locales/en/staff/family-contact.json';

// Registers the `family-contact` namespace's English synchronously, at
// module top level — importing this file (as the staff lazy route chunk
// does, in `app/lazy-routes/staff-routes.tsx`, since `FamilyContactCard`
// only ever renders from the educator student detail page) is itself what
// loads it. This keeps the namespace's English OUT of the eager main-chunk
// bundle (`lib/i18n/index.ts` only globs direct children of `locales/en/`,
// never `locales/en/staff/`) — see `features/educator/staff-locales.ts` for
// the full reasoning (the worked example this follows).
//
// A unit test that renders a `family-contact`-namespace component directly
// (bypassing the lazy route) must import this file first, the same way the
// real route chunk does — see `staff-locales.test.ts` and
// `family-contact-label.test.ts`.
registerEnglishNamespace('family-contact', enFamilyContact);
