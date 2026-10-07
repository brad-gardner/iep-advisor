import '@testing-library/jest-dom';
import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import type { TemplateFieldDto } from '@/features/admin/templates/types';
// `document-authoring` and `educator` are both staff-only namespaces (plan
// phase 5) — see `@/app/lazy-routes/staff-locales`'s doc comment and
// `docs/i18n/README.md`'s "Staff and admin namespaces". This component
// renders directly here (not through the lazy route), so their English must
// be registered the same way the real route chunk does. `educator` is
// needed because `ReadGoals`/`resolveOwnerDisplay` now name it (for
// `teamRoleLabel`).
import '@/app/lazy-routes/staff-locales';
import { ReadGoals } from './read-goals';
import { DocumentEditorContext, type DocumentEditorContextValue } from '../../../hooks/document-editor-context';
import type { StudentTeamCache } from '../../../hooks/use-student-team';

const domainCol = 'c1111111-1111-1111-1111-111111111111';
const goalTextCol = 'c2222222-2222-2222-2222-222222222222';
const measurementCol = 'c3333333-3333-3333-3333-333333333333';
const timeframeCol = 'c4444444-4444-4444-4444-444444444444';
const fieldKey = 'f1111111-1111-1111-1111-111111111111';

function field(): TemplateFieldDto {
  return {
    id: 1,
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
        { columnKey: measurementCol, type: 'Text', label: 'Measurement', required: false, semantic: 'measurementMethod' },
        { columnKey: timeframeCol, type: 'Text', label: 'Timeframe', required: false, semantic: 'timeframe' },
      ],
    }),
  } as TemplateFieldDto;
}

const steph = {
  id: 1,
  userId: 9,
  staffProfileId: 1,
  firstName: 'Steph',
  lastName: 'Case',
  email: 'steph@example.com',
  orgRoleName: 'Teacher',
  teamRole: 'CaseManager',
  isLead: true,
  accessRole: 'Owner',
  isActive: true,
  addedAt: '2026-01-01T00:00:00Z',
} as const;

function renderWithTeam(
  value: unknown,
  opts: { team?: StudentTeamCache; onEditRow?: (rowKey: string) => void } = {}
) {
  const team = opts.team ?? { members: [steph], isLoading: false, isError: false };
  const editor = { team } as unknown as DocumentEditorContextValue;
  return render(
    <DocumentEditorContext.Provider value={editor}>
      <ReadGoals field={field()} value={value} onEditRow={opts.onEditRow} />
    </DocumentEditorContext.Provider>
  );
}

describe('ReadGoals', () => {
  it('shows a card per goal: number, area, clamped statement, chips, objectives count and owner', () => {
    renderWithTeam([
      {
        _rowId: 'r1',
        [domainCol]: 'Reading fluency',
        [goalTextCol]: 'Jordan will read 100 words per minute.',
        [measurementCol]: 'Weekly ORF probe',
        [timeframeCol]: 'By October 2027',
        _ownerUserId: 9,
        _objectives: [{ _rowId: 'o1', description: 'a' }, { _rowId: 'o2', description: 'b' }],
      },
    ]);
    const card = screen.getByTestId('read-goal-0');
    expect(within(card).getByText('Reading fluency')).toBeInTheDocument();
    expect(within(card).getByText(/Jordan will read 100 words per minute/)).toBeInTheDocument();
    expect(within(card).getByText('Weekly ORF probe')).toBeInTheDocument();
    expect(within(card).getByText('By October 2027')).toBeInTheDocument();
    expect(within(card).getByText('2 objectives')).toBeInTheDocument();
    expect(within(card).getByText('Steph Case — Case manager')).toBeInTheDocument();
  });

  it('shows "No owner" in amber when a goal has no owner, and "0 objectives" when the array is empty', () => {
    renderWithTeam([{ _rowId: 'r1', [goalTextCol]: 'Read better', _objectives: [] }]);
    const card = screen.getByTestId('read-goal-0');
    expect(within(card).getByText('No owner')).toBeInTheDocument();
    expect(within(card).getByText('0 objectives')).toBeInTheDocument();
  });

  it('shows a carried-forward badge, marking it "needs review" until reviewed', () => {
    renderWithTeam([
      { _rowId: 'r1', [goalTextCol]: 'Read better', _carriedFrom: { versionId: 3, rowId: 'r1', label: 'IEP v1' }, _confirmed: false },
    ]);
    expect(screen.getByTestId('read-goal-0-carried')).toHaveTextContent('Carried from IEP v1');
    expect(screen.getByTestId('read-goal-0-carried')).toHaveTextContent('needs review');
  });

  it('omits "needs review" once a carried row has been confirmed', () => {
    renderWithTeam([
      { _rowId: 'r1', [goalTextCol]: 'Read better', _carriedFrom: { versionId: 3, rowId: 'r1', label: 'IEP v1' }, _confirmed: true },
    ]);
    expect(screen.getByTestId('read-goal-0-carried')).not.toHaveTextContent('needs review');
  });

  it('shows "No goal statement yet" when the goal text is blank', () => {
    renderWithTeam([{ _rowId: 'r1' }]);
    expect(screen.getByText('No goal statement yet')).toBeInTheDocument();
  });

  it('shows the empty state when there are no goals', () => {
    renderWithTeam([]);
    expect(screen.getByText('No goals yet.')).toBeInTheDocument();
    expect(screen.getByText('0 goals')).toBeInTheDocument();
  });

  it('calls onEditRow with the goal\'s persisted id when "Edit goal" is clicked', () => {
    const onEditRow = vi.fn();
    renderWithTeam([{ _rowId: 'r1', [goalTextCol]: 'Read better' }], { onEditRow });
    screen.getByTestId('read-goal-0-edit').click();
    expect(onEditRow).toHaveBeenCalledWith('r1');
  });

  it('omits the "Edit goal" button when no onEditRow is given', () => {
    renderWithTeam([{ _rowId: 'r1', [goalTextCol]: 'Read better' }]);
    expect(screen.queryByTestId('read-goal-0-edit')).not.toBeInTheDocument();
  });
});
