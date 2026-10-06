import 'i18next';
import type { resources } from './index';

// Derives typed keys from the English resources *exported by `index.ts`*
// (the single place namespaces are registered — see its `resources`
// object): an unknown or misspelled `t()` key becomes a `tsc` error, and
// adding a namespace there (one import + one entry) is all it takes to type
// it here too. Spanish is not part of this — it's validated at runtime by
// the key-parity test (`locale-parity.test.ts`) instead, since it's loaded
// lazily and isn't statically imported anywhere.
declare module 'i18next' {
  interface CustomTypeOptions {
    defaultNS: 'common';
    returnNull: false;
    resources: (typeof resources)['en'];
  }
}
