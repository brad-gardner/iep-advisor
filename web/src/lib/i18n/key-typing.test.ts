import { createElement } from 'react';
import { describe, expect, it } from 'vitest';
import { Trans, useTranslation } from 'react-i18next';
import i18n from './index';

/**
 * Compile-time canary for the generated key typing (`types.d.ts` deriving
 * `CustomTypeOptions` from `index.ts`'s `resources`). The function below is
 * never called — it exists purely so `tsc` type-checks its body. Each
 * `// @ts-expect-error` line names a key that doesn't exist in any
 * namespace, so it must fail its own type check; if the typing machinery
 * ever silently degrades (e.g. `resources`' shape loosens, or a future
 * i18next/react-i18next upgrade changes how `CustomTypeOptions` is
 * consumed) and these keys stop being type errors, the `@ts-expect-error`
 * directives themselves become "unused" — a `tsc` error — and `npm run
 * test:types` fails. That's the signal this file exists to catch: keys
 * silently becoming `any` (and therefore un-typo-checkable) without any
 * other test noticing.
 *
 * JSX syntax needs a `.tsx` file, so `<Trans i18nKey="..." />` is written
 * as the equivalent `createElement(Trans, { i18nKey: '...' })` here to stay
 * in `.ts` (this file also runs as an ordinary Vitest file — see the inert
 * `describe`/`it` below — so it needs to load like any other test module).
 */
export function _keyTypingCanary_doNotCall(): void {
  // @ts-expect-error -- unknown key in the default ('common') namespace must not type-check
  i18n.t('common:__missing__');

  function useAuthCanary() {
    const { t } = useTranslation('auth');
    // @ts-expect-error -- unknown key in the 'auth' namespace must not type-check
    return t('__missing__');
  }
  void useAuthCanary;

  // @ts-expect-error -- unknown i18nKey must not type-check
  createElement(Trans, { i18nKey: '__missing__' });
}

describe('i18n key typing canary', () => {
  it('has nothing to assert at runtime — see the @ts-expect-error lines above', () => {
    // `_keyTypingCanary_doNotCall` is intentionally never invoked: calling
    // it would actually look up `__missing__` and trip the test suite's
    // throw-on-missing-key handler (`test/setup.ts`). The real assertion
    // here is `tsc` itself, via `npm run test:types`.
    expect(typeof _keyTypingCanary_doNotCall).toBe('function');
  });
});
