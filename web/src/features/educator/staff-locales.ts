import { registerEnglishNamespace } from '@/lib/i18n';
import enEducator from '@/locales/en/staff/educator.json';

// Registers the `educator` namespace's English synchronously, at module top
// level — so importing this file (as the staff lazy route chunk does, in
// `app/lazy-routes/staff-routes.tsx`) is itself what loads it. This keeps the
// namespace's English OUT of the eager main-chunk bundle (`lib/i18n/index.ts`
// only globs direct children of `locales/en/`, never `locales/en/staff/`)
// while still making it available, in English, with no flash of raw
// `ns:key` text, by the time any staff page that calls
// `useTranslation('educator')` actually renders — static JS module
// evaluation order guarantees this import runs to completion before the
// barrel module that imports it resolves, i.e. before React.lazy's promise
// settles and a page component can mount.
//
// A unit test that renders an `educator`-namespace component directly
// (bypassing the lazy route) must import this file first, the same way the
// real route chunk does — see `staff-locales.test.ts` and
// `educator-students-page.test.tsx`.
registerEnglishNamespace('educator', enEducator);
