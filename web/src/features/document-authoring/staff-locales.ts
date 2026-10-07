import { registerEnglishNamespace } from '@/lib/i18n';
import enDocumentAuthoring from '@/locales/en/staff/document-authoring.json';

// Registers the `document-authoring` namespace's English synchronously, at
// module top level — so importing this file (as the staff lazy route chunk
// does, in `app/lazy-routes/staff-routes.tsx`) is itself what loads it. This
// keeps the namespace's English OUT of the eager main-chunk bundle
// (`lib/i18n/index.ts` only globs direct children of `locales/en/`, never
// `locales/en/staff/`) while still making it available, in English, with no
// flash of raw `ns:key` text, by the time any staff page that calls
// `useTranslation('document-authoring')` actually renders — static JS module
// evaluation order guarantees this import runs to completion before the
// barrel module that imports it resolves, i.e. before React.lazy's promise
// settles and a page component can mount. See
// `features/educator/staff-locales.ts` for the original worked example this
// mirrors, and `docs/i18n/README.md`'s "Staff and admin namespaces" section.
//
// NOTE: `authored-version-snapshot.tsx` (and anything else document-authoring
// renders on a PARENT route — the shared-draft review page, the parent
// authored-version viewer) does NOT use this namespace: those chrome strings
// live in the EAGER `document-authoring-shared` namespace instead
// (`locales/{en,es}/document-authoring-shared.json`), precisely so a parent
// route never depends on this staff-only chunk.
//
// A unit test that renders a `document-authoring`-namespace component
// directly (bypassing the lazy route) must import this file first, the same
// way the real route chunk does — see this feature's test files' top import.
registerEnglishNamespace('document-authoring', enDocumentAuthoring);
