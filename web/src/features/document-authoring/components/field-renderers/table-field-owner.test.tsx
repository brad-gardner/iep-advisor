import { describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import type { TemplateFieldDto } from '@/features/admin/templates/types';
import { TableField } from './table-field';
import { DocumentFlushContext } from '../../hooks/flush-registry-context';
import { DocumentEditorContext, type DocumentEditorContextValue } from '../../hooks/document-editor-context';
import type { SaveResult } from '../../hooks/use-document-instance';
import type { StudentTeamCache } from '../../hooks/use-student-team';

const svcTypeCol = 'c1111111-1111-1111-1111-111111111111';
const fieldKey = 'f1111111-1111-1111-1111-111111111111';

const servicesField: TemplateFieldDto = {
  id: 10,
  fieldKey,
  fieldType: 'Table',
  label: 'Services',
  required: false,
  displayOrder: 0,
  configJson: JSON.stringify({
    semantic: 'services',
    columns: [{ columnKey: svcTypeCol, type: 'Text', label: 'Service', required: false, semantic: 'serviceType' }],
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

function renderField(
  value: unknown,
  onSave: (p: Record<string, unknown>) => Promise<SaveResult>,
  team: StudentTeamCache = teamOf(ana)
) {
  const editor = {
    instanceId: 1,
    studentId: 2,
    shareableEntries: { entries: [], isLoading: false, isError: false, ensureLoaded: async () => {} },
    team,
    setActiveField: () => {},
    clearActiveField: () => {},
  } as unknown as DocumentEditorContextValue;
  return render(
    <DocumentEditorContext.Provider value={editor}>
      <DocumentFlushContext.Provider value={registry}>
        <TableField field={servicesField} value={value} onSave={onSave} />
      </DocumentFlushContext.Provider>
    </DocumentEditorContext.Provider>
  );
}

describe('TableField owner picker (plan 2026-10-02-002)', () => {
  it('loads the owner picker from the editor team cache, listing active members', () => {
    renderField([{ _rowId: 'r1', [svcTypeCol]: 'OT' }], vi.fn());
    expect(screen.getByTestId(`field-${fieldKey}-row-0-owner`)).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'Ana Ito — Occupational therapist' })).toBeInTheDocument();
    expect(screen.getByText('No owner yet')).toBeInTheDocument();
  });

  it('sets an owner, flushing immediately through the same row autosave path (no debounce wait)', async () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    renderField([{ _rowId: 'r1', [svcTypeCol]: 'OT' }], onSave);

    fireEvent.change(screen.getByTestId(`field-${fieldKey}-row-0-owner`), { target: { value: '7' } });

    await waitFor(() =>
      expect(onSave).toHaveBeenCalledWith({ [fieldKey]: [{ _rowId: 'r1', [svcTypeCol]: 'OT', _ownerUserId: 7 }] })
    );
  });

  it('clears an owner back to Unassigned, dropping the key entirely', async () => {
    const onSave = vi.fn().mockResolvedValue({ ok: true, values: {} });
    renderField([{ _rowId: 'r1', [svcTypeCol]: 'OT', _ownerUserId: 7 }], onSave);

    fireEvent.change(screen.getByTestId(`field-${fieldKey}-row-0-owner`), { target: { value: '' } });

    await waitFor(() => expect(onSave).toHaveBeenCalled());
    const sent = (onSave.mock.calls.at(-1)?.[0] as Record<string, unknown>)[fieldKey] as Array<Record<string, unknown>>;
    expect('_ownerUserId' in sent[0]).toBe(false);
  });

  it('shows a server save warning next to the picker when the owner is rejected', async () => {
    const onSave = vi.fn().mockResolvedValue({
      ok: true,
      values: {},
      warnings: [{ fieldKey, rowId: 'r1', code: 'ownerNotTeamMember', message: 'Not an active team member.' }],
    });
    renderField([{ _rowId: 'r1', [svcTypeCol]: 'OT' }], onSave);

    fireEvent.change(screen.getByTestId(`field-${fieldKey}-row-0-owner`), { target: { value: '7' } });

    expect(await screen.findByRole('alert')).toHaveTextContent('Not an active team member.');
  });

  it('clears a stale warning once a later save comes back clean', async () => {
    const onSave = vi
      .fn()
      .mockResolvedValueOnce({
        ok: true,
        values: {},
        warnings: [{ fieldKey, rowId: 'r1', code: 'ownerNotTeamMember', message: 'Not an active team member.' }],
      })
      .mockResolvedValueOnce({ ok: true, values: {}, warnings: [] });
    renderField([{ _rowId: 'r1', [svcTypeCol]: 'OT' }], onSave);

    fireEvent.change(screen.getByTestId(`field-${fieldKey}-row-0-owner`), { target: { value: '7' } });
    expect(await screen.findByRole('alert')).toBeInTheDocument();

    fireEvent.change(screen.getByTestId(`field-${fieldKey}-row-0-owner`), { target: { value: '' } });
    await waitFor(() => expect(onSave).toHaveBeenCalledTimes(2));
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
