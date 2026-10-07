import 'i18next';
import type { resources } from './index';

// Derives STRICT typed keys from the English resources *exported by
// `index.ts`* (the single place the two SHELL namespaces are registered —
// see its `resources` object): an unknown or misspelled `t('common:…')`/
// `t('auth:…')` key becomes a `tsc` error, and adding a bundled namespace
// there (one import + one entry) is all it takes to type it here too.
//
// Every other, feature-level namespace (`children`, `home`, …) loads
// lazily in BOTH languages (see `index.ts`'s `localeLoaders`) and is
// deliberately NOT part of `resources` above, so it can't be derived the
// same way. It still needs an entry here — react-i18next's generated
// `useTranslation`/`t` overloads only accept a namespace name that
// `CustomTypeOptions.resources` knows about at all, strictly-typed or
// not — so each one is listed in `LazyNamespaces` below with a loose
// `Record<string, string>` shape: `useTranslation('children')` and
// `t('children:anyKey')` compile, but an individual KEY typo in a lazy
// namespace is caught at runtime by the key-parity test
// (`locale-parity.test.ts`), not by `tsc`. Add one line per new
// feature-level namespace as a phase converts it.
interface LazyNamespaces {
  children: Record<string, string>;
  home: Record<string, string>;
  onboarding: Record<string, string>;
  notifications: Record<string, string>;
  subscription: Record<string, string>;
  'child-links': Record<string, string>;
  sharing: Record<string, string>;
  'knowledge-base': Record<string, string>;
}

declare module 'i18next' {
  interface CustomTypeOptions {
    defaultNS: 'common';
    returnNull: false;
    resources: (typeof resources)['en'] & LazyNamespaces;
  }
}
