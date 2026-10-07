import '@testing-library/jest-dom';
import { describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import type { TemplateFieldDto } from '@/features/admin/templates/types';
// `document-authoring` and `educator` are both staff-only namespaces (plan
// phase 5) — see `@/app/lazy-routes/staff-locales`'s doc comment and
// `docs/i18n/README.md`'s "Staff and admin namespaces". This component
// renders directly here (not through the lazy route), so their English must
// be registered the same way the real route chunk does. `educator` is
// needed because `ReadAccommodations`/`resolveOwnerDisplay` now name it
// (for `teamRoleLabel`).
import '@/app/lazy-routes/staff-locales';
import { ReadAccommodations } from './read-accommodations';
import { DocumentEditorContext, type DocumentEditorContextValue } from '../../../hooks/document-editor-context';
import type { StudentTeamCache } from '../../../hooks/use-student-team';

const categoryCol = 'c1111111-1111-1111-1111-111111111111';
const accommodationCol = 'c2222222-2222-2222-2222-222222222222';
const fieldKey = 'f1111111-1111-1111-1111-111111111111';

function field(): TemplateFieldDto {
  return {
    id: 1,
    fieldKey,
    fieldType: 'Table',
    label: 'Accommodations',
    required: false,
    displayOrder: 0,
    configJson: JSON.stringify({
      semantic: 'accommodations',
      columns: [
        {
          columnKey: categoryCol,
          type: 'Select',
          label: 'Category',
          required: false,
          semantic: 'category',
          configJson: JSON.stringify({ options: [{ value: 'instruction', label: 'Instruction' }, { value: 'testing', label: 'Testing' }] }),
        },
        { columnKey: accommodationCol, type: 'Text', label: 'Accommodation', required: false, semantic: 'accommodation' },
      ],
    }),
  } as TemplateFieldDto;
}

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

function renderWithTeam(value: unknown, team: StudentTeamCache = { members: [ana], isLoading: false, isError: false }) {
  const editor = { team } as unknown as DocumentEditorContextValue;
  return render(
    <DocumentEditorContext.Provider value={editor}>
      <ReadAccommodations field={field()} value={value} />
    </DocumentEditorContext.Provider>
  );
}

describe('ReadAccommodations', () => {
  it('groups items by the category column\'s resolved option label', () => {
    renderWithTeam([
      { _rowId: 'r1', [categoryCol]: 'instruction', [accommodationCol]: 'Visual schedule' },
      { _rowId: 'r2', [categoryCol]: 'testing', [accommodationCol]: 'Extended time' },
      { _rowId: 'r3', [categoryCol]: 'instruction', [accommodationCol]: 'Graphic organizers' },
    ]);

    const instruction = screen.getByTestId(`read-field-${fieldKey}-group-Instruction`);
    expect(within(instruction).getByText('Visual schedule')).toBeInTheDocument();
    expect(within(instruction).getByText('Graphic organizers')).toBeInTheDocument();

    const testing = screen.getByTestId(`read-field-${fieldKey}-group-Testing`);
    expect(within(testing).getByText('Extended time')).toBeInTheDocument();
  });

  it('falls back to "Uncategorized" for a row with no category', () => {
    renderWithTeam([{ _rowId: 'r1', [accommodationCol]: 'Preferential seating' }]);
    expect(screen.getByTestId(`read-field-${fieldKey}-group-Uncategorized`)).toHaveTextContent('Preferential seating');
  });

  it("shows each item's owner by name + role, and \"Not set\" for a blank accommodation", () => {
    renderWithTeam([{ _rowId: 'r1', [categoryCol]: 'instruction', [accommodationCol]: '', _ownerUserId: 7 }]);
    expect(screen.getByText('Not set')).toBeInTheDocument();
    expect(screen.getByText('Ana Ito — Occupational therapist')).toBeInTheDocument();
  });

  it('shows "Former team member" for an owner no longer on the active team', () => {
    renderWithTeam(
      [{ _rowId: 'r1', [categoryCol]: 'instruction', [accommodationCol]: 'Visual schedule', _ownerUserId: 999 }],
      { members: [ana], isLoading: false, isError: false }
    );
    expect(screen.getByText('Former team member')).toBeInTheDocument();
  });

  it('shows "No rows yet." when empty', () => {
    renderWithTeam([]);
    expect(screen.getByText('No rows yet.')).toBeInTheDocument();
  });
});
