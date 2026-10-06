import 'i18next';
import type enCommon from '@/locales/en/common.json';
import type enAuth from '@/locales/en/auth.json';

// Derives typed keys from the English resources: an unknown or misspelled
// `t()` key becomes a `tsc` error. Spanish is not part of this — it's
// validated at runtime by the key-parity test (`locale-parity.test.ts`)
// instead, since it's loaded lazily and isn't statically imported anywhere.
declare module 'i18next' {
  interface CustomTypeOptions {
    defaultNS: 'common';
    returnNull: false;
    resources: {
      common: typeof enCommon;
      auth: typeof enAuth;
    };
  }
}
