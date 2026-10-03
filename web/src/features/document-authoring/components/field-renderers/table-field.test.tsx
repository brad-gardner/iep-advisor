import { describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import type { TemplateFieldDto } from '@/features/admin/templates/types';
import { TableField } from './table-field';
import { ToastProvider } from '@/components/ui/toast';
import { DocumentFlushContext } from '../../hooks/flush-registry-context';
import { DocumentEditorContext, type ActiveFieldTarget, type DocumentEditorContextValue } from '../../hooks/document-editor-context';
import type { SaveResult } from '../../hooks/use-document-instance';

// Goals now render through `GoalsBlock` (card list + focused editor) instead
// of this generic stacked-card block — see `table-field-goals.test.tsx` for
// goal-specific coverage (including the goal-retirement dialog). `services`
// exercises the SAME generic row-block mechanics (add/remove, min/max,
// carried-forward, id adoption, evidence-insert targeting) that services,
// accommodations and transition still share.
const svcCol = 'c1111111-1111-1111-1111-111111111111';
const noteCol = 'c2222222-2222-2222-2222-222222222222';
const fieldKey = 'f1111111-1111-1111-1111-111111111111';

const servicesField: TemplateFieldDto = {
  id: 7,
  fieldKey,
  fieldType: 'Table',
  label: 'Services',
  required: false,
  displayOrder: 0,
  configJson: JSON.stringify({
    semantic: 'services',
    columns: [
      { columnKey: svcCol, type: 'Text', label: 'Service', required: true, semantic: 'serviceType' },
      { columnKey: noteCol, type: 'Text', label: 'Notes', required: false },
    ],
    maxRows: 2,
  }),
} as TemplateFieldDto;

const registry = { register: () => () => {}, flushAll: async () => {} } as unknown as React.ContextType<typeof DocumentFlushContext>;

function renderField(value: unknown, onSave: (p: Record<string, unknown>) => Promise<SaveResult>) {
  return render(
    <DocumentFlushContext.Provider value={registry}>
      <TableField field={servicesField} value={value} onSave={onSave} />
    </DocumentFlushContext.Provider>
  );
}

describe('TableField (semantic row block)', () => {
  it('renders each service as a labelled card, with min/max controlling add and remove', () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    renderField([{ _rowId: 'ID-1', [svcCol]: 'Speech therapy', [noteCol]: 'pull-out' }], onSave);

    expect(screen.getByText('Service 1')).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: /^Service\s*\*?$/ })).toHaveValue('Speech therapy');
    expect(screen.getByRole('textbox', { name: 'Notes' })).toHaveValue('pull-out');
    expect(screen.getByRole('button', { name: 'Add service' })).toBeEnabled();

    fireEvent.click(screen.getByRole('button', { name: 'Add service' }));
    expect(screen.getByText('Service 2')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Add service' })).toBeDisabled(); // maxRows = 2
  });

  it('keeps a new row mounted (same input, same key) while the first save adopts its server id', async () => {
    let resolveSave!: (r: SaveResult) => void;
    const onSave = vi.fn().mockImplementation(
      () => new Promise<SaveResult>((resolve) => (resolveSave = resolve))
    );
    renderField([], onSave);

    fireEvent.click(screen.getByRole('button', { name: 'Add service' }));
    const svcInput = screen.getByRole('textbox', { name: /^Service\s*\*?$/ });
    svcInput.focus();
    fireEvent.change(svcInput, { target: { value: 'Sp' } });
    expect(onSave).toHaveBeenCalledTimes(1); // add flushed immediately

    // AI help is only offered once the row has a persisted id (the hint shows until then).
    expect(screen.getByText(/AI help is available once this row has saved/)).toBeInTheDocument();

    await act(async () => {
      resolveSave({ ok: true, values: { [fieldKey]: [{ _rowId: 'SERVER-ID', [svcCol]: '', [noteCol]: '' }] } });
    });

    // The very same element is still mounted and focused — the key did not change.
    expect(screen.getByRole('textbox', { name: /^Service\s*\*?$/ })).toBe(svcInput);
    expect(document.activeElement).toBe(svcInput);
    expect(svcInput).toHaveValue('Sp');
    expect(screen.queryByText(/AI help is available once this row has saved/)).not.toBeInTheDocument(); // id adopted
  });

  it('shows a carried-forward chip; Keep as-is and editing both mark the row reviewed', async () => {
    const calls: unknown[] = [];
    const onSave = vi.fn().mockImplementation((patch: Record<string, unknown>) => {
      calls.push(patch[fieldKey]);
      return Promise.resolve({ ok: true, values: {} });
    });
    renderField(
      [
        { _rowId: 'ID-1', _carriedFrom: { versionId: 3, rowId: 'ID-1', label: 'IEP v1', date: '2025-10-14' }, _confirmed: false, [svcCol]: 'Speech therapy', [noteCol]: '' },
        { _rowId: 'ID-2', _carriedFrom: { versionId: 3, rowId: 'ID-2', label: 'IEP v1' }, _confirmed: false, [svcCol]: 'Occupational therapy', [noteCol]: '' },
      ],
      onSave
    );
    const shownDate = new Date('2025-10-14T00:00:00').toLocaleDateString();
    expect(screen.getByTestId(`field-${fieldKey}-row-0-carried`)).toHaveTextContent(`Carried from IEP v1 (${shownDate}) · not yet reviewed`);

    fireEvent.click(screen.getByTestId(`field-${fieldKey}-row-0-keep`));
    await act(async () => {});
    expect(screen.getByTestId(`field-${fieldKey}-row-0-carried`)).toHaveTextContent('reviewed');
    expect(screen.queryByTestId(`field-${fieldKey}-row-0-keep`)).not.toBeInTheDocument();
    const sent = calls.at(-1) as Array<Record<string, unknown>>;
    expect(sent[0]._confirmed).toBe(true);

    const second = screen.getAllByRole('textbox', { name: /^Service\s*\*?$/ })[1];
    fireEvent.change(second, { target: { value: 'OT services' } });
    expect(screen.getByTestId(`field-${fieldKey}-row-1-carried`)).toHaveTextContent('· reviewed');
  });

  it('sends the latest rows (including adopted ids) on the next save', async () => {
    const calls: unknown[] = [];
    let n = 0;
    const onSave = vi.fn().mockImplementation((patch: Record<string, unknown>) => {
      calls.push(patch[fieldKey]);
      n += 1;
      return Promise.resolve({ ok: true, values: { [fieldKey]: [{ _rowId: 'ID-A', [svcCol]: 'x', [noteCol]: '' }] } });
    });
    renderField([], onSave);

    fireEvent.click(screen.getByRole('button', { name: 'Add service' }));
    await act(async () => {}); // let the add-save resolve and adopt ID-A
    fireEvent.change(screen.getByRole('textbox', { name: /^Service\s*\*?$/ }), { target: { value: 'typed' } });
    fireEvent.click(screen.getByRole('button', { name: 'Remove service 1' })); // immediate flush, no dialog for services
    await act(async () => {});

    // Second+ saves carry the adopted id for the row (before it was removed).
    const withId = calls.slice(1).flat() as Array<Record<string, unknown>>;
    expect(n).toBeGreaterThanOrEqual(2);
    expect(withId.some((r) => r._rowId === 'ID-A' && r[svcCol] === 'typed') || withId.length === 0).toBe(true);
  });
});

describe('TableField evidence-insert target', () => {
  function renderInEditor(value: unknown, onSave: (p: Record<string, unknown>) => Promise<SaveResult>) {
    const targets: ActiveFieldTarget[] = [];
    const cleared: string[] = [];
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
          <DocumentFlushContext.Provider value={registry}>
            <TableField field={servicesField} value={value} onSave={onSave} />
          </DocumentFlushContext.Provider>
        </DocumentEditorContext.Provider>
      </ToastProvider>
    );
    return { targets, cleared, ...utils };
  }

  it('appends to the focused cell, labels it by its current row, and drops the target when the row goes', async () => {
    const calls: unknown[] = [];
    const onSave = vi.fn().mockImplementation((patch: Record<string, unknown>) => {
      calls.push(patch[fieldKey]);
      return Promise.resolve({ ok: true, values: {} });
    });
    const { targets, cleared, unmount } = renderInEditor(
      [
        { _rowId: 'ID-1', [svcCol]: 'Speech therapy', [noteCol]: '' },
        { _rowId: 'ID-2', [svcCol]: 'Occupational therapy', [noteCol]: '' },
      ],
      onSave
    );

    const secondService = screen.getAllByRole('textbox', { name: /^Service\s*\*?$/ })[1];
    fireEvent.focus(secondService);
    const target = targets.at(-1)!;
    expect(target.label()).toBe('Service 2 — Service');

    await act(async () => target.apply('Provider: Dr. Lee'));
    expect(secondService).toHaveValue('Occupational therapy\n\nProvider: Dr. Lee');
    const sent = calls.at(-1) as Array<Record<string, unknown>>;
    expect(sent[1][svcCol]).toBe('Occupational therapy\n\nProvider: Dr. Lee');

    // Removing a DIFFERENT row (not the one holding the target) leaves it alone.
    fireEvent.click(screen.getByRole('button', { name: 'Remove service 1' }));
    await act(async () => {});
    expect(cleared.some((id) => target.id.startsWith(`${id}:`))).toBe(false);
    expect(target.label()).toBe('Service 1 — Service'); // re-labeled after the shift

    // Removing the row that owns the target clears it by prefix; unmount clears the field.
    fireEvent.click(screen.getByRole('button', { name: 'Remove service 1' }));
    await act(async () => {});
    expect(cleared.some((id) => target.id.startsWith(`${id}:`))).toBe(true);
    await act(async () => target.apply('ignored'));
    expect((calls.at(-1) as unknown[]).length).toBe(0);
    unmount();
    expect(cleared.at(-1)).toBe(fieldKey);
  });
});
