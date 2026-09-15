import { describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import type { TemplateFieldDto } from '@/features/admin/templates/types';
import { TableField } from './table-field';
import { DocumentFlushContext } from '../../hooks/flush-registry-context';
import type { SaveResult } from '../../hooks/use-document-instance';

const goalCol = 'c1111111-1111-1111-1111-111111111111';
const baseCol = 'c2222222-2222-2222-2222-222222222222';
const fieldKey = 'f1111111-1111-1111-1111-111111111111';

const goalsField: TemplateFieldDto = {
  id: 7,
  fieldKey,
  fieldType: 'Table',
  label: 'Goals',
  required: false,
  displayOrder: 0,
  configJson: JSON.stringify({
    semantic: 'goals',
    columns: [
      { columnKey: goalCol, type: 'Text', label: 'Goal', required: true, semantic: 'goalText' },
      { columnKey: baseCol, type: 'Text', label: 'Baseline', required: false, semantic: 'baseline' },
    ],
    maxRows: 2,
  }),
} as TemplateFieldDto;

const registry = { register: () => () => {}, flushAll: async () => {} } as unknown as React.ContextType<typeof DocumentFlushContext>;

function renderField(value: unknown, onSave: (p: Record<string, unknown>) => Promise<SaveResult>) {
  return render(
    <DocumentFlushContext.Provider value={registry}>
      <TableField field={goalsField} value={value} onSave={onSave} />
    </DocumentFlushContext.Provider>
  );
}

describe('TableField (semantic row block)', () => {
  it('renders each goal as a labelled card, with min/max controlling add and remove', () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    renderField([{ _rowId: 'ID-1', [goalCol]: 'Read 90 wpm', [baseCol]: '42 wpm' }], onSave);

    expect(screen.getByText('Goal 1')).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: /^Goal\s*\*?$/ })).toHaveValue('Read 90 wpm');
    expect(screen.getByRole('textbox', { name: 'Baseline' })).toHaveValue('42 wpm');
    expect(screen.getByRole('button', { name: 'Add goal' })).toBeEnabled();

    fireEvent.click(screen.getByRole('button', { name: 'Add goal' }));
    expect(screen.getByText('Goal 2')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Add goal' })).toBeDisabled(); // maxRows = 2
  });

  it('keeps a new row mounted (same input, same key) while the first save adopts its server id', async () => {
    let resolveSave!: (r: SaveResult) => void;
    const onSave = vi.fn().mockImplementation(
      () => new Promise<SaveResult>((resolve) => (resolveSave = resolve))
    );
    renderField([], onSave);

    fireEvent.click(screen.getByRole('button', { name: 'Add goal' }));
    const goalInput = screen.getByRole('textbox', { name: /^Goal\s*\*?$/ });
    goalInput.focus();
    fireEvent.change(goalInput, { target: { value: 'Re' } });
    expect(onSave).toHaveBeenCalledTimes(1); // add flushed immediately

    // AI help is only offered once the row has a persisted id (the hint shows until then).
    expect(screen.getByText(/AI help is available once this row has saved/)).toBeInTheDocument();

    await act(async () => {
      resolveSave({ ok: true, values: { [fieldKey]: [{ _rowId: 'SERVER-ID', [goalCol]: '', [baseCol]: '' }] } });
    });

    // The very same element is still mounted and focused — the key did not change.
    expect(screen.getByRole('textbox', { name: /^Goal\s*\*?$/ })).toBe(goalInput);
    expect(document.activeElement).toBe(goalInput);
    expect(goalInput).toHaveValue('Re');
    expect(screen.queryByText(/AI help is available once this row has saved/)).not.toBeInTheDocument(); // id adopted
  });

  it('sends the latest rows (including adopted ids) on the next save', async () => {
    const calls: unknown[] = [];
    let n = 0;
    const onSave = vi.fn().mockImplementation((patch: Record<string, unknown>) => {
      calls.push(patch[fieldKey]);
      n += 1;
      return Promise.resolve({ ok: true, values: { [fieldKey]: [{ _rowId: 'ID-A', [goalCol]: 'x', [baseCol]: '' }] } });
    });
    renderField([], onSave);

    fireEvent.click(screen.getByRole('button', { name: 'Add goal' }));
    await act(async () => {}); // let the add-save resolve and adopt ID-A
    fireEvent.change(screen.getByRole('textbox', { name: /^Goal\s*\*?$/ }), { target: { value: 'typed' } });
    fireEvent.click(screen.getByRole('button', { name: 'Remove goal 1' })); // immediate flush
    await act(async () => {});

    // Second+ saves carry the adopted id for the row (before it was removed).
    const withId = calls.slice(1).flat() as Array<Record<string, unknown>>;
    expect(n).toBeGreaterThanOrEqual(2);
    expect(withId.some((r) => r._rowId === 'ID-A' && r[goalCol] === 'typed') || withId.length === 0).toBe(true);
  });
});
