import '@testing-library/jest-dom';
import { describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import type { TemplateFieldDto } from '@/features/admin/templates/types';
// `document-authoring` is a staff-only namespace (plan phase 5) — see
// `../../../staff-locales`'s doc comment and `docs/i18n/README.md`'s "Staff
// and admin namespaces". This component renders directly here (not through
// the lazy route), so its English must be registered the same way the real
// route chunk does.
import '../../../staff-locales';
import { ReadTransitionList } from './read-transition-list';
import { DocumentEditorContext, type DocumentEditorContextValue } from '../../../hooks/document-editor-context';
import type { StudentTeamCache } from '../../../hooks/use-student-team';

const areaCol = 'c1111111-1111-1111-1111-111111111111';
const servicesCol = 'c2222222-2222-2222-2222-222222222222';
const fieldKey = 'f1111111-1111-1111-1111-111111111111';

function field(): TemplateFieldDto {
  return {
    id: 1,
    fieldKey,
    fieldType: 'Table',
    label: 'Transition',
    required: false,
    displayOrder: 0,
    configJson: JSON.stringify({
      semantic: 'transition',
      columns: [
        { columnKey: areaCol, type: 'Text', label: 'Goal area', required: false, semantic: 'goalArea' },
        { columnKey: servicesCol, type: 'Text', label: 'Services', required: false, semantic: 'transitionServices' },
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

function renderWithTeam(value: unknown, team: StudentTeamCache = { members: [steph], isLoading: false, isError: false }) {
  const editor = { team } as unknown as DocumentEditorContextValue;
  return render(
    <DocumentEditorContext.Provider value={editor}>
      <ReadTransitionList field={field()} value={value} />
    </DocumentEditorContext.Provider>
  );
}

describe('ReadTransitionList', () => {
  it('labels each item by its own goal area and shows the owner alongside', () => {
    renderWithTeam([
      { _rowId: 'r1', [areaCol]: 'Education / training', [servicesCol]: 'Tour the STEM lab in spring.', _ownerUserId: 9 },
    ]);
    const item = screen.getByTestId(`read-field-${fieldKey}-item-r1`);
    expect(within(item).getByText('Education / training')).toBeInTheDocument();
    expect(within(item).getByText('Owner: Steph Case — Case manager')).toBeInTheDocument();
    expect(within(item).getByText('Tour the STEM lab in spring.')).toBeInTheDocument();
  });

  it('falls back when the goal area and services text are blank', () => {
    renderWithTeam([{ _rowId: 'r1' }]);
    expect(screen.getByText('Postsecondary goal area not set')).toBeInTheDocument();
    expect(screen.getByText('Not set')).toBeInTheDocument();
  });

  it('does not merge two rows that share a goal area into one box', () => {
    renderWithTeam([
      { _rowId: 'r1', [areaCol]: 'Employment', [servicesCol]: 'Job shadow a local employer.' },
      { _rowId: 'r2', [areaCol]: 'Employment', [servicesCol]: 'Mock interview practice.' },
    ]);
    expect(screen.getByTestId(`read-field-${fieldKey}-item-r1`)).toBeInTheDocument();
    expect(screen.getByTestId(`read-field-${fieldKey}-item-r2`)).toBeInTheDocument();
    expect(screen.getAllByText('Employment')).toHaveLength(2);
  });

  it('shows "No rows yet." when empty', () => {
    renderWithTeam([]);
    expect(screen.getByText('No rows yet.')).toBeInTheDocument();
  });
});
