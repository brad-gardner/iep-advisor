import { describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent, act, waitFor, within } from '@testing-library/react';
import type { TemplateFieldDto } from '@/features/admin/templates/types';
import { TableField } from './table-field';
import { ToastProvider } from '@/components/ui/toast';
import { DocumentFlushContext } from '../../hooks/flush-registry-context';
import { DocumentEditorContext, type DocumentEditorContextValue } from '../../hooks/document-editor-context';
import type { SaveResult } from '../../hooks/use-document-instance';
import type { StudentTeamCache } from '../../hooks/use-student-team';

const serviceTypeCol = 'c1111111-1111-1111-1111-111111111111';
const providerRoleCol = 'c2222222-2222-2222-2222-222222222222';
const frequencyCol = 'c3333333-3333-3333-3333-333333333333';
const durationCol = 'c4444444-4444-4444-4444-444444444444';
const locationCol = 'c5555555-5555-5555-5555-555555555555';
const startDateCol = 'c6666666-6666-6666-6666-666666666666';
const endDateCol = 'c7777777-7777-7777-7777-777777777777';
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
      { columnKey: serviceTypeCol, type: 'Text', label: 'Service type', required: true, semantic: 'serviceType' },
      { columnKey: providerRoleCol, type: 'Text', label: 'Provider role', required: false, semantic: 'providerRole' },
      { columnKey: frequencyCol, type: 'Text', label: 'Frequency', required: false, semantic: 'frequency' },
      { columnKey: durationCol, type: 'Text', label: 'Duration', required: false, semantic: 'duration' },
      {
        columnKey: locationCol,
        type: 'Select',
        label: 'Setting',
        required: false,
        semantic: 'location',
        configJson: JSON.stringify({ options: [{ value: 'gen-ed', label: 'General education classroom' }, { value: 'sped', label: 'Special education setting' }] }),
      },
      { columnKey: startDateCol, type: 'Date', label: 'Start', required: false, semantic: 'startDate' },
      { columnKey: endDateCol, type: 'Date', label: 'End', required: false, semantic: 'endDate' },
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

function renderServices(
  value: unknown,
  onSave: (p: Record<string, unknown>) => Promise<SaveResult>,
  opts: { team?: StudentTeamCache; initialFocusRowKey?: string } = {}
) {
  const editor = {
    instanceId: 1,
    studentId: 2,
    shareableEntries: { entries: [], isLoading: false, isError: false, ensureLoaded: async () => {} },
    team: opts.team ?? teamOf(ana),
    setActiveField: () => {},
    clearActiveField: () => {},
  } as unknown as DocumentEditorContextValue;
  return render(
    <ToastProvider>
      <DocumentEditorContext.Provider value={editor}>
        <DocumentFlushContext.Provider value={registry}>
          <TableField field={servicesField} value={value} onSave={onSave} initialFocusRowKey={opts.initialFocusRowKey} />
        </DocumentFlushContext.Provider>
      </DocumentEditorContext.Provider>
    </ToastProvider>
  );
}

describe('ServicesBlock — schedule row and focused editor', () => {
  it('renders a compact schedule row per service, and "Edit" switches to the focused editor', () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    renderServices(
      [{ _rowId: 'ID-1', [serviceTypeCol]: 'Speech-language therapy', [frequencyCol]: '2 per week', [durationCol]: '20 minutes' }],
      onSave
    );

    expect(screen.getByText(/1 service/)).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: /Editing:/ })).not.toBeInTheDocument();

    fireEvent.click(screen.getByTestId(`field-${fieldKey}-row-0-edit`));
    expect(screen.getByRole('heading', { name: 'Editing: Speech-language therapy' })).toBeInTheDocument();

    fireEvent.click(screen.getByTestId(`field-${fieldKey}-row-0-done`));
    expect(screen.queryByRole('heading', { name: /Editing:/ })).not.toBeInTheDocument();
    expect(screen.getByTestId(`field-${fieldKey}-row-0-edit`)).toBeInTheDocument();
  });

  it('lands directly in the focused editor when opened via initialFocusRowKey (Edit from read)', () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    renderServices(
      [
        { _rowId: 'ID-1', [serviceTypeCol]: 'First service' },
        { _rowId: 'ID-2', [serviceTypeCol]: 'Second service' },
      ],
      onSave,
      { initialFocusRowKey: 'ID-2' }
    );
    expect(screen.getByRole('heading', { name: 'Editing: Second service' })).toBeInTheDocument();
  });

  it('shows the empty state, and "Add service" opens a new service straight into its focused editor', async () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    renderServices([], onSave);
    expect(screen.getByText('No services yet. Add one to get started.')).toBeInTheDocument();

    await act(async () => fireEvent.click(screen.getByTestId(`field-${fieldKey}-add`)));
    expect(screen.getByRole('heading', { name: 'Editing: Service 1' })).toBeInTheDocument();
    expect(onSave).toHaveBeenCalledTimes(1); // add flushes immediately
  });

  it('starts a new service\'s frequency count blank (placeholder only), never displaying an unsaved default', async () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    renderServices([], onSave);
    await act(async () => fireEvent.click(screen.getByTestId(`field-${fieldKey}-add`)));

    const countInput = screen.getByTestId(`field-${fieldKey}-cell-0-${frequencyCol}`);
    expect(countInput).toHaveValue(null); // blank — not defaulted to 1
    expect(countInput).toHaveAttribute('placeholder', '1');
    // What's displayed matches what's actually stored: nothing.
    const sent = (onSave.mock.calls.at(-1)?.[0] as Record<string, unknown>)[fieldKey] as Array<Record<string, unknown>>;
    expect(sent[0][frequencyCol]).toBe('');
  });

  it('writes normalized frequency text as the count or period changes', async () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    renderServices([{ _rowId: 'ID-1', [serviceTypeCol]: 'OT', [frequencyCol]: '2 per week' }], onSave);
    fireEvent.click(screen.getByTestId(`field-${fieldKey}-row-0-edit`));

    const countInput = screen.getByTestId(`field-${fieldKey}-cell-0-${frequencyCol}`);
    expect(countInput).toHaveValue(2);
    fireEvent.change(countInput, { target: { value: '3' } });
    fireEvent.blur(countInput);

    await waitFor(() => expect(onSave).toHaveBeenCalled());
    const sent = (onSave.mock.calls.at(-1)?.[0] as Record<string, unknown>)[fieldKey] as Array<Record<string, unknown>>;
    expect(sent[0][frequencyCol]).toBe('3 per week');

    // Changing the period is a discrete pick — flushes immediately, no blur needed.
    fireEvent.change(screen.getByTestId(`field-${fieldKey}-cell-0-${frequencyCol}-period`), { target: { value: 'month' } });
    await waitFor(() => {
      const latest = (onSave.mock.calls.at(-1)?.[0] as Record<string, unknown>)[fieldKey] as Array<Record<string, unknown>>;
      expect(latest[0][frequencyCol]).toBe('3 per month');
    });
  });

  it('writes normalized duration text as the minutes field changes', async () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    renderServices([{ _rowId: 'ID-1', [serviceTypeCol]: 'OT', [durationCol]: '30 minutes' }], onSave);
    fireEvent.click(screen.getByTestId(`field-${fieldKey}-row-0-edit`));

    const minutesInput = screen.getByTestId(`field-${fieldKey}-cell-0-${durationCol}`);
    expect(minutesInput).toHaveValue(30);
    fireEvent.change(minutesInput, { target: { value: '45' } });
    fireEvent.blur(minutesInput);

    await waitFor(() => expect(onSave).toHaveBeenCalled());
    const sent = (onSave.mock.calls.at(-1)?.[0] as Record<string, unknown>)[fieldKey] as Array<Record<string, unknown>>;
    expect(sent[0][durationCol]).toBe('45 minutes');
  });

  it('keeps unparseable frequency/duration text in a free-text fallback instead of destroying it', async () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    renderServices(
      [{ _rowId: 'ID-1', [serviceTypeCol]: 'OT', [frequencyCol]: 'as needed, per IEP team', [durationCol]: 'varies' }],
      onSave
    );
    fireEvent.click(screen.getByTestId(`field-${fieldKey}-row-0-edit`));

    const freqInput = screen.getByTestId(`field-${fieldKey}-cell-0-${frequencyCol}`);
    expect(freqInput).toHaveValue('as needed, per IEP team');
    expect(screen.getByTestId(`field-${fieldKey}-cell-0-${frequencyCol}-switch-structured`)).toBeInTheDocument();

    const durInput = screen.getByTestId(`field-${fieldKey}-cell-0-${durationCol}`);
    expect(durInput).toHaveValue('varies');

    // Switching to structured entry commits a normalized default immediately,
    // without having destroyed the original text until the user opted in.
    fireEvent.click(screen.getByTestId(`field-${fieldKey}-cell-0-${frequencyCol}-switch-structured`));
    await waitFor(() => expect(onSave).toHaveBeenCalled());
    const sent = (onSave.mock.calls.at(-1)?.[0] as Record<string, unknown>)[fieldKey] as Array<Record<string, unknown>>;
    expect(sent[0][frequencyCol]).toBe('1 per week');
    expect(screen.getByTestId(`field-${fieldKey}-cell-0-${frequencyCol}`)).toHaveValue(1);
  });

  it('renders the template\'s own Select options for a Select setting column', () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    renderServices([{ _rowId: 'ID-1', [serviceTypeCol]: 'OT', [locationCol]: 'sped' }], onSave);
    fireEvent.click(screen.getByTestId(`field-${fieldKey}-row-0-edit`));

    const setting = screen.getByTestId(`field-${fieldKey}-cell-0-${locationCol}`);
    expect(setting.tagName).toBe('SELECT');
    expect(within(setting).getByRole('option', { name: 'Special education setting' })).toBeInTheDocument();
  });

  it('sets an owner immediately through the same row autosave path', async () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    renderServices([{ _rowId: 'ID-1', [serviceTypeCol]: 'OT' }], onSave);
    fireEvent.click(screen.getByTestId(`field-${fieldKey}-row-0-edit`));

    const picker = screen.getByTestId(`field-${fieldKey}-row-0-owner`);
    expect(within(picker).getByRole('option', { name: 'Ana Ito — Occupational therapist' })).toBeInTheDocument();

    fireEvent.change(picker, { target: { value: '7' } });
    await waitFor(() =>
      expect(onSave).toHaveBeenCalledWith({ [fieldKey]: [{ _rowId: 'ID-1', [serviceTypeCol]: 'OT', _ownerUserId: 7 }] })
    );
  });

  it('shows a carried-forward badge with Keep as-is, which clears once kept', async () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    renderServices(
      [
        {
          _rowId: 'ID-1',
          [serviceTypeCol]: 'OT',
          _carriedFrom: { versionId: 3, rowId: 'ID-1', label: 'IEP v1' },
          _confirmed: false,
        },
      ],
      onSave
    );
    const carried = screen.getByTestId(`field-${fieldKey}-row-0-carried`);
    expect(carried).toHaveTextContent('Carried from IEP v1');
    // Same wording as ReadServices (shared ServiceReadRow) — this summary row
    // used to say "not yet reviewed" instead, a drift from the read view's
    // "needs review".
    expect(carried).toHaveTextContent('needs review');

    fireEvent.click(screen.getByTestId(`field-${fieldKey}-row-0-edit`));
    expect(screen.getByTestId(`field-${fieldKey}-row-0-keep`)).toBeInTheDocument();

    fireEvent.click(screen.getByTestId(`field-${fieldKey}-row-0-keep`));
    await waitFor(() => expect(onSave).toHaveBeenCalled());
    const sent = (onSave.mock.calls.at(-1)?.[0] as Record<string, unknown>)[fieldKey] as Array<Record<string, unknown>>;
    expect(sent[0]._confirmed).toBe(true);
    expect(screen.queryByTestId(`field-${fieldKey}-row-0-keep`)).not.toBeInTheDocument();
  });

  it('gives the repeated "Edit" buttons a distinguishing aria-label per service', () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    renderServices(
      [
        { _rowId: 'ID-1', [serviceTypeCol]: 'Speech-language therapy' },
        { _rowId: 'ID-2', [serviceTypeCol]: 'Occupational therapy' },
      ],
      onSave
    );
    expect(screen.getByRole('button', { name: 'Edit Speech-language therapy' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Edit Occupational therapy' })).toBeInTheDocument();
  });

  it('removes a service immediately, with no confirmation dialog', async () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    renderServices(
      [
        { _rowId: 'ID-1', [serviceTypeCol]: 'First' },
        { _rowId: 'ID-2', [serviceTypeCol]: 'Second' },
      ],
      onSave
    );
    fireEvent.click(screen.getByTestId(`field-${fieldKey}-row-0-edit`));
    fireEvent.click(screen.getByTestId(`field-${fieldKey}-remove-0`));

    await waitFor(() => expect(onSave).toHaveBeenCalled());
    expect(screen.queryByText('First')).not.toBeInTheDocument();
    expect(screen.getByText('Second')).toBeInTheDocument();
  });

  it('offers AI rewrite/improve help but not "Pull from student" for a saved service', () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    renderServices([{ _rowId: 'ID-1', [serviceTypeCol]: 'OT' }], onSave);
    fireEvent.click(screen.getByTestId(`field-${fieldKey}-row-0-edit`));

    expect(screen.getByTestId(`field-${fieldKey}-row-0-assist-bar`)).toBeInTheDocument();
    expect(screen.queryByTestId(`field-${fieldKey}-row-0-pull-button`)).not.toBeInTheDocument();
  });
});
