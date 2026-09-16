import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { TemplateVersionDetailDto } from '@/features/admin/templates/types';
import type { ProposedEditDto } from '@/features/meetings/types';

const decisionsApi = vi.hoisted(() => ({
  getProposedEdits: vi.fn(),
  markDecisionApplied: vi.fn(),
}));
vi.mock('@/features/meetings/api/meeting-decisions-api', () => decisionsApi);

const sectionDom = vi.hoisted(() => ({
  jumpToFieldWhenVisible: vi.fn(),
}));
vi.mock('../lib/section-dom', () => sectionDom);

import { ProposedEditsPanel } from './proposed-edits-panel';

const templateVersion: TemplateVersionDetailDto = {
  id: 1,
  documentTemplateId: 1,
  versionNumber: 1,
  status: 'Published',
  publishedAt: '2026-01-01T00:00:00.000Z',
  rowVersion: null,
  sections: [
    {
      id: 1,
      sectionKey: 'goals',
      title: 'Goals',
      displayOrder: 0,
      fields: [{ id: 100, fieldKey: 'goals-field', fieldType: 'Text', label: 'Goal', required: false, displayOrder: 0, configJson: null }],
    },
  ],
};

function makeEdit(overrides: Partial<ProposedEditDto> = {}): ProposedEditDto {
  return {
    decisionId: 1,
    meetingId: 5,
    meetingTitle: 'Annual review meeting',
    targetFieldKey: 'goals-field',
    targetRowId: null,
    targetLabel: 'Reading goal',
    text: 'Increase the reading fluency target.',
    outcome: 'Agreed',
    recordedAt: '2026-09-10T00:00:00.000Z',
    appliedAt: null,
    ...overrides,
  };
}

describe('ProposedEditsPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows an empty hint when there are no proposed edits', async () => {
    decisionsApi.getProposedEdits.mockResolvedValue({ success: true, data: [] });
    render(<ProposedEditsPanel instanceId={7} templateVersion={templateVersion} />);

    expect(await screen.findByTestId('proposed-edits-empty')).toBeInTheDocument();
  });

  it('renders a proposed edit with its meeting, target and outcome', async () => {
    decisionsApi.getProposedEdits.mockResolvedValue({ success: true, data: [makeEdit()] });
    render(<ProposedEditsPanel instanceId={7} templateVersion={templateVersion} />);

    const row = await screen.findByTestId('proposed-edit-1');
    expect(row).toHaveTextContent('Annual review meeting');
    expect(row).toHaveTextContent('Reading goal');
    expect(row).toHaveTextContent('Increase the reading fluency target.');
    expect(row).toHaveTextContent('Agreed');
  });

  it('jumps to the target field via jumpToFieldWhenVisible', async () => {
    const user = userEvent.setup();
    decisionsApi.getProposedEdits.mockResolvedValue({ success: true, data: [makeEdit()] });
    render(<ProposedEditsPanel instanceId={7} templateVersion={templateVersion} />);

    await user.click(await screen.findByTestId('proposed-edit-jump-1'));
    expect(sectionDom.jumpToFieldWhenVisible).toHaveBeenCalledWith('document-field-100', 1);
  });

  it('marks a proposed edit applied and shows the applied state', async () => {
    const user = userEvent.setup();
    decisionsApi.getProposedEdits.mockResolvedValue({ success: true, data: [makeEdit()] });
    decisionsApi.markDecisionApplied.mockResolvedValue({
      success: true,
      data: makeEdit({ appliedAt: '2026-09-12T00:00:00.000Z' }),
    });
    render(<ProposedEditsPanel instanceId={7} templateVersion={templateVersion} />);

    await user.click(await screen.findByTestId('proposed-edit-mark-applied-1'));

    await waitFor(() => expect(decisionsApi.markDecisionApplied).toHaveBeenCalledWith(1));
    expect(await screen.findByTestId('proposed-edit-applied-1')).toBeInTheDocument();
    expect(screen.queryByTestId('proposed-edit-mark-applied-1')).not.toBeInTheDocument();
  });
});
