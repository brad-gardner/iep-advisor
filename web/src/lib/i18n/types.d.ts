import 'i18next';

// Type-only companions to `index.ts`'s runtime `import.meta.glob(..., {
// eager: true })` discovery of every `en/*.json` file. A dynamic glob gives
// Vite's types a generic `Record<string, Module>` shape — every match typed
// the same way, with no literal per-file-path key and no per-file literal
// JSON shape (see `node_modules/vite/types/importGlob.d.ts`'s
// `ImportGlobFunction`) — so the glob's VALUE can't, by itself, produce the
// STRICT, per-namespace key typing below; these `import type` lines are the
// (erased-at-runtime) substitute. `EnResources` must name exactly the same
// namespaces as `index.ts`'s `resources.en` (equivalently, its
// `featureNamespaces` export) — `index.test.ts` and `locale-parity.test.ts`
// cover the files on disk; this is the one place that still needs a line
// added per new namespace. Forgetting one isn't silent: the first
// `useTranslation('<that namespace>')` call for it fails to compile (the
// namespace is simply unknown to `CustomTypeOptions`, not loosely typed).
//
// Because every namespace is listed here now (not just the two shell ones),
// an unknown or misspelled key is a `tsc` error for EVERY namespace, not
// only `common`/`auth` — see `key-typing.test.tsx`'s canary.
import type EnAdvocacyGoals from '@/locales/en/advocacy-goals.json';
import type EnAdvocate from '@/locales/en/advocate.json';
import type EnAnalysis from '@/locales/en/analysis.json';
import type EnAuth from '@/locales/en/auth.json';
import type EnChildLinks from '@/locales/en/child-links.json';
import type EnChildren from '@/locales/en/children.json';
import type EnCommon from '@/locales/en/common.json';
import type EnDraftSharing from '@/locales/en/draft-sharing.json';
import type EnEtrDocuments from '@/locales/en/etr-documents.json';
import type EnGoals from '@/locales/en/goals.json';
import type EnHome from '@/locales/en/home.json';
import type EnIepComparison from '@/locales/en/iep-comparison.json';
import type EnIepDocuments from '@/locales/en/iep-documents.json';
import type EnIepVersions from '@/locales/en/iep-versions.json';
import type EnJournal from '@/locales/en/journal.json';
import type EnKnowledgeBase from '@/locales/en/knowledge-base.json';
import type EnMeetingPrep from '@/locales/en/meeting-prep.json';
import type EnMeetings from '@/locales/en/meetings.json';
import type EnNotifications from '@/locales/en/notifications.json';
import type EnOnboarding from '@/locales/en/onboarding.json';
import type EnProgressReports from '@/locales/en/progress-reports.json';
import type EnSharedDrafts from '@/locales/en/shared-drafts.json';
import type EnSharing from '@/locales/en/sharing.json';
import type EnStudent from '@/locales/en/student.json';
import type EnSubscription from '@/locales/en/subscription.json';

export interface EnResources {
  'advocacy-goals': typeof EnAdvocacyGoals;
  advocate: typeof EnAdvocate;
  analysis: typeof EnAnalysis;
  auth: typeof EnAuth;
  'child-links': typeof EnChildLinks;
  children: typeof EnChildren;
  common: typeof EnCommon;
  'draft-sharing': typeof EnDraftSharing;
  'etr-documents': typeof EnEtrDocuments;
  goals: typeof EnGoals;
  home: typeof EnHome;
  'iep-comparison': typeof EnIepComparison;
  'iep-documents': typeof EnIepDocuments;
  'iep-versions': typeof EnIepVersions;
  journal: typeof EnJournal;
  'knowledge-base': typeof EnKnowledgeBase;
  'meeting-prep': typeof EnMeetingPrep;
  meetings: typeof EnMeetings;
  notifications: typeof EnNotifications;
  onboarding: typeof EnOnboarding;
  'progress-reports': typeof EnProgressReports;
  'shared-drafts': typeof EnSharedDrafts;
  sharing: typeof EnSharing;
  student: typeof EnStudent;
  subscription: typeof EnSubscription;
}

declare module 'i18next' {
  interface CustomTypeOptions {
    defaultNS: 'common';
    returnNull: false;
    resources: EnResources;
  }
}
