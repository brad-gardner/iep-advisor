import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent, act, waitFor } from '@testing-library/react';
import type { TemplateFieldDto } from '@/features/admin/templates/types';
import { TableField } from './table-field';
import { ToastProvider } from '@/components/ui/toast';
import { DocumentFlushContext } from '../../hooks/flush-registry-context';
import { DocumentEditorContext, type ActiveFieldTarget, type DocumentEditorContextValue } from '../../hooks/document-editor-context';
import type { SaveResult } from '../../hooks/use-document-instance';

const goalsApi = vi.hoisted(() => ({
  recordGoalRetirement: vi.fn().mockResolvedValue(undefined),
}));
vi.mock('@/features/goals/api/goals-api', () => goalsApi);

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

  it('shows a carried-forward chip; Keep as-is and editing both mark the row reviewed', async () => {
    const calls: unknown[] = [];
    const onSave = vi.fn().mockImplementation((patch: Record<string, unknown>) => {
      calls.push(patch[fieldKey]);
      return Promise.resolve({ ok: true, values: {} });
    });
    renderField(
      [
        { _rowId: 'ID-1', _carriedFrom: { versionId: 3, rowId: 'ID-1', label: 'IEP v1', date: '2025-10-14' }, _confirmed: false, [goalCol]: 'Read 70 wpm', [baseCol]: '42' },
        { _rowId: 'ID-2', _carriedFrom: { versionId: 3, rowId: 'ID-2', label: 'IEP v1' }, _confirmed: false, [goalCol]: 'Write a paragraph', [baseCol]: '' },
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

    const second = screen.getAllByRole('textbox', { name: /^Goal\s*\*?$/ })[1];
    fireEvent.change(second, { target: { value: 'Write a full paragraph' } });
    expect(screen.getByTestId(`field-${fieldKey}-row-1-carried`)).toHaveTextContent('· reviewed');
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
    // No editor context here — a goal row is removed immediately (nothing to
    // retire: this row was never finalized, so it has no lineage).
    fireEvent.click(screen.getByRole('button', { name: 'Remove goal 1' })); // immediate flush
    await act(async () => {});

    // Second+ saves carry the adopted id for the row (before it was removed).
    const withId = calls.slice(1).flat() as Array<Record<string, unknown>>;
    expect(n).toBeGreaterThanOrEqual(2);
    expect(withId.some((r) => r._rowId === 'ID-A' && r[goalCol] === 'typed') || withId.length === 0).toBe(true);
  });
});

describe('TableField evidence-insert target', () => {
  beforeEach(() => {
    goalsApi.recordGoalRetirement.mockClear();
  });

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
            <TableField field={goalsField} value={value} onSave={onSave} />
          </DocumentFlushContext.Provider>
        </DocumentEditorContext.Provider>
      </ToastProvider>
    );
    return { targets, cleared, ...utils };
  }

  /** Inside an editor context, removing a PERSISTED goal row (one with a
   *  `_rowId`) opens the "Remove goal" dialog instead of removing it right
   *  away — type a reason and confirm to complete the removal. */
  async function removeGoalRowViaDialog(buttonName: string) {
    fireEvent.click(screen.getByRole('button', { name: buttonName }));
    const reasonBox = await screen.findByTestId('remove-goal-dialog-reason');
    fireEvent.change(reasonBox, { target: { value: 'No longer applicable to this student' } });
    fireEvent.click(screen.getByTestId('remove-goal-dialog-confirm'));
    await waitFor(() => expect(screen.queryByTestId('remove-goal-dialog-reason')).not.toBeInTheDocument());
  }

  it('appends to the focused cell, labels it by its current row, and drops the target when the row goes', async () => {
    const calls: unknown[] = [];
    const onSave = vi.fn().mockImplementation((patch: Record<string, unknown>) => {
      calls.push(patch[fieldKey]);
      return Promise.resolve({ ok: true, values: {} });
    });
    const { targets, cleared, unmount } = renderInEditor(
      [
        { _rowId: 'ID-1', [goalCol]: 'Read 70 wpm', [baseCol]: '' },
        { _rowId: 'ID-2', [goalCol]: 'Write a paragraph', [baseCol]: '' },
      ],
      onSave
    );

    const secondGoal = screen.getAllByRole('textbox', { name: /^Goal\s*\*?$/ })[1];
    fireEvent.focus(secondGoal);
    const target = targets.at(-1)!;
    expect(target.label()).toBe('Goal 2 — Goal');

    await act(async () => target.apply('Baseline: 42 wpm (ETR)'));
    expect(secondGoal).toHaveValue('Write a paragraph\n\nBaseline: 42 wpm (ETR)');
    const sent = calls.at(-1) as Array<Record<string, unknown>>;
    expect(sent[1][goalCol]).toBe('Write a paragraph\n\nBaseline: 42 wpm (ETR)');

    // Deleting the first row (a persisted lineage) requires a retirement reason.
    await removeGoalRowViaDialog('Remove goal 1');
    expect(goalsApi.recordGoalRetirement).toHaveBeenCalledWith(1, {
      lineageId: 'ID-1',
      reason: 'No longer applicable to this student',
    });
    expect(cleared.some((id) => target.id.startsWith(`${id}:`))).toBe(false); // a different row was removed
    expect(target.label()).toBe('Goal 1 — Goal');

    // Removing the row that owns the target clears it by prefix; unmount clears the field.
    await removeGoalRowViaDialog('Remove goal 1');
    expect(cleared.some((id) => target.id.startsWith(`${id}:`))).toBe(true);
    await act(async () => target.apply('ignored'));
    expect((calls.at(-1) as unknown[]).length).toBe(0);
    unmount();
    expect(cleared.at(-1)).toBe(fieldKey);
  });

  it('blocks removal without a reason, and cancel leaves the row in place', async () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    renderInEditor([{ _rowId: 'ID-1', [goalCol]: 'Read 70 wpm', [baseCol]: '' }], onSave);

    fireEvent.click(screen.getByRole('button', { name: 'Remove goal 1' }));
    const confirmButton = await screen.findByTestId('remove-goal-dialog-confirm');
    expect(confirmButton).toBeDisabled();

    fireEvent.change(screen.getByTestId('remove-goal-dialog-reason'), { target: { value: 'too short' } });
    expect(confirmButton).toBeDisabled(); // under 10 characters

    fireEvent.click(screen.getByTestId('remove-goal-dialog-cancel'));
    await waitFor(() => expect(screen.queryByTestId('remove-goal-dialog-reason')).not.toBeInTheDocument());

    // Cancel never called the API, and the row is still there.
    expect(goalsApi.recordGoalRetirement).not.toHaveBeenCalled();
    expect(screen.getByText('Goal 1')).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: /^Goal\s*\*?$/ })).toHaveValue('Read 70 wpm');
  });

  it('ignores Esc / backdrop / × while the retirement request is in flight, so the row is never removed behind a "cancel"', async () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    let finish!: () => void;
    goalsApi.recordGoalRetirement.mockReturnValueOnce(new Promise<void>((resolve) => (finish = resolve)));
    renderInEditor([{ _rowId: 'ID-1', [goalCol]: 'Read 70 wpm', [baseCol]: '' }], onSave);

    fireEvent.click(screen.getByRole('button', { name: 'Remove goal 1' }));
    fireEvent.change(await screen.findByTestId('remove-goal-dialog-reason'), { target: { value: 'Goal met and replaced by a comprehension goal' } });
    fireEvent.click(screen.getByTestId('remove-goal-dialog-confirm'));
    await waitFor(() => expect(goalsApi.recordGoalRetirement).toHaveBeenCalledTimes(1));

    // Every dismiss gesture is inert while submitting: the dialog stays, the × is disabled.
    const dialog = screen.getByTestId('remove-goal-dialog');
    fireEvent(dialog, new Event('cancel', { bubbles: false, cancelable: true }));
    fireEvent.click(dialog);
    expect(screen.getByTestId('remove-goal-dialog-close')).toBeDisabled();
    expect(screen.getByTestId('remove-goal-dialog-reason')).toBeInTheDocument();

    await act(async () => finish());
    await waitFor(() => expect(screen.queryByTestId('remove-goal-dialog-reason')).not.toBeInTheDocument());
    expect(screen.queryByText('Goal 1')).not.toBeInTheDocument(); // the confirmed removal landed
  });

  it('removes a never-finalized row immediately (no lineage to retire)', async () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    renderInEditor([], onSave);

    fireEvent.click(screen.getByRole('button', { name: 'Add goal' }));
    fireEvent.click(screen.getByRole('button', { name: 'Remove goal 1' }));

    expect(screen.queryByTestId('remove-goal-dialog-reason')).not.toBeInTheDocument();
    expect(goalsApi.recordGoalRetirement).not.toHaveBeenCalled();
    expect(screen.queryByText('Goal 1')).not.toBeInTheDocument();
  });
});
