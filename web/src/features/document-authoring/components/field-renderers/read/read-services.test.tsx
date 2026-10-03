import '@testing-library/jest-dom';
import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import type { TemplateFieldDto } from '@/features/admin/templates/types';
import { ReadServices } from './read-services';
import { DocumentEditorContext, type DocumentEditorContextValue } from '../../../hooks/document-editor-context';
import type { StudentTeamCache } from '../../../hooks/use-student-team';

const serviceTypeCol = 'c1111111-1111-1111-1111-111111111111';
const providerRoleCol = 'c2222222-2222-2222-2222-222222222222';
const frequencyCol = 'c3333333-3333-3333-3333-333333333333';
const durationCol = 'c4444444-4444-4444-4444-444444444444';
const locationCol = 'c5555555-5555-5555-5555-555555555555';
const startDateCol = 'c6666666-6666-6666-6666-666666666666';
const endDateCol = 'c7777777-7777-7777-7777-777777777777';
const fieldKey = 'f1111111-1111-1111-1111-111111111111';

function field(): TemplateFieldDto {
  return {
    id: 1,
    fieldKey,
    fieldType: 'Table',
    label: 'Services',
    required: false,
    displayOrder: 0,
    configJson: JSON.stringify({
      semantic: 'services',
      columns: [
        { columnKey: serviceTypeCol, type: 'Text', label: 'Service', required: true, semantic: 'serviceType' },
        { columnKey: providerRoleCol, type: 'Text', label: 'Provider role', required: false, semantic: 'providerRole' },
        { columnKey: frequencyCol, type: 'Text', label: 'Frequency', required: false, semantic: 'frequency' },
        { columnKey: durationCol, type: 'Text', label: 'Duration', required: false, semantic: 'duration' },
        { columnKey: locationCol, type: 'Text', label: 'Setting', required: false, semantic: 'location' },
        { columnKey: startDateCol, type: 'Date', label: 'Start', required: false, semantic: 'startDate' },
        { columnKey: endDateCol, type: 'Date', label: 'End', required: false, semantic: 'endDate' },
      ],
    }),
  } as TemplateFieldDto;
}

const dev = {
  id: 1,
  userId: 9,
  staffProfileId: 1,
  firstName: 'Dev',
  lastName: 'Patel',
  email: 'dev@example.com',
  orgRoleName: 'RelatedServiceProvider',
  teamRole: 'SpeechLanguagePathologist',
  isLead: false,
  accessRole: 'Collaborator',
  isActive: true,
  addedAt: '2026-01-01T00:00:00Z',
} as const;

function renderWithTeam(value: unknown, opts: { team?: StudentTeamCache; onEditRow?: (rowKey: string) => void } = {}) {
  const team = opts.team ?? { members: [dev], isLoading: false, isError: false };
  const editor = { team } as unknown as DocumentEditorContextValue;
  return render(
    <DocumentEditorContext.Provider value={editor}>
      <ReadServices field={field()} value={value} onEditRow={opts.onEditRow} />
    </DocumentEditorContext.Provider>
  );
}

describe('ReadServices', () => {
  it('shows a row per service: type, provider role, frequency × minutes, setting, dates and owner', () => {
    renderWithTeam([
      {
        _rowId: 'r1',
        [serviceTypeCol]: 'Speech-language therapy',
        [providerRoleCol]: 'Speech-language pathologist',
        [frequencyCol]: '2 per week',
        [durationCol]: '20 minutes',
        [locationCol]: 'Special education setting',
        [startDateCol]: '2026-10-12',
        [endDateCol]: '2027-10-11',
        _ownerUserId: 9,
      },
    ]);
    const row = screen.getByTestId('read-service-0');
    expect(within(row).getByText('Speech-language therapy')).toBeInTheDocument();
    expect(within(row).getByText('Speech-language pathologist')).toBeInTheDocument();
    expect(within(row).getByText('2×/week · 20 min')).toBeInTheDocument();
    expect(within(row).getByText('Special education setting')).toBeInTheDocument();
    expect(within(row).getByText(/Oct 12, 2026.*Oct 11, 2027/)).toBeInTheDocument();
    expect(within(row).getByText('Dev Patel — Speech-language pathologist')).toBeInTheDocument();
  });

  it('shows the header total minutes/week summed across parseable rows', () => {
    renderWithTeam([
      { _rowId: 'r1', [serviceTypeCol]: 'Reading', [frequencyCol]: '5 per week', [durationCol]: '30 minutes' },
      { _rowId: 'r2', [serviceTypeCol]: 'Counseling', [frequencyCol]: '1 per week', [durationCol]: '25 minutes' },
    ]);
    expect(screen.getByTestId(`read-field-${fieldKey}-total`)).toHaveTextContent('2 services · 175 min/week');
  });

  it('notes excluded rows in the header total when a frequency or duration does not parse', () => {
    renderWithTeam([
      { _rowId: 'r1', [serviceTypeCol]: 'Reading', [frequencyCol]: '5 per week', [durationCol]: '30 minutes' },
      { _rowId: 'r2', [serviceTypeCol]: 'OT', [frequencyCol]: 'as needed', [durationCol]: '30 minutes' },
    ]);
    expect(screen.getByTestId(`read-field-${fieldKey}-total`)).toHaveTextContent('2 services · 150 min/week');
    expect(screen.getByTestId(`read-field-${fieldKey}-total`)).toHaveTextContent('not counted');
  });

  it('shows "No owner" in amber when a service has no owner', () => {
    renderWithTeam([{ _rowId: 'r1', [serviceTypeCol]: 'Reading' }]);
    expect(within(screen.getByTestId('read-service-0')).getByText('No owner')).toBeInTheDocument();
  });

  it('shows "Not set" for a blank schedule and blank dates, and "—" for an unset setting', () => {
    renderWithTeam([{ _rowId: 'r1', [serviceTypeCol]: 'Reading' }]);
    const row = screen.getByTestId('read-service-0');
    // One "Not set" for the schedule cell, one for the (blank) dates cell.
    expect(within(row).getAllByText('Not set')).toHaveLength(2);
    expect(within(row).getByText('—')).toBeInTheDocument();
  });

  it('shows the empty state when there are no services', () => {
    renderWithTeam([]);
    expect(screen.getByText('No services yet.')).toBeInTheDocument();
    expect(screen.queryByTestId(`read-field-${fieldKey}-total`)).not.toBeInTheDocument();
  });

  it('calls onEditRow with the service\'s persisted id when "Edit" is clicked', () => {
    const onEditRow = vi.fn();
    renderWithTeam([{ _rowId: 'r1', [serviceTypeCol]: 'Reading' }], { onEditRow });
    screen.getByTestId('read-service-0-edit').click();
    expect(onEditRow).toHaveBeenCalledWith('r1');
  });

  it('omits the "Edit" button when no onEditRow is given', () => {
    renderWithTeam([{ _rowId: 'r1', [serviceTypeCol]: 'Reading' }]);
    expect(screen.queryByTestId('read-service-0-edit')).not.toBeInTheDocument();
  });
});
