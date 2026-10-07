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
import type EnAuth from '@/locales/en/auth.json';
import type EnChildLinks from '@/locales/en/child-links.json';
import type EnChildren from '@/locales/en/children.json';
import type EnCommon from '@/locales/en/common.json';
import type EnHome from '@/locales/en/home.json';
import type EnKnowledgeBase from '@/locales/en/knowledge-base.json';
import type EnNotifications from '@/locales/en/notifications.json';
import type EnOnboarding from '@/locales/en/onboarding.json';
import type EnSharing from '@/locales/en/sharing.json';
import type EnSubscription from '@/locales/en/subscription.json';

export interface EnResources {
  auth: typeof EnAuth;
  'child-links': typeof EnChildLinks;
  children: typeof EnChildren;
  common: typeof EnCommon;
  home: typeof EnHome;
  'knowledge-base': typeof EnKnowledgeBase;
  notifications: typeof EnNotifications;
  onboarding: typeof EnOnboarding;
  sharing: typeof EnSharing;
  subscription: typeof EnSubscription;
}

declare module 'i18next' {
  interface CustomTypeOptions {
    defaultNS: 'common';
    returnNull: false;
    resources: EnResources;
  }
}
