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
        onChange: (e: ChangeEvent<HTMLTextAreaElement>) => onChange(e.target.value),
      })
    );
  }

  return { ...actual, RichTextEditor };
});
