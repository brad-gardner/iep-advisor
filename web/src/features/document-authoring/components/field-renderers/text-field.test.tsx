import { describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import type { TemplateFieldDto } from '@/features/admin/templates/types';
import { TextField } from './text-field';
import { RichTextField } from './rich-text-field';
import { ToastProvider } from '@/components/ui/toast';
import { DocumentFlushContext } from '../../hooks/flush-registry-context';
import { DocumentEditorContext, type ActiveFieldTarget, type DocumentEditorContextValue } from '../../hooks/document-editor-context';

const registry = { register: () => () => {}, flushAll: async () => {} } as unknown as React.ContextType<typeof DocumentFlushContext>;

function field(type: 'Text' | 'RichText', configJson: string): TemplateFieldDto {
  return { id: 3, fieldKey: 'f3333333-3333-3333-3333-333333333333', fieldType: type, label: 'Present Levels', required: false, displayOrder: 0, configJson } as TemplateFieldDto;
}

function harness(ui: (editor: DocumentEditorContextValue, onSave: ReturnType<typeof vi.fn>) => React.ReactElement, disabled = false) {
  const targets: ActiveFieldTarget[] = [];
  const cleared: string[] = [];
  const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
  const editor = {
    instanceId: 1,
    studentId: 2,
    shareableEntries: { entries: null, load: async () => {} },
    setActiveField: (t: ActiveFieldTarget) => targets.push(t),
    clearActiveField: (id: string) => cleared.push(id),
  } as unknown as DocumentEditorContextValue;
  const utils = render(
    <ToastProvider>
      <DocumentEditorContext.Provider value={editor}>
        <DocumentFlushContext.Provider value={registry}>{ui(editor, onSave)}</DocumentFlushContext.Provider>
      </DocumentEditorContext.Provider>
    </ToastProvider>
  );
  void disabled;
  return { targets, cleared, onSave, ...utils };
}

describe('evidence insert into text renderers', () => {
  it('RichText: focus registers a target that appends after existing prose and saves at once', async () => {
    const f = field('RichText', '{}');
    const { targets, cleared, onSave, unmount } = harness((_, onSave) => (
      <RichTextField field={f} value="Existing narrative." disabled={false} onSave={onSave} />
    ));
    const box = screen.getByRole('textbox', { name: 'Present Levels' });
    fireEvent.focus(box);
    expect(targets.at(-1)!.label()).toBe('Present Levels');

    await act(async () => targets.at(-1)!.apply('Reads 42 wpm (ETR 2024).'));
    expect(box).toHaveValue('Existing narrative.\n\nReads 42 wpm (ETR 2024).');
    expect(onSave).toHaveBeenCalledWith({ [f.fieldKey]: 'Existing narrative.\n\nReads 42 wpm (ETR 2024).' });

    unmount();
    expect(cleared).toContain(f.fieldKey);
  });

  it('Text: appends inline, clips to maxLength, and ignores inserts once disabled', async () => {
    const f = field('Text', JSON.stringify({ maxLength: 20 }));
    const { targets, onSave, rerender } = harness((_, onSave) => <TextField field={f} value="Jordan" disabled={false} onSave={onSave} />);
    const input = screen.getByRole('textbox', { name: 'Present Levels' });
    fireEvent.focus(input);
    await act(async () => targets.at(-1)!.apply('Ellis, grade seven, reading'));
    expect(input).toHaveValue('Jordan Ellis, grade '); // 20 chars
    expect(onSave).toHaveBeenLastCalledWith({ [f.fieldKey]: 'Jordan Ellis, grade ' });

    rerender(
      <ToastProvider>
        <DocumentEditorContext.Provider value={{ setActiveField: () => {}, clearActiveField: () => {} } as unknown as DocumentEditorContextValue}>
          <DocumentFlushContext.Provider value={registry}>
            <TextField field={f} value="Jordan" disabled onSave={onSave} />
          </DocumentFlushContext.Provider>
        </DocumentEditorContext.Provider>
      </ToastProvider>
    );
    const calls = onSave.mock.calls.length;
    await act(async () => targets.at(-1)!.apply('more'));
    expect(onSave.mock.calls.length).toBe(calls);
  });
});
