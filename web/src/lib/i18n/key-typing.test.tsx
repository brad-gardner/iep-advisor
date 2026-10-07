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
 * `.tsx`, not `.ts` (phase 2): the `Trans` canary below needs real JSX —
 * `React.createElement(Trans, { i18nKey: '...', ns: 'common' })` does NOT
 * get the same per-element generic inference JSX syntax does, so with a
 * LAZY, loosely-typed feature namespace now in `CustomTypeOptions`
 * (`LazyNamespaces` in `types.d.ts` — `children`, as of phase 2;
 * `Record<string, string>` accepts any key), `createElement`'s inference
 * fell back to the widest namespace it could satisfy and the canary went
 * vacuously green even with an explicit `ns` prop pinned. Actual JSX
 * resolves `Ns` from the `ns` attribute correctly.
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
  // stays pinned to a namespace that's still STRICTLY typed — `common` —
  // rather than resolving across every namespace including a loose,
  // lazy-feature one (see the file doc comment above).
  // @ts-expect-error -- unknown i18nKey in the strictly-typed 'common' namespace must not type-check
  <Trans i18nKey="__missing__" ns="common" />;

  function useChildrenCanary() {
    const { t } = useTranslation('children');
    // A lazy, loosely-typed feature namespace accepts ANY string key at
    // compile time by design (see `types.d.ts`'s `LazyNamespaces`) — this is
    // NOT wrapped in `@ts-expect-error`; it documents that this line is
    // SUPPOSED to compile, so a future tightening of `LazyNamespaces` that
    // breaks it is caught by a normal `tsc` error here, not silently.
    return t('this key does not exist either, and that is fine for now');
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
