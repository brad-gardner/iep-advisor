import '@testing-library/jest-dom';
import { vi } from 'vitest';
import { createElement, type ChangeEvent } from 'react';
import type { RichTextEditorProps } from '@/components/ui/rich-text-editor';
import i18n, { i18nReady, featureNamespaces } from '@/lib/i18n';

// Initialize i18next synchronously (from this suite's point of view) with
// English before any test file renders a component that calls `t()` — the
// ~600 existing English text queries (getByRole name, getByText,
// getByLabelText) keep working unchanged. Forced explicitly rather than
// relying on jsdom's default `navigator.languages` so a test run is never
// at the mercy of environment detection. Spanish-specific tests opt in via
// `renderInSpanish` (`src/test/i18n-test-utils.tsx`).
await i18nReady;
await i18n.changeLanguage('en');
// Every namespace's ENGLISH data is bundled eagerly now (`lib/i18n/index.ts`
// — every `en/*.json` file, not just the two shell namespaces), so this
// resolves immediately with nothing to fetch; it's a confirmation, not a
// real load. Kept anyway so `hasLoadedNamespace` (the missing-key guard
// below) reports every namespace as loaded before the first test runs, the
// same as if a component had already rendered it once — belt-and-suspenders
// against any i18next internal distinction between "preseeded via
// `resources`" and "loaded".
await i18n.loadNamespaces(featureNamespaces);

// Fail the test immediately on a genuinely missing translation key, instead
// of relying on each Spanish-rendering test to separately regex-check the
// DOM for a leaked raw `ns:key` string. `saveMissing` must be on for
// i18next to invoke the handler at all; neither is set in the production
// init (`lib/i18n/index.ts`) — this is test-only.
//
// A key resolved through an explicit, MEANINGFUL `defaultValue` (e.g.
// `orgRoleLabel`'s fallback for an unrecognized role name, `{ defaultValue:
// name }`) is NOT a bug, and is withheld from the throw. But `<Trans
// i18nKey="some.key" />` with no `children`/`defaults`/`tOptions.defaultValue`
// of its own ALSO ends up with a `defaultValue` in `opt` — react-i18next
// falls back to the key itself (`opt.defaultValue === key`) when it has
// nothing better, which is indistinguishable from "no default" and must
// still throw on a genuinely missing key. Only a defaultValue that differs
// from the key is a real, intentional fallback.
i18n.options.saveMissing = true;
i18n.options.missingKeyHandler = (lngs, ns, key, _fallbackValue, _updateMissing, opt) => {
  if (opt && typeof opt === 'object' && 'defaultValue' in opt && opt.defaultValue !== key) return;
  const primaryLng = Array.isArray(lngs) ? lngs[0] : lngs;
  // English is always already loaded (every `en/*.json` file is bundled
  // eagerly — see `lib/i18n/index.ts`), so this guard only ever matters for
  // a Spanish-rendering test: a namespace's SPANISH data loads lazily, on
  // demand, via `esLoaders`, and the very first render of a component that
  // calls `useTranslation('<that namespace>')` happens before that async
  // load resolves (no Suspense boundary — `react: { useSuspense: false }`),
  // so `t()` genuinely has nothing loaded to check yet; react-i18next
  // re-renders once it resolves. That's a transient "not loaded yet", not a
  // missing key, and must not fail the test — only a key still missing
  // AFTER its namespace has loaded is a real bug. `renderInSpanish`
  // (`test/i18n-test-utils.tsx`) preloads every namespace in Spanish before
  // render; this is the backstop for anything that still races it.
  if (!i18n.hasLoadedNamespace(ns, { lng: primaryLng })) return;
  const languages = Array.isArray(lngs) ? lngs.join(', ') : lngs;
  throw new Error(`[i18n] missing translation for "${ns}:${key}" (${languages})`);
};

// jsdom does not implement scrolling APIs used by overlay scroll-lock. Stub
// them so tests exercising Modal/Drawer open/close don't emit "Not implemented"
// noise.
if (!('scrollTo' in window) || typeof window.scrollTo !== 'function') {
  window.scrollTo = vi.fn();
} else {
  vi.spyOn(window, 'scrollTo').mockImplementation(() => {});
}

// Same gap on the element side: jsdom leaves `Element.prototype.scrollIntoView`
// undefined, so a pin-to-bottom that scrolls a sentinel into view (MessageList)
// would throw rather than no-op. Stub it as a no-op — jsdom has no layout, so
// there is no scroll position for a test to assert against either way.
if (typeof Element.prototype.scrollIntoView !== 'function') {
  Element.prototype.scrollIntoView = vi.fn();
}

// Same gap for `Element.prototype.scrollTo` (as opposed to `window.scrollTo`
// above) — a scrollable pane that pins itself to the bottom as content grows
// (e.g. the document assistant's chat thread) calls it on its own container
// element, which jsdom leaves undefined.
if (typeof Element.prototype.scrollTo !== 'function') {
  Element.prototype.scrollTo = vi.fn();
}

// jsdom implements Element.getClientRects/getBoundingClientRect but not
// Range's — ProseMirror's `coordsAtPos` (used by the real RichTextEditor's
// `.focus()` command, which scrolls the new selection into view) measures a
// Range when the target position falls inside a text node. Without this,
// jsdom throws "target.getClientRects is not a function" from an async
// requestAnimationFrame callback, surfacing as test-run noise (occasionally
// after the test that triggered it has already finished).
if (typeof Range !== 'undefined' && typeof Range.prototype.getClientRects !== 'function') {
  Range.prototype.getClientRects = () => ({
    length: 0,
    item: () => null,
    [Symbol.iterator]: function* () {},
  }) as unknown as DOMRectList;
  Range.prototype.getBoundingClientRect = () =>
    ({ x: 0, y: 0, top: 0, left: 0, right: 0, bottom: 0, width: 0, height: 0, toJSON: () => ({}) }) as DOMRect;
}

// Global stand-in for the TipTap-backed RichTextEditor. jsdom doesn't run a
// real contenteditable/ProseMirror well enough for the hundreds of existing
// feature tests that drive these fields with
// `fireEvent.change(getByTestId(...), { target: { value } })`, so every test
// gets a plain, fully-controlled `<textarea>` that preserves the props those
// tests rely on (id/label/data-testid/placeholder/maxLength/disabled/
// aria-label/required) and calls `onChange(value)` the same way the real
// editor does. `rich-text-editor.test.tsx` calls `vi.unmock(...)` to exercise
// the real TipTap editor directly.
vi.mock('@/components/ui/rich-text-editor', async () => {
  const actual = await vi.importActual<typeof import('@/components/ui/rich-text-editor')>(
    '@/components/ui/rich-text-editor'
  );

  function RichTextEditor({
    id,
    label,
    value,
    onChange,
    placeholder,
    disabled,
    maxLength,
    required,
    className,
    onFocus,
    onBlur,
    'data-testid': dataTestId,
    'aria-label': ariaLabel,
  }: RichTextEditorProps) {
    const textareaId = id || label?.toLowerCase().replace(/\s+/g, '-');
    return createElement(
      'div',
      { className },
      label ? createElement('label', { htmlFor: textareaId }, label) : null,
      createElement('textarea', {
        id: textareaId,
        value,
        placeholder,
        disabled,
        maxLength,
        required,
        'aria-label': ariaLabel,
        'data-testid': dataTestId,
        onFocus,
        onBlur,
        onChange: (e: ChangeEvent<HTMLTextAreaElement>) => onChange(e.target.value),
      })
    );
  }

  // The idle warm-up would pull the real TipTap chunk into every routed test; it is a no-op here.
  return { ...actual, RichTextEditor, warmRichTextEditor: () => {} };
});
