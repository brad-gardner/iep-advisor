import { describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import type { TemplateFieldDto } from '@/features/admin/templates/types';
import { RichTextField } from './rich-text-field';
import { ToastProvider } from '@/components/ui/toast';
import { DocumentFlushContext } from '../../hooks/flush-registry-context';
import { DocumentEditorContext, type ActiveFieldTarget, type DocumentEditorContextValue } from '../../hooks/document-editor-context';

// Exercises RichTextField mounted against the REAL TipTap-backed editor
// (rich-text-field.tsx's only existing test, in text-field.test.tsx, runs
// against the global plain-`<textarea>` stand-in from src/test/setup.ts).
// The lazy impl (rich-text-editor.tsx -> rich-text-editor-impl.tsx) is
// awaited via `findByRole` before any interaction, per RichTextEditor's own
// test file.
vi.unmock('@/components/ui/rich-text-editor');

const FIELD_KEY = 'f3333333-3333-3333-3333-333333333333';

const registry = { register: () => () => {}, flushAll: async () => {} } as unknown as React.ContextType<
  typeof DocumentFlushContext
>;

function field(): TemplateFieldDto {
  return {
    id: 3,
    fieldKey: FIELD_KEY,
    fieldType: 'RichText',
    label: 'Present Levels',
    required: false,
    displayOrder: 0,
    configJson: '{}',
  } as TemplateFieldDto;
}

function harness(value: string, onSave: ReturnType<typeof vi.fn>, disabled = false) {
  const targets: ActiveFieldTarget[] = [];
  const editorCtx = {
    instanceId: 1,
    studentId: 2,
    shareableEntries: { entries: null, load: async () => {} },
    setActiveField: (t: ActiveFieldTarget) => targets.push(t),
    clearActiveField: () => {},
  } as unknown as DocumentEditorContextValue;
  const utils = render(
    <ToastProvider>
      <DocumentEditorContext.Provider value={editorCtx}>
        <DocumentFlushContext.Provider value={registry}>
          <RichTextField field={field()} value={value} disabled={disabled} onSave={onSave} />
        </DocumentFlushContext.Provider>
      </DocumentEditorContext.Provider>
    </ToastProvider>
  );
  return { targets, ...utils };
}

// Simulates typing by mutating the contenteditable's DOM directly and firing
// `input` — the same technique RichTextEditor's own real-editor typing tests
// rely on (a real key-by-key simulation isn't reliable against ProseMirror
// under jsdom). ProseMirror's DOMObserver reconciles the mutation via a real
// MutationObserver callback, which lands on a microtask a tick after the
// `input` event — independent of fake timers (they mock task-queue APIs like
// setTimeout, not native microtasks), so the extra `act` below is required
// even with real timers, and unaffected by `vi.useFakeTimers()`.
async function typeIntoEditor(box: HTMLElement, text: string) {
  const p = box.querySelector('p');
  if (!p) throw new Error('expected a paragraph to type into');
  act(() => {
    p.textContent = text;
    fireEvent.input(box);
  });
  await act(async () => {
    await Promise.resolve();
    await Promise.resolve();
  });
}

describe('RichTextField with the real editor', () => {
  it('typing triggers the debounced autosave with markdown', async () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    harness('', onSave);
    // The lazy-loaded editor's own dynamic import, and this query's retry
    // loop while it resolves, both need real timers — only switch to fake
    // ones once the editor has actually mounted.
    const box = await screen.findByRole('textbox', { name: 'Present Levels' });

    vi.useFakeTimers();
    try {
      await typeIntoEditor(box, 'Reads at grade level.');
      expect(onSave).not.toHaveBeenCalled();

      await act(async () => {
        vi.advanceTimersByTime(700);
      });

      expect(onSave).toHaveBeenCalledWith({ [FIELD_KEY]: 'Reads at grade level.' });
    } finally {
      vi.useRealTimers();
    }
  });

  it('blur flushes the pending save immediately, without waiting for the debounce', async () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    harness('', onSave);
    const box = await screen.findByRole('textbox', { name: 'Present Levels' });

    await typeIntoEditor(box, 'Reads at grade level.');
    expect(onSave).not.toHaveBeenCalled();

    await act(async () => {
      fireEvent.blur(box);
    });

    expect(onSave).toHaveBeenCalledWith({ [FIELD_KEY]: 'Reads at grade level.' });
  });

  it('an evidence-insert "apply" appends a new paragraph in the real doc, not just a longer string', async () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    const { targets } = harness('Existing narrative.', onSave);
    const box = await screen.findByRole('textbox', { name: 'Present Levels' });

    fireEvent.focus(box);
    expect(targets.at(-1)!.label()).toBe('Present Levels');

    await act(async () => {
      targets.at(-1)!.apply('Reads 42 wpm (ETR 2024).');
    });

    // appendText's default separator is a blank line — a genuine second
    // paragraph in the ProseMirror doc, not merely a longer text run.
    const paragraphs = box.querySelectorAll('p');
    expect(paragraphs).toHaveLength(2);
    expect(paragraphs[0]).toHaveTextContent('Existing narrative.');
    expect(paragraphs[1]).toHaveTextContent('Reads 42 wpm (ETR 2024).');

    expect(onSave).toHaveBeenCalledWith({
      [FIELD_KEY]: 'Existing narrative.\n\nReads 42 wpm (ETR 2024).',
    });
  });
});
