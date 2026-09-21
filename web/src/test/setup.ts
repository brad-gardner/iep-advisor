import '@testing-library/jest-dom';
import { vi } from 'vitest';
import { createElement, type ChangeEvent } from 'react';
import type { RichTextEditorProps } from '@/components/ui/rich-text-editor';

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
