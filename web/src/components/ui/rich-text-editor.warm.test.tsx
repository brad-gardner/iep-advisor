import { describe, it, expect, vi } from 'vitest';
import { act, render, screen, waitFor } from '@testing-library/react';
import { RichTextEditor, isRichTextEditorWarm, warmRichTextEditor } from './rich-text-editor';

// Real editor (not the global textarea stand-in) — see rich-text-editor.test.tsx.
// Own file because the module-level warm-up cache is process state: once this
// suite warms it, later renders in the same module instance go direct.
vi.unmock('@/components/ui/rich-text-editor');

describe('RichTextEditor warm-up', () => {
  it('an instance that mounted through Suspense keeps its editor when the warm-up lands mid-edit', async () => {
    expect(isRichTextEditorWarm()).toBe(false);
    const { rerender } = render(<RichTextEditor label="Note" value="" onChange={() => {}} />);
    const before = await screen.findByLabelText('Note');

    // jsdom has no requestIdleCallback, so the warm-up falls back to a timer.
    vi.useFakeTimers();
    warmRichTextEditor();
    await act(async () => {
      await vi.advanceTimersByTimeAsync(2000);
    });
    vi.useRealTimers();
    await waitFor(() => expect(isRichTextEditorWarm()).toBe(true));

    // A keystroke-driven re-render must not swap element type under the field.
    rerender(<RichTextEditor label="Note" value="typed" onChange={() => {}} />);
    await waitFor(() => expect(screen.getByLabelText('Note')).toHaveTextContent('typed'));
    expect(screen.getByLabelText('Note')).toBe(before);
  });

  it('renders the editor directly, with no busy placeholder, once warm', async () => {
    expect(isRichTextEditorWarm()).toBe(true);
    render(<RichTextEditor label="Later" value="**b**" onChange={() => {}} />);
    expect(document.querySelector('[aria-busy]')).toBeNull();
    const region = await screen.findByLabelText('Later');
    expect(region.querySelector('strong')).not.toBeNull();
  });

  it('is idempotent — a second call does not schedule more work', () => {
    const spy = vi.spyOn(window, 'setTimeout');
    warmRichTextEditor();
    expect(spy).not.toHaveBeenCalled();
    spy.mockRestore();
  });
});
