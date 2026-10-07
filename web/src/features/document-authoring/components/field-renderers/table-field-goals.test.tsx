import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent, act, waitFor, within } from '@testing-library/react';
import type { TemplateFieldDto } from '@/features/admin/templates/types';
// `document-authoring` is a staff-only namespace (plan phase 5) — see
// `../../staff-locales`'s doc comment and `docs/i18n/README.md`'s "Staff and
// admin namespaces". This component renders directly here (not through the
// lazy route), so its English must be registered the same way the real
// route chunk does.
import '../../staff-locales';
import { TableField } from './table-field';
import { ToastProvider } from '@/components/ui/toast';
import { DocumentFlushContext } from '../../hooks/flush-registry-context';
import { DocumentEditorContext, type ActiveFieldTarget, type DocumentEditorContextValue } from '../../hooks/document-editor-context';
import type { SaveResult } from '../../hooks/use-document-instance';
import type { StudentTeamCache } from '../../hooks/use-student-team';

const goalsApi = vi.hoisted(() => ({
  recordGoalRetirement: vi.fn().mockResolvedValue(undefined),
}));
vi.mock('@/features/goals/api/goals-api', () => goalsApi);

const domainCol = 'c1111111-1111-1111-1111-111111111111';
const goalTextCol = 'c2222222-2222-2222-2222-222222222222';
const baselineCol = 'c3333333-3333-3333-3333-333333333333';
const targetCol = 'c4444444-4444-4444-4444-444444444444';
const measurementCol = 'c5555555-5555-5555-5555-555555555555';
const timeframeCol = 'c6666666-6666-6666-6666-666666666666';
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
      { columnKey: domainCol, type: 'Text', label: 'Area', required: false, semantic: 'domain' },
      { columnKey: goalTextCol, type: 'Text', label: 'Goal', required: true, semantic: 'goalText' },
      { columnKey: baselineCol, type: 'Text', label: 'Baseline', required: false, semantic: 'baseline' },
      { columnKey: targetCol, type: 'Text', label: 'Target criteria', required: false, semantic: 'targetCriteria' },
      { columnKey: measurementCol, type: 'Text', label: 'Measurement', required: false, semantic: 'measurementMethod' },
      { columnKey: timeframeCol, type: 'Text', label: 'Timeframe', required: false, semantic: 'timeframe' },
    ],
  }),
} as TemplateFieldDto;

const registry = { register: () => () => {}, flushAll: async () => {} } as unknown as React.ContextType<typeof DocumentFlushContext>;

const ana = {
  id: 1,
  userId: 7,
  staffProfileId: 1,
  firstName: 'Ana',
  lastName: 'Ito',
  email: 'ana@example.com',
  orgRoleName: 'RelatedServiceProvider',
  teamRole: 'OccupationalTherapist',
  isLead: false,
  accessRole: 'Collaborator',
  isActive: true,
  addedAt: '2026-01-01T00:00:00Z',
} as const;

function teamOf(...members: StudentTeamCache['members']): StudentTeamCache {
  return { members, isLoading: false, isError: false };
}

function renderGoals(
  value: unknown,
  onSave: (p: Record<string, unknown>) => Promise<SaveResult>,
  opts: { team?: StudentTeamCache; initialFocusRowKey?: string } = {}
) {
  const targets: ActiveFieldTarget[] = [];
  const cleared: string[] = [];
  const editor = {
    instanceId: 1,
    studentId: 2,
    shareableEntries: { entries: [], isLoading: false, isError: false, ensureLoaded: async () => {} },
    team: opts.team ?? teamOf(ana),
    setActiveField: (t: ActiveFieldTarget) => targets.push(t),
    clearActiveField: (id: string) => cleared.push(id),
  } as unknown as DocumentEditorContextValue;
  const utils = render(
    <ToastProvider>
      <DocumentEditorContext.Provider value={editor}>
        <DocumentFlushContext.Provider value={registry}>
          <TableField field={goalsField} value={value} onSave={onSave} initialFocusRowKey={opts.initialFocusRowKey} />
        </DocumentFlushContext.Provider>
      </DocumentEditorContext.Provider>
    </ToastProvider>
  );
  return { targets, cleared, ...utils };
}

describe('GoalsBlock — card list and focused editor', () => {
  it('renders a compact summary card per goal, and "Edit goal" switches to the focused editor', () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    renderGoals(
      [{ _rowId: 'ID-1', [domainCol]: 'Reading', [goalTextCol]: 'Read 90 wpm', [measurementCol]: 'Weekly probe' }],
      onSave
    );

    expect(screen.getByText('1 goal')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: /Editing goal/ })).not.toBeInTheDocument();

    fireEvent.click(screen.getByTestId(`field-${fieldKey}-row-0-edit`));
    expect(screen.getByRole('heading', { name: 'Editing goal 1' })).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: /^Goal\s*\*?$/ })).toHaveValue('Read 90 wpm');

    fireEvent.click(screen.getByTestId(`field-${fieldKey}-row-0-done`));
    expect(screen.queryByRole('heading', { name: /Editing goal/ })).not.toBeInTheDocument();
    expect(screen.getByTestId(`field-${fieldKey}-row-0-edit`)).toBeInTheDocument();
  });

  it('renders the goal statement as formatted markdown in the summary card, not raw markdown text (shared with ReadGoals)', () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    renderGoals([{ _rowId: 'ID-1', [goalTextCol]: '**Jordan** will read fluently.' }], onSave);

    const card = screen.getByTestId(`field-${fieldKey}-row-0`);
    expect(within(card).getByText('Jordan').tagName).toBe('STRONG');
    expect(within(card).queryByText(/\*\*Jordan\*\*/)).not.toBeInTheDocument();
    // Edit button is given a distinguishing label — repeated "Edit goal" text
    // is otherwise indistinguishable to a screen reader.
    expect(within(card).getByRole('button', { name: 'Edit goal 1' })).toBeInTheDocument();
  });

  it('moves focus into the editor on "Edit goal", and back to that button on Done', async () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    renderGoals([{ _rowId: 'ID-1', [domainCol]: 'Reading', [goalTextCol]: 'Read 90 wpm' }], onSave);

    fireEvent.click(screen.getByTestId(`field-${fieldKey}-row-0-edit`));
    const firstField = screen.getByRole('textbox', { name: /^Area\s*\*?$/ });
    await waitFor(() => expect(document.activeElement).toBe(firstField));

    fireEvent.click(screen.getByTestId(`field-${fieldKey}-row-0-done`));
    await waitFor(() => expect(document.activeElement).toBe(screen.getByTestId(`field-${fieldKey}-row-0-edit`)));
  });

  it('lands directly in the focused editor when opened via initialFocusRowKey', () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    renderGoals(
      [
        { _rowId: 'ID-1', [goalTextCol]: 'First goal' },
        { _rowId: 'ID-2', [goalTextCol]: 'Second goal' },
      ],
      onSave,
      { initialFocusRowKey: 'ID-2' }
    );
    expect(screen.getByRole('heading', { name: 'Editing goal 2' })).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: /^Goal\s*\*?$/ })).toHaveValue('Second goal');
  });

  it('shows the empty state and "Add goal" opens a new goal straight into its focused editor', async () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    renderGoals([], onSave);
    expect(screen.getByText('No goals yet. Add one to get started.')).toBeInTheDocument();

    await act(async () => fireEvent.click(screen.getByTestId(`field-${fieldKey}-add`)));
    expect(screen.getByRole('heading', { name: 'Editing goal 1' })).toBeInTheDocument();
    expect(onSave).toHaveBeenCalledTimes(1); // add flushes immediately
  });

  it('saves rich-text goal statement/baseline/target as markdown strings in the same cell', async () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    renderGoals([{ _rowId: 'ID-1', [goalTextCol]: '' }], onSave);
    fireEvent.click(screen.getByTestId(`field-${fieldKey}-row-0-edit`));

    fireEvent.change(screen.getByRole('textbox', { name: /^Goal\s*\*?$/ }), {
      target: { value: '**Jordan** will read fluently.' },
    });
    fireEvent.change(screen.getByRole('textbox', { name: 'Baseline' }), { target: { value: 'Reads 40 wpm' } });
    fireEvent.blur(screen.getByRole('textbox', { name: 'Baseline' }));

    await waitFor(() => expect(onSave).toHaveBeenCalled());
    const sent = (onSave.mock.calls.at(-1)?.[0] as Record<string, unknown>)[fieldKey] as Array<Record<string, unknown>>;
    expect(sent[0][goalTextCol]).toBe('**Jordan** will read fluently.');
    expect(sent[0][baselineCol]).toBe('Reads 40 wpm');
  });

  it('shows a carried-forward badge with Keep as-is, which clears once kept', async () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    renderGoals(
      [
        {
          _rowId: 'ID-1',
          [goalTextCol]: 'Read 70 wpm',
          _carriedFrom: { versionId: 3, rowId: 'ID-1', label: 'IEP v1' },
          _confirmed: false,
        },
      ],
      onSave
    );
    fireEvent.click(screen.getByTestId(`field-${fieldKey}-row-0-edit`));
    expect(screen.getByTestId(`field-${fieldKey}-row-0-keep`)).toBeInTheDocument();

    fireEvent.click(screen.getByTestId(`field-${fieldKey}-row-0-keep`));
    await waitFor(() => expect(onSave).toHaveBeenCalled());
    const sent = (onSave.mock.calls.at(-1)?.[0] as Record<string, unknown>)[fieldKey] as Array<Record<string, unknown>>;
    expect(sent[0]._confirmed).toBe(true);
    expect(screen.queryByTestId(`field-${fieldKey}-row-0-keep`)).not.toBeInTheDocument();
  });

  it('owner picker is present in the side panel and sets the owner immediately', async () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    renderGoals([{ _rowId: 'ID-1', [goalTextCol]: 'Read better' }], onSave);
    fireEvent.click(screen.getByTestId(`field-${fieldKey}-row-0-edit`));

    const picker = screen.getByTestId(`field-${fieldKey}-row-0-owner`);
    expect(within(picker).getByRole('option', { name: 'Ana Ito — Occupational therapist' })).toBeInTheDocument();

    fireEvent.change(picker, { target: { value: '7' } });
    await waitFor(() =>
      expect(onSave).toHaveBeenCalledWith({
        [fieldKey]: [{ _rowId: 'ID-1', [goalTextCol]: 'Read better', _ownerUserId: 7 }],
      })
    );
  });

  it('opens the existing RemoveGoalDialog for a persisted goal via "Remove goal…"', async () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    renderGoals([{ _rowId: 'ID-1', [goalTextCol]: 'Read better' }], onSave);
    fireEvent.click(screen.getByTestId(`field-${fieldKey}-row-0-edit`));

    fireEvent.click(screen.getByTestId(`field-${fieldKey}-remove-0`));
    const reasonBox = await screen.findByTestId('remove-goal-dialog-reason');
    fireEvent.change(reasonBox, { target: { value: 'No longer applicable to this student' } });
    fireEvent.click(screen.getByTestId('remove-goal-dialog-confirm'));
    await waitFor(() => expect(screen.queryByTestId('remove-goal-dialog-reason')).not.toBeInTheDocument());

    expect(goalsApi.recordGoalRetirement).toHaveBeenCalledWith(1, {
      lineageId: 'ID-1',
      reason: 'No longer applicable to this student',
    });
    expect(screen.getByText('No goals yet. Add one to get started.')).toBeInTheDocument();
  });
});

describe('GoalsBlock — objectives', () => {
  function openGoal(onSave: (p: Record<string, unknown>) => Promise<SaveResult>, objectives: unknown = []) {
    renderGoals([{ _rowId: 'ID-1', [goalTextCol]: 'Read better', _objectives: objectives }], onSave);
    fireEvent.click(screen.getByTestId(`field-${fieldKey}-row-0-edit`));
  }

  const testIdPrefix = `field-${fieldKey}-row-0`;

  it('adds an objective, flushing immediately, and removes one, also immediately', async () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    openGoal(onSave);

    fireEvent.click(screen.getByTestId(`${testIdPrefix}-objectives-add`));
    await waitFor(() => expect(onSave).toHaveBeenCalledTimes(1));
    let sent = (onSave.mock.calls.at(-1)?.[0] as Record<string, unknown>)[fieldKey] as Array<Record<string, unknown>>;
    expect(sent[0]._objectives).toEqual([{ description: '', criteria: '', targetDate: '' }]);

    fireEvent.click(screen.getByTestId(`${testIdPrefix}-objective-0-remove`));
    await waitFor(() => expect(onSave).toHaveBeenCalledTimes(2));
    sent = (onSave.mock.calls.at(-1)?.[0] as Record<string, unknown>)[fieldKey] as Array<Record<string, unknown>>;
    expect(sent[0]._objectives).toEqual([]);
    expect(screen.queryByTestId(`${testIdPrefix}-objective-0`)).not.toBeInTheDocument();
  });

  it('reorders objectives with the up/down buttons, flushing immediately', async () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    openGoal(onSave, [
      { _rowId: 'o1', description: 'first', criteria: '', targetDate: '' },
      { _rowId: 'o2', description: 'second', criteria: '', targetDate: '' },
    ]);

    expect(screen.getByTestId(`${testIdPrefix}-objective-0-description`)).toHaveValue('first');
    fireEvent.click(screen.getByTestId(`${testIdPrefix}-objective-0-down`));

    await waitFor(() => expect(onSave).toHaveBeenCalled());
    expect(screen.getByTestId(`${testIdPrefix}-objective-0-description`)).toHaveValue('second');
    expect(screen.getByTestId(`${testIdPrefix}-objective-1-description`)).toHaveValue('first');
    const sent = (onSave.mock.calls.at(-1)?.[0] as Record<string, unknown>)[fieldKey] as Array<Record<string, unknown>>;
    expect((sent[0]._objectives as Array<{ _rowId: string }>).map((o) => o._rowId)).toEqual(['o2', 'o1']);

    // Up/down are disabled at the ends.
    expect(screen.getByTestId(`${testIdPrefix}-objective-0-up`)).toBeDisabled();
    expect(screen.getByTestId(`${testIdPrefix}-objective-1-down`)).toBeDisabled();
  });

  it('keeps the same textarea mounted and focused while a newly-added (still blank) objective\'s save resolves', async () => {
    let resolveSave!: (r: SaveResult) => void;
    const onSave = vi.fn().mockImplementation(() => new Promise<SaveResult>((resolve) => (resolveSave = resolve)));
    openGoal(onSave);

    fireEvent.click(screen.getByTestId(`${testIdPrefix}-objectives-add`));
    const descInput = screen.getByTestId(`${testIdPrefix}-objective-0-description`);
    descInput.focus();
    fireEvent.change(descInput, { target: { value: 'Write a topic sentence' } });

    await act(async () => {
      // Realistic: the add's save sent a blank objective (typing into it
      // happened after dispatch), which a real server drops rather than
      // assigning it an id — see `adoptObjectiveIds`'s keep-rule.
      resolveSave({
        ok: true,
        values: { [fieldKey]: [{ _rowId: 'ID-1', [goalTextCol]: 'Read better', _objectives: [] }] },
      });
    });

    // Same element, still focused, still holding what was typed — the key never changed.
    expect(screen.getByTestId(`${testIdPrefix}-objective-0-description`)).toBe(descInput);
    expect(document.activeElement).toBe(descInput);
    expect(descInput).toHaveValue('Write a topic sentence');
  });

  it('sends the adopted objective id (not a fresh one) on the next save', async () => {
    const calls: unknown[] = [];
    // Mirrors the server's own keep rule instead of a canned response: a
    // wholly-blank objective is dropped (never assigned an id); anything else
    // keeps its existing id or is assigned one for the first time.
    const onSave = vi.fn().mockImplementation((patch: Record<string, unknown>) => {
      const sent = (patch[fieldKey] as Array<Record<string, unknown>>)[0]._objectives as Array<Record<string, unknown>>;
      calls.push(sent);
      const kept = sent.filter((o) => o.description || o.criteria || o.targetDate);
      return Promise.resolve({
        ok: true,
        values: {
          [fieldKey]: [
            {
              _rowId: 'ID-1',
              [goalTextCol]: 'Read better',
              _objectives: kept.map((o) => ({ ...o, _rowId: (o._rowId as string | undefined) ?? 'OBJ-1' })),
            },
          ],
        },
      });
    });
    openGoal(onSave);

    fireEvent.click(screen.getByTestId(`${testIdPrefix}-objectives-add`));
    await waitFor(() => expect(onSave).toHaveBeenCalledTimes(1)); // still blank — nothing kept, nothing adopted

    fireEvent.change(screen.getByTestId(`${testIdPrefix}-objective-0-criteria`), { target: { value: '4/5 trials' } });
    fireEvent.blur(screen.getByTestId(`${testIdPrefix}-objective-0-criteria`));
    await waitFor(() => expect(onSave).toHaveBeenCalledTimes(2));
    // What THIS save sent has no id yet — it's assigned in the response and
    // adopted only once that response lands.
    expect(calls[1]).toEqual([{ description: '', criteria: '4/5 trials', targetDate: '' }]);
    await waitFor(() => expect(screen.getByTestId(`${testIdPrefix}-objective-0-criteria`)).toHaveValue('4/5 trials'));

    fireEvent.change(screen.getByTestId(`${testIdPrefix}-objective-0-date`), { target: { value: 'June' } });
    fireEvent.blur(screen.getByTestId(`${testIdPrefix}-objective-0-date`));
    await waitFor(() => expect(onSave).toHaveBeenCalledTimes(3));

    const thirdSend = calls[2] as Array<Record<string, unknown>>;
    expect(thirdSend).toEqual([{ _rowId: 'OBJ-1', description: '', criteria: '4/5 trials', targetDate: 'June' }]);
  });

  it('round-trips an edited objective through Done/Edit-goal without blanking its text or losing its adopted id', async () => {
    const onSave = vi.fn().mockImplementation((patch: Record<string, unknown>) => {
      const sent = (patch[fieldKey] as Array<Record<string, unknown>>)[0]._objectives as Array<Record<string, unknown>>;
      return Promise.resolve({
        ok: true,
        values: {
          [fieldKey]: [{ _rowId: 'ID-1', [goalTextCol]: 'Read better', _objectives: sent.map((o) => ({ ...o, _rowId: 'OBJ-1' })) }],
        },
      });
    });
    openGoal(onSave, [{ _rowId: 'OBJ-1', description: 'first', criteria: '', targetDate: '' }]);

    fireEvent.change(screen.getByTestId(`${testIdPrefix}-objective-0-criteria`), { target: { value: '4/5' } });
    fireEvent.blur(screen.getByTestId(`${testIdPrefix}-objective-0-criteria`));
    await waitFor(() => expect(onSave).toHaveBeenCalledTimes(1));

    // Close the focused editor and reopen it.
    fireEvent.click(screen.getByTestId(`${testIdPrefix}-done`));
    fireEvent.click(screen.getByTestId(`field-${fieldKey}-row-0-edit`));

    // The objective's text survived the round trip — re-coercing an
    // already-keyed cell must not blank it out or mint it a fresh key.
    expect(screen.getByTestId(`${testIdPrefix}-objective-0-description`)).toHaveValue('first');
    expect(screen.getByTestId(`${testIdPrefix}-objective-0-criteria`)).toHaveValue('4/5');

    fireEvent.change(screen.getByTestId(`${testIdPrefix}-objective-0-date`), { target: { value: 'June' } });
    fireEvent.blur(screen.getByTestId(`${testIdPrefix}-objective-0-date`));
    await waitFor(() => expect(onSave).toHaveBeenCalledTimes(2));

    const sent = onSave.mock.calls.at(-1)?.[0] as Record<string, unknown>;
    const objectives = (sent[fieldKey] as Array<Record<string, unknown>>)[0]._objectives as Array<Record<string, unknown>>;
    expect(objectives).toEqual([{ _rowId: 'OBJ-1', description: 'first', criteria: '4/5', targetDate: 'June' }]);
  });

  it('editing only the goal text leaves a persisted, never-opened objectives list untouched (plain-vs-keyed cast no longer throws)', async () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    const persisted = [{ _rowId: 'OBJ-1', description: 'Write a sentence', criteria: '4/5', targetDate: 'Jan' }];
    openGoal(onSave, persisted);

    fireEvent.change(screen.getByRole('textbox', { name: /^Goal\s*\*?$/ }), { target: { value: 'Read better, updated' } });
    fireEvent.blur(screen.getByRole('textbox', { name: /^Goal\s*\*?$/ }));

    await waitFor(() => expect(onSave).toHaveBeenCalled());
    const sent = (onSave.mock.calls.at(-1)?.[0] as Record<string, unknown>)[fieldKey] as Array<Record<string, unknown>>;
    expect(sent[0][goalTextCol]).toBe('Read better, updated');
    expect(sent[0]._objectives).toEqual(persisted);
  });

  it('shows the "no objectives yet" empty state', () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    openGoal(onSave, []);
    expect(screen.getByText('No objectives yet.')).toBeInTheDocument();
  });
});

describe('GoalsBlock — removal (goal-retirement dialog) and evidence-insert targeting', () => {
  beforeEach(() => {
    goalsApi.recordGoalRetirement.mockClear();
  });

  it('focuses the goal statement, labels the evidence target by its row, and drops it when the goal is removed', async () => {
    const calls: unknown[] = [];
    const onSave = vi.fn().mockImplementation((patch: Record<string, unknown>) => {
      calls.push(patch[fieldKey]);
      return Promise.resolve({ ok: true, values: {} });
    });
    const { targets, cleared, unmount } = renderGoals([{ _rowId: 'ID-1', [goalTextCol]: 'Read 70 wpm' }], onSave);
    fireEvent.click(screen.getByTestId(`field-${fieldKey}-row-0-edit`));

    const goalInput = screen.getByRole('textbox', { name: /^Goal\s*\*?$/ });
    fireEvent.focus(goalInput);
    const target = targets.at(-1)!;
    expect(target.label()).toBe('Goal 1 — Goal');

    await act(async () => target.apply('Baseline: 42 wpm (ETR)'));
    expect(goalInput).toHaveValue('Read 70 wpm\n\nBaseline: 42 wpm (ETR)');
    const sent = calls.at(-1) as Array<Record<string, unknown>>;
    expect(sent[0][goalTextCol]).toBe('Read 70 wpm\n\nBaseline: 42 wpm (ETR)');

    // Removing the row that owns the target clears it by prefix; unmount clears the field.
    fireEvent.click(screen.getByTestId(`field-${fieldKey}-remove-0`));
    const reasonBox = await screen.findByTestId('remove-goal-dialog-reason');
    fireEvent.change(reasonBox, { target: { value: 'No longer applicable to this student' } });
    fireEvent.click(screen.getByTestId('remove-goal-dialog-confirm'));
    await waitFor(() => expect(screen.queryByTestId('remove-goal-dialog-reason')).not.toBeInTheDocument());

    expect(cleared.some((id) => target.id.startsWith(`${id}:`))).toBe(true);
    await act(async () => target.apply('ignored'));
    expect((calls.at(-1) as unknown[]).length).toBe(0);
    unmount();
    expect(cleared.at(-1)).toBe(fieldKey);
  });

  it('blocks removal without a reason, and cancel leaves the goal in place', async () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    renderGoals([{ _rowId: 'ID-1', [goalTextCol]: 'Read 70 wpm' }], onSave);
    fireEvent.click(screen.getByTestId(`field-${fieldKey}-row-0-edit`));

    fireEvent.click(screen.getByTestId(`field-${fieldKey}-remove-0`));
    const confirmButton = await screen.findByTestId('remove-goal-dialog-confirm');
    expect(confirmButton).toBeDisabled();

    fireEvent.change(screen.getByTestId('remove-goal-dialog-reason'), { target: { value: 'too short' } });
    expect(confirmButton).toBeDisabled(); // under 10 characters

    fireEvent.click(screen.getByTestId('remove-goal-dialog-cancel'));
    await waitFor(() => expect(screen.queryByTestId('remove-goal-dialog-reason')).not.toBeInTheDocument());

    // Cancel never called the API, and the goal is still there, still open.
    expect(goalsApi.recordGoalRetirement).not.toHaveBeenCalled();
    expect(screen.getByRole('heading', { name: 'Editing goal 1' })).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: /^Goal\s*\*?$/ })).toHaveValue('Read 70 wpm');
  });

  it('ignores Esc / backdrop / × while the retirement request is in flight, so the goal is never removed behind a "cancel"', async () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    let finish!: () => void;
    goalsApi.recordGoalRetirement.mockReturnValueOnce(new Promise<void>((resolve) => (finish = resolve)));
    renderGoals([{ _rowId: 'ID-1', [goalTextCol]: 'Read 70 wpm' }], onSave);
    fireEvent.click(screen.getByTestId(`field-${fieldKey}-row-0-edit`));

    fireEvent.click(screen.getByTestId(`field-${fieldKey}-remove-0`));
    fireEvent.change(await screen.findByTestId('remove-goal-dialog-reason'), {
      target: { value: 'Goal met and replaced by a comprehension goal' },
    });
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
    expect(screen.queryByRole('heading', { name: /Editing goal/ })).not.toBeInTheDocument(); // the confirmed removal landed
  });

  it('removes a never-finalized goal immediately (no lineage to retire, no dialog)', async () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    renderGoals([], onSave);

    await act(async () => fireEvent.click(screen.getByTestId(`field-${fieldKey}-add`)));
    await act(async () => fireEvent.click(screen.getByTestId(`field-${fieldKey}-remove-0`)));

    expect(screen.queryByTestId('remove-goal-dialog-reason')).not.toBeInTheDocument();
    expect(goalsApi.recordGoalRetirement).not.toHaveBeenCalled();
    expect(screen.queryByRole('heading', { name: /Editing goal/ })).not.toBeInTheDocument();
    expect(screen.getByText('No goals yet. Add one to get started.')).toBeInTheDocument();
  });
});
