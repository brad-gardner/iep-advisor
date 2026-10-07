import { registerEnglishNamespace } from '@/lib/i18n';
import enMeetingBrief from '@/locales/en/staff/meeting-brief.json';

// Registers the `meeting-brief` namespace's English synchronously, at module
// top level — mirrors `features/educator/staff-locales.ts` (the worked
// example in `docs/i18n/README.md`'s "Staff and admin namespaces"). Imported
// for its side effect at the top of `app/lazy-routes/staff-routes.tsx`
// (the barrel the `MeetingBriefPage` route chunk resolves through), so this
// runs to completion before that page can render. A unit test that renders
// `MeetingBriefPage` directly (bypassing the lazy route) must import this
// file first — see `staff-locales.test.ts` and `meeting-brief-page.test.tsx`.
registerEnglishNamespace('meeting-brief', enMeetingBrief);
