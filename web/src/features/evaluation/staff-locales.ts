import { registerEnglishNamespace } from '@/lib/i18n';
import enEvaluation from '@/locales/en/staff/evaluation.json';

// Registers the `evaluation` namespace's English synchronously, at module
// top level — importing this file (as the staff lazy route chunk does, in
// `app/lazy-routes/staff-routes.tsx`, since the "Evaluation" card only ever
// renders from the educator student detail page) is itself what loads it.
// This keeps the namespace's English OUT of the eager main-chunk bundle
// (`lib/i18n/index.ts` only globs direct children of `locales/en/`, never
// `locales/en/staff/`) while still making it available, in English, with no
// flash of raw `ns:key` text, by the time any staff page that calls
// `useTranslation('evaluation')` actually renders — see
// `features/educator/staff-locales.ts` for the full reasoning (the worked
// example this follows).
//
// A unit test that renders an `evaluation`-namespace component directly
// (bypassing the lazy route) must import this file first, the same way the
// real route chunk does — see `staff-locales.test.ts` and
// `evaluation-case-label.test.ts`.
registerEnglishNamespace('evaluation', enEvaluation);
