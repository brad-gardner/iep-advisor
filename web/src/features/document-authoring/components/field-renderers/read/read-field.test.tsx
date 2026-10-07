import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import type { TemplateFieldDto } from '@/features/admin/templates/types';
// `document-authoring` is a staff-only namespace (plan phase 5) — see
// `../../../staff-locales`'s doc comment and `docs/i18n/README.md`'s "Staff
// and admin namespaces". This component renders directly here (not through
// the lazy route), so its English must be registered the same way the real
// route chunk does.
import '../../../staff-locales';
import { ReadField } from './read-field';
import { DocumentEditorContext, type DocumentEditorContextValue } from '../../../hooks/document-editor-context';
import type { StudentTeamCache } from '../../../hooks/use-student-team';

function field(overrides: Partial<TemplateFieldDto>): TemplateFieldDto {
  return {
    id: 1,
    fieldKey: 'f-1',
    fieldType: 'Text',
    label: 'Field label',
    required: false,
    displayOrder: 0,
    configJson: null,
    ...overrides,
  };
}

describe('ReadField', () => {
  it('Text: renders the plain-text value, or "Not set" when blank', () => {
    const { rerender } = render(<ReadField field={field({ fieldType: 'Text' })} value="Jordan Ellis" />);
    expect(screen.getByTestId('read-field-f-1')).toHaveTextContent('Jordan Ellis');

    rerender(<ReadField field={field({ fieldType: 'Text' })} value="" />);
    expect(screen.getByTestId('read-field-f-1')).toHaveTextContent('Not set');
  });

  it('RichText: renders the stored markdown as formatted content', () => {
    render(<ReadField field={field({ fieldType: 'RichText' })} value={'**Strengths**\n\n- Oral vocabulary'} />);
    const container = screen.getByTestId('read-field-f-1');
    expect(container.querySelector('strong')).toHaveTextContent('Strengths');
    expect(container.querySelector('li')).toHaveTextContent('Oral vocabulary');
  });

  it('RichText: shows "Not set" when blank', () => {
    render(<ReadField field={field({ fieldType: 'RichText' })} value="" />);
    expect(screen.getByTestId('read-field-f-1')).toHaveTextContent('Not set');
  });

  it('Date: formats an ISO date for display', () => {
    render(<ReadField field={field({ fieldType: 'Date' })} value="2026-10-02" />);
    expect(screen.getByTestId('read-field-f-1')).toHaveTextContent('Oct 2, 2026');
  });

  it('Select: resolves the option label for the stored value', () => {
    const configJson = JSON.stringify({ options: [{ value: 'sld', label: 'Specific learning disability' }] });
    render(<ReadField field={field({ fieldType: 'Select', configJson })} value="sld" />);
    expect(screen.getByTestId('read-field-f-1')).toHaveTextContent('Specific learning disability');
  });

  it('Checkbox: shows Yes/No (never "Not set" — a boolean is never blank)', () => {
    const { rerender } = render(<ReadField field={field({ fieldType: 'Checkbox', label: 'Attended' })} value={true} />);
    expect(screen.getByTestId('read-field-f-1')).toHaveTextContent('Attended — Yes');

    rerender(<ReadField field={field({ fieldType: 'Checkbox', label: 'Attended' })} value={false} />);
    expect(screen.getByTestId('read-field-f-1')).toHaveTextContent('Attended — No');
  });

  it('Table: renders a header per column and one row per entry', () => {
    const configJson = JSON.stringify({
      columns: [
        { columnKey: 'c1', type: 'Text', label: 'Goal', required: false },
        { columnKey: 'c2', type: 'Checkbox', label: 'Mastered', required: false },
      ],
    });
    render(
      <ReadField
        field={field({ fieldType: 'Table', configJson })}
        value={[{ c1: 'Read 90 wpm', c2: true }]}
      />
    );
    const container = screen.getByTestId('read-field-f-1');
    expect(container).toHaveTextContent('Goal');
    expect(container).toHaveTextContent('Mastered');
    expect(container).toHaveTextContent('Read 90 wpm');
    expect(container).toHaveTextContent('Yes');
  });

  it('Table: shows "No rows yet." when the array is empty', () => {
    const configJson = JSON.stringify({ columns: [{ columnKey: 'c1', type: 'Text', label: 'Goal', required: false }] });
    render(<ReadField field={field({ fieldType: 'Table', configJson })} value={[]} />);
    expect(screen.getByTestId('read-field-f-1')).toHaveTextContent('No rows yet.');
  });

  describe('Table: owner column for an owner-eligible semantic (plan 2026-10-02-002)', () => {
    const svcTypeCol = 'c1111111-1111-1111-1111-111111111111';
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

    function servicesField(): TemplateFieldDto {
      return field({
        fieldType: 'Table',
        configJson: JSON.stringify({
          semantic: 'services',
          columns: [{ columnKey: svcTypeCol, type: 'Text', label: 'Service', required: false, semantic: 'serviceType' }],
        }),
      });
    }

    function renderWithTeam(value: unknown, team: StudentTeamCache) {
      const editor = { team } as unknown as DocumentEditorContextValue;
      return render(
        <DocumentEditorContext.Provider value={editor}>
          <ReadField field={servicesField()} value={value} />
        </DocumentEditorContext.Provider>
      );
    }

    it('shows the owner by name + role for an active team member', () => {
      renderWithTeam([{ _rowId: 'r1', [svcTypeCol]: 'OT', _ownerUserId: 7 }], { members: [ana], isLoading: false, isError: false });
      expect(screen.getByTestId('read-field-f-1')).toHaveTextContent('Owner');
      expect(screen.getByTestId('read-field-f-1')).toHaveTextContent('Ana Ito — Occupational therapist');
    });

    it('shows "Former team member" when the owner id is no longer active', () => {
      renderWithTeam([{ _rowId: 'r1', [svcTypeCol]: 'OT', _ownerUserId: 999 }], { members: [ana], isLoading: false, isError: false });
      expect(screen.getByTestId('read-field-f-1')).toHaveTextContent('Former team member');
    });

    it('shows no Owner column at all for an untagged table', () => {
      const configJson = JSON.stringify({ columns: [{ columnKey: svcTypeCol, type: 'Text', label: 'Service', required: false }] });
      render(<ReadField field={field({ fieldType: 'Table', configJson })} value={[{ _rowId: 'r1', [svcTypeCol]: 'OT' }]} />);
      expect(screen.queryByText('Owner')).not.toBeInTheDocument();
    });
  });
});
