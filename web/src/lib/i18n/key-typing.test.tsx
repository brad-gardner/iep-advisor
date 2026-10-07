import { describe, expect, it } from 'vitest';
import { Trans, useTranslation } from 'react-i18next';
import i18n from './index';

/**
 * Compile-time canary for the generated key typing (`types.d.ts`'s
 * `EnResources`, built from one `import type` per `en/*.json` namespace
 * file). The function below is never called — it exists purely so `tsc`
 * type-checks its body. Each `// @ts-expect-error` line names a key that
 * doesn't exist in any namespace, so it must fail its own type check; if the
 * typing machinery ever silently degrades (e.g. `EnResources`' shape
 * loosens, or a future i18next/react-i18next upgrade changes how
 * `CustomTypeOptions` is consumed) and these keys stop being type errors,
 * the `@ts-expect-error` directives themselves become "unused" — a `tsc`
 * error — and `npm run test:types` fails. That's the signal this file exists
 * to catch: keys silently becoming `any` (and therefore un-typo-checkable)
 * without any other test noticing.
 *
 * Every namespace is strictly typed now (phase 2 review: all `en/*.json`
 * files are bundled eagerly — see `index.ts` — so there's no more "lazy,
 * loosely-typed" namespace that accepts any string key). `.tsx`, not `.ts`,
 * because the `Trans` canary needs REAL JSX — `React.createElement(Trans, {
 * i18nKey: '...', ns: 'common' })` does NOT get the same per-element generic
 * inference JSX syntax does, so a bare-`createElement` version of this
 * check could go vacuously green under some future typing regression where
 * JSX-only inference would still catch it. Actual JSX resolves `Ns` from the
 * `ns` attribute correctly.
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

  // `ns` is explicit (rather than relying on the default namespace) so this
  // stays pinned to a specific namespace — `common` — rather than resolving
  // across every namespace.
  // @ts-expect-error -- unknown i18nKey in the 'common' namespace must not type-check
  <Trans i18nKey="__missing__" ns="common" />;

  function useChildrenCanary() {
    const { t } = useTranslation('children');
    // @ts-expect-error -- unknown key in the 'children' namespace must not type-check
    return t('this key does not exist either, and that is still caught now that every namespace is strictly typed');
  }
  void useChildrenCanary;
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
